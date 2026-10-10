using System;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core;
using Game.Network;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class UpgradeConfirmationTests
    {
        private const string Id = "equipment.hat";
        private GameDataStore _data;
        private PlayerProfile _profile;
        private LocalSaveStore _store;
        private string _path;
        private GameObject _root;
        private UpgradeConfirmPopupView _view;
        private SpyApi _api;
        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private sealed class SpyApi : IUpgradeApi
        {
            public LocalUpgradeApi Inner;
            public int Calls, FailAt;
            public UniTaskCompletionSource<PlayerSnapshot> Pending;
            public UniTask<PlayerSnapshot> Purchase(string id, string key)
            {
                Calls++;
                if (Pending != null)
                {
                    return Pending.Task;
                }

                if (Calls == FailAt)
                {
                    throw new ApiException(ApiErrorKind.Network, "OFFLINE");
                }

                return Inner.Purchase(id, key);
            }
        }
        [SetUp]
        public void SetUp()
        {
            _data = new GameDataStore();
            _profile = new PlayerProfile(_data);
            _path = Path.Combine(Path.GetTempPath(), "nova-upgrade-" + Guid.NewGuid().ToString("N") + ".json");
            _store = new LocalSaveStore(_path);
            var def = _data.Upgrades.GetOrThrow(Id);
            var save = _store.Load();
            int required = _data.UnlockLevelOf(def);
            for (int i = 1; i < required; i++)
            {
                save.exp += _data.PlayerLevels.RequiredToNext(i);
            }

            save.wallet.gold = def.CostAt(1) + def.CostAt(2);
            save.upgrades = new();
            save.items = new() { new() { itemId = def.MaterialItemId, quantity = def.MaterialAt(1) + def.MaterialAt(2) } };
            _store.Flush(save);
            _profile.Apply(new LocalPlayerApi(_store).GetMe().GetAwaiter().GetResult());
            _api = new SpyApi { Inner = new LocalUpgradeApi(_store, _data) };
            _root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Lobby/UpgradeConfirmPopup.prefab"));
            _view = _root.GetComponent<UpgradeConfirmPopupView>();
            _view.Initialize();
            _view.Construct(_profile, _data, _api);
        }
        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll(); Object.DestroyImmediate(_root); if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        private void Confirm() => Field<Button>(_view, "_confirmButton").onClick.Invoke();
        [Test]
        public void BatchQuoteSumsEveryLevelAndCapsAtAffordableResources()
        {
            var def = _data.Upgrades.GetOrThrow(Id);
            var plan = UpgradePurchasePlan.Create(_profile, _data, Id, true);
            Assert.That(plan.Count, Is.EqualTo(2));
            Assert.That(plan.CoinCost, Is.EqualTo(def.CostAt(1) + def.CostAt(2)));
            Assert.That(plan.MaterialCost, Is.EqualTo(def.MaterialAt(1) + def.MaterialAt(2)));
            Assert.That(UpgradePurchasePlan.Create(_profile, _data, Id, false).Count, Is.EqualTo(1));
        }
        [Test]
        public void CancelIncludingLateConfirmNeverPurchases()
        {
            _view.Show("후드", Id, true);
            Assert.That(_api.Calls, Is.Zero);
            Field<Button>(_view, "_cancelButton").onClick.Invoke();
            Confirm();
            Assert.That(_api.Calls, Is.Zero);
            Assert.That(_profile.UpgradeLevel(Id), Is.Zero);
        }
        [Test]
        public void ConfirmedBatchPurchasesExactlyTheQuoteOnce()
        {
            var plan = UpgradePurchasePlan.Create(_profile, _data, Id, true);
            int gold = _profile.Gold, material = _profile.ItemQuantity(plan.MaterialItemId);
            _view.Show("후드", Id, true); Confirm(); Confirm();
            Assert.That(_api.Calls, Is.EqualTo(2));
            Assert.That(_profile.UpgradeLevel(Id), Is.EqualTo(2));
            Assert.That(_profile.Gold, Is.EqualTo(gold - plan.CoinCost));
            Assert.That(_profile.ItemQuantity(plan.MaterialItemId), Is.EqualTo(material - plan.MaterialCost));
        }
        [Test]
        public void ChangedQuoteRequiresAnotherConfirmationWithoutSpending()
        {
            _view.Show("후드", Id, true);
            var snapshot = _profile.Snapshot();
            snapshot.gold = _data.Upgrades.GetOrThrow(Id).CostAt(1);
            _profile.Apply(snapshot);
            Confirm(); Assert.That(_api.Calls, Is.Zero);
            Confirm(); Assert.That(_api.Calls, Is.EqualTo(1));
        }
        [Test]
        public void InFlightRequestBlocksRepeatedConfirmAndCancel()
        {
            _api.Pending = new UniTaskCompletionSource<PlayerSnapshot>();
            _view.Show("후드", Id, false); Confirm(); Confirm();
            Field<Button>(_view, "_cancelButton").onClick.Invoke();
            Assert.That(_api.Calls, Is.EqualTo(1));
            Assert.That(_view.IsBusy, Is.True);
            var result = _api.Inner.Purchase(Id, "finish").GetAwaiter().GetResult();
            _api.Pending.TrySetResult(result);
            Assert.That(_view.IsBusy, Is.False);
            Assert.That(_profile.UpgradeLevel(Id), Is.EqualTo(1));
        }
        [Test]
        public void PartialFailureKeepsCompletedUpgradeAndShowsRemainingQuote()
        {
            _api.FailAt = 2;
            _view.Show("후드", Id, true); Confirm();
            Assert.That(_profile.UpgradeLevel(Id), Is.EqualTo(1));
            Assert.That(Field<UpgradePurchasePlan>(_view, "_plan").Count, Is.EqualTo(1));
            Assert.That(Field<TMPro.TMP_Text>(_view, "_description").text, Does.Contain("1회 강화 완료"));
            Assert.That(_view.IsBusy, Is.False);
            Confirm();
            Assert.That(_profile.UpgradeLevel(Id), Is.EqualTo(2));
        }
        [Test]
        public void LockedMaxedAndInsufficientMaterialsHaveNoPurchases()
        {
            var snapshot = _profile.Snapshot(); snapshot.exp = 0; _profile.Apply(snapshot);
            Assert.That(UpgradePurchasePlan.Create(_profile, _data, Id, true).Count, Is.Zero);
            snapshot.exp = 1000000;
            snapshot.upgrades = new() { new() { upgradeId = Id, level = _data.Upgrades.GetOrThrow(Id).MaxLevel } };
            _profile.Apply(snapshot);
            Assert.That(UpgradePurchasePlan.Create(_profile, _data, Id, true).Count, Is.Zero);
            snapshot.upgrades.Clear(); snapshot.items.Clear(); _profile.Apply(snapshot);
            Assert.That(UpgradePurchasePlan.Create(_profile, _data, Id, true).Count, Is.Zero);
        }
    }
}
