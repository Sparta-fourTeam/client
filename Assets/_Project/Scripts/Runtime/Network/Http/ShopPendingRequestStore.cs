using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Network
{
    /// <summary>POST 전 요청을 계정별 파일에 남긴다. 재시작 시 원래 ID와 견적을 그대로 복구한다.</summary>
    public sealed class ShopPendingRequestStore
    {
        private readonly string _directory;
        private readonly IAuthTokenStore _tokens;
        private readonly long _generation;
        private readonly string _accountId;

        public ShopPendingRequestStore(string directory, IAuthTokenStore tokens)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _generation = tokens.Generation;
            _accountId = AccountId(tokens.AccessToken);
        }

        public HttpShopExchange Begin(HttpShopApi api, ShopExchangeRequest request)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }
            EnsureApi(api);
            string path = PathForCurrentAccount();
            bool recovered = File.Exists(path);
            if (recovered && !Same(Read(path), request))
            { throw new InvalidOperationException("기존 미확정 상점 요청을 먼저 복구해야 합니다"); }
            return new HttpShopExchange(api, request, this, recovered);
        }

        public HttpShopExchange Recover(HttpShopApi api)
        {
            EnsureApi(api);
            string path = PathForCurrentAccount();
            if (!File.Exists(path)) { return null; }
            return new HttpShopExchange(api, Read(path), this, true);
        }

        internal void Prepare(ShopExchangeRequest request)
        {
            string path = PathForCurrentAccount();
            if (File.Exists(path))
            {
                var pending = Read(path);
                if (!Same(pending, request))
                { throw new InvalidOperationException("기존 미확정 상점 요청을 먼저 복구해야 합니다"); }
                return;
            }
            Directory.CreateDirectory(_directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var data = new JObject
            {
                ["schema"] = 1,
                ["requestId"] = request.RequestId,
                ["operation"] = request.Operation,
                ["dayKey"] = request.DayKey == null ? JValue.CreateNull() : new JValue(request.DayKey),
                ["rerollIndex"] = request.RerollIndex,
                ["slot"] = request.Slot.HasValue ? new JValue(request.Slot.Value) : JValue.CreateNull(),
                ["configVersion"] = request.ConfigVersion,
                ["index"] = request.Index
            };
            try
            {
                File.WriteAllText(temporary, data.ToString(Formatting.None));
                if (File.Exists(path))
                {
                    if (!Same(Read(path), request))
                    { throw new InvalidOperationException("기존 미확정 상점 요청을 먼저 복구해야 합니다"); }
                    return;
                }
                File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
        }

        internal void Complete(ShopExchangeRequest request)
        {
            string path = PathForCurrentAccount();
            if (File.Exists(path) && Same(Read(path), request)) { File.Delete(path); }
        }

        private static ShopExchangeRequest Read(string path)
        {
            try
            {
                using var reader = new JsonTextReader(new StringReader(File.ReadAllText(path)))
                { DateParseHandling = DateParseHandling.None };
                var data = JObject.Load(reader, new JsonLoadSettings
                { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read() || (int?)data["schema"] != 1 || data.Properties().Count() != 8)
                { throw new JsonException(); }
                return ShopExchangeRequest.Restore((string)data["requestId"], (string)data["operation"],
                    (string)data["dayKey"], (int)data["rerollIndex"], (int?)data["slot"],
                    (int)data["configVersion"], (int)data["index"]);
            }
            catch (Exception error) when (error is JsonException || error is ArgumentException || error is InvalidCastException
                || error is NullReferenceException)
            { throw new InvalidDataException("저장된 상점 요청이 손상되어 자동 재시도할 수 없습니다", error); }
        }

        private static bool Same(ShopExchangeRequest a, ShopExchangeRequest b) =>
            a.RequestId == b.RequestId && a.Operation == b.Operation && a.DayKey == b.DayKey
            && a.RerollIndex == b.RerollIndex && a.Slot == b.Slot
            && a.ConfigVersion == b.ConfigVersion && a.Index == b.Index;

        private string PathForCurrentAccount()
        {
            if (_tokens.Generation != _generation || AccountId(_tokens.AccessToken) != _accountId)
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
            return Path.Combine(_directory, "shop-pending-" + _accountId + ".json");
        }

        private void EnsureApi(HttpShopApi api)
        {
            if (api == null) { throw new ArgumentNullException(nameof(api)); }
            if (!ReferenceEquals(api.TokenStore, _tokens))
            { throw new ArgumentException("상점 API와 대기 요청 저장소의 인증 세션이 다릅니다", nameof(api)); }
            api.EnsureSession();
        }

        private static string AccountId(string token)
        {
            if (string.IsNullOrEmpty(token)) { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
            try
            {
                string[] parts = token.Split('.');
                if (parts.Length != 3) { throw new FormatException(); }
                string payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
                var claims = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
                string subject = (string)claims["sub"];
                if (!long.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out long id) || id <= 0
                    || subject != id.ToString(CultureInfo.InvariantCulture)) { throw new FormatException(); }
                return subject;
            }
            catch (Exception error) when (error is FormatException || error is JsonException || error is InvalidCastException)
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
        }
    }
}
