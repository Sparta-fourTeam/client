using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HttpEnergyApiTests
    {
        private AuthSession _session;
        private Transport _transport;
        private HttpEnergyApi _api;
        private sealed class Transport : IHttpTransport
        {
            public readonly List<HttpRequestData> Requests = new();
            public readonly Queue<HttpResponseData> Responses = new();
            public UniTaskCompletionSource<HttpResponseData> Gate;
            public string GatePath;
            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                if (Gate != null && (GatePath == null || request.Url.EndsWith(GatePath))) { var gate = Gate; Gate = null; return gate.Task; }
                return UniTask.FromResult(Responses.Dequeue());
            }
        }
        [SetUp]
        public void SetUp()
        {
            _session = new AuthSession(); _session.Set("access-a", "refresh-a");
            _transport = new Transport();
            _api = new HttpEnergyApi(new HttpApiConfig("https://example.test"), _transport, _session);
        }
        private void Queue(string body, int status = 200) => _transport.Responses.Enqueue(new HttpResponseData(status, body));
        private static string Me(int gems = 231, int energy = 19) =>
            $"{{\"gold\":19,\"gem\":{gems},\"exp\":0,\"energyStored\":{energy},\"energyUpdatedAt\":\"2026-10-19T18:00:00Z\",\"stageProgress\":[],\"upgrades\":[],\"items\":[],\"codex\":[],\"tutorials\":[]}}";
        private static string State(int count = 4, string now = "2026-10-19T18:00:00.123456789Z") =>
            $"{{\"serverTime\":\"{now}\",\"purchaseDay\":\"2026-10-19\",\"purchaseCount\":{count},\"remainingPurchases\":{7 - count},\"resetsAt\":\"2026-10-19T19:00:00Z\",\"gemCost\":21,\"energyGranted\":9,\"dailyLimit\":7}}";
        private static string Receipt(string key = "transaction-a") =>
            $"{{\"createId\":\"{key}\",\"source\":\"Purchase\",\"energyGranted\":9,\"gemsSpent\":21,\"purchaseDay\":\"2026-10-18\",\"purchaseCount\":1,\"completedAt\":\"2026-10-18T15:00:00Z\"}}";
        private void Current(int count = 4) { Queue(Me()); Queue(State(count)); }
        private ApiException Error(UniTask<PlayerSnapshot> task) => Assert.Throws<ApiException>(() => task.GetAwaiter().GetResult());

        [Test]
        public void Purchase_ReadsCurrentBalancesAndLimit_InsteadOfReplayingReceiptAmounts()
        {
            Queue(Receipt()); Current();
            var snapshot = _api.Recover(EnergySource.Purchase, "transaction-a").GetAwaiter().GetResult();
            Assert.That(snapshot.gems, Is.EqualTo(231)); Assert.That(snapshot.energyStored, Is.EqualTo(19));
            Assert.That(_api.PurchaseState.PurchaseCount, Is.EqualTo(4));
            Assert.That(_api.PurchaseState.RemainingPurchases, Is.EqualTo(3));
            Assert.That(_api.PurchaseState.GemCost, Is.EqualTo(21));
            Assert.That(_api.PurchaseState.EnergyGranted, Is.EqualTo(9));
            Assert.That(_api.PurchaseState.RemainingSeconds, Is.InRange(3598d, 3600d));
            Assert.That(_api.HasPendingPurchase, Is.False);
            CollectionAssert.AreEqual(new[] { "https://example.test/api/user/energy/recover", "https://example.test/api/user/me",
                "https://example.test/api/user/energy/purchase-state" }, _transport.Requests.ConvertAll(r => r.Url));
            var request = JObject.Parse(_transport.Requests[0].Body);
            Assert.That(request["source"].Value<string>(), Is.EqualTo("Purchase"));
            Assert.That(request.Count, Is.EqualTo(2), "지급량·가격·계정 ID를 클라가 보내지 않는다");
        }

        [Test]
        public void LostResponse_RetryKeepsKey_AndPreventsDifferentPurchase()
        {
            Queue("", 503);
            Assert.That(Error(_api.Recover(EnergySource.Purchase, "transaction-a")), Is.Not.Null);
            Assert.That(_api.HasPendingPurchase, Is.True);
            Assert.That(Error(_api.Recover(EnergySource.Purchase, "transaction-b")).Code, Is.EqualTo("ENERGY_RECOVERY_REQUIRED"));
            Queue(Receipt()); Current();
            _api.Recover(EnergySource.Purchase).GetAwaiter().GetResult();
            Assert.That(_transport.Requests[1].Body, Is.EqualTo(_transport.Requests[0].Body));
        }

        [Test]
        public void AcknowledgedReceipt_BalanceReadFailure_RetriesOnlyReads()
        {
            Queue(Receipt()); Queue("", 500);
            Error(_api.Recover(EnergySource.Purchase, "transaction-a"));
            Assert.That(_api.HasPendingPurchase, Is.True);
            Current(); _api.Recover(EnergySource.Purchase).GetAwaiter().GetResult();
            Assert.That(_transport.Requests.FindAll(r => r.Verb == HttpVerb.Post).Count, Is.EqualTo(1));
        }

        [TestCase(400, false)]
        [TestCase(409, true)]
        [TestCase(500, true)]
        public void Rejection_PreservesOnlyUncertainTransactions(int status, bool pending)
        {
            Queue("", status); Error(_api.Recover(EnergySource.Purchase, "transaction-a"));
            Assert.That(_api.HasPendingPurchase, Is.EqualTo(pending)); Assert.That(_api.PurchaseState, Is.Null);
        }

        [Test]
        public void EarlierAmbiguousAttempt_Later400DoesNotForgetKey()
        {
            Queue("", 500); Error(_api.Recover(EnergySource.Purchase, "transaction-a"));
            Queue("", 400); Error(_api.Recover(EnergySource.Purchase));
            Assert.That(_api.HasPendingPurchase, Is.True);
        }

        [Test]
        public async Task DoubleClick_IsBlockedWhilePurchaseIsInFlight()
        {
            var gate = _transport.Gate = new UniTaskCompletionSource<HttpResponseData>();
            var first = _api.Recover(EnergySource.Purchase, "transaction-a").AsTask();
            Assert.That(Error(_api.Recover(EnergySource.Purchase)).Code, Is.EqualTo("ENERGY_REQUEST_BUSY"));
            Current(); gate.TrySetResult(new HttpResponseData(200, Receipt())); await first;
            Assert.That(_transport.Requests.Count, Is.EqualTo(3));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task AccountChangeOrLogout_DropsLateReceiptAndDoesNotReadNewAccount(bool logOut)
        {
            var gate = _transport.Gate = new UniTaskCompletionSource<HttpResponseData>();
            var first = _api.Recover(EnergySource.Purchase, "transaction-a").AsTask();
            if (logOut) { _session.Clear(); } else { _session.Set("access-b", "refresh-b"); }
            gate.TrySetResult(new HttpResponseData(200, Receipt()));
            var error = Assert.ThrowsAsync<ApiException>(async () => await first);
            Assert.That(error.Code, Is.EqualTo(HttpErrorMapper.SessionExpired));
            Assert.That(_transport.Requests.Count, Is.EqualTo(1));
            Assert.That(_api.PurchaseState, Is.Null); Assert.That(_api.HasPendingPurchase, Is.False);
            await Task.CompletedTask;
        }

        [Test]
        public async Task AccountChangeDuringStatusRead_DropsCachedStatus()
        {
            Queue(Me());
            var gate = new UniTaskCompletionSource<HttpResponseData>();
            // 첫 읽기를 보류했다가 새 계정으로 바뀐 뒤 완료한다.
            _transport.Gate = gate;
            var first = _api.RefreshPurchaseState().AsTask();
            _session.Set("access-b", "refresh-b");
            gate.TrySetResult(new HttpResponseData(200, Me()));
            Assert.ThrowsAsync<ApiException>(async () => await first);
            Assert.That(_api.PurchaseState, Is.Null);
            await Task.CompletedTask;
        }

        [Test]
        public async Task LogoutDuringStatusResponse_DoesNotCacheLateState()
        {
            Queue(Me());
            var gate = _transport.Gate = new UniTaskCompletionSource<HttpResponseData>();
            _transport.GatePath = "/energy/purchase-state";
            var request = _api.RefreshPurchaseState().AsTask();
            _session.Clear(); gate.TrySetResult(new HttpResponseData(200, State()));
            var error = Assert.ThrowsAsync<ApiException>(async () => await request);
            Assert.That(error.Code, Is.EqualTo(HttpErrorMapper.SessionExpired));
            Assert.That(_api.PurchaseState, Is.Null);
            Assert.That(_transport.Requests.Count, Is.EqualTo(2));
            await Task.CompletedTask;
        }

        [TestCase("{}")]
        [TestCase("{\"purchaseCount\":0}")]
        public void IncompleteState_IsRejectedInsteadOfDefaultZeros(string state)
        {
            Queue(Me()); Queue(state);
            Assert.That(Error(_api.RefreshPurchaseState()).Code, Is.EqualTo(HttpErrorMapper.InvalidResponse));
            Assert.That(_api.PurchaseState, Is.Null);
        }

        [Test]
        public void OldReceiptAfterReset_UsesCurrentZeroCount_AndNextPurchaseGetsNewKey()
        {
            Queue(Receipt()); Current(0); _api.Recover(EnergySource.Purchase, "transaction-a").GetAwaiter().GetResult();
            Assert.That(_api.PurchaseState.PurchaseCount, Is.Zero);
            Assert.That(_api.PurchaseState.RemainingPurchases, Is.EqualTo(7));
            Queue("", 503); Error(_api.Recover(EnergySource.Purchase));
            Assert.That(_transport.Requests[3].Body, Does.Not.Contain("transaction-a"));
        }

        [Test]
        public void AdsRemainDisabledWithoutHttp()
        {
            Assert.That(Error(_api.Recover(EnergySource.Ad)).Code, Is.EqualTo("AD_NOT_READY"));
            Assert.That(_transport.Requests, Is.Empty);
        }
    }
}
