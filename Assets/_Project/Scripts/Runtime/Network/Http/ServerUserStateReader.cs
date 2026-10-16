using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Network
{
    internal static class ServerUserStateReader
    {
        internal static ServerUserData Parse(string body)
        {
            try
            {
                using var reader = new JsonTextReader(new StringReader(body ?? string.Empty))
                {
                    DateParseHandling = DateParseHandling.None,
                };
                var state = JObject.Load(reader);
                if (reader.Read() || !IsCompleteState(state))
                {
                    throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse);
                }
                return state.ToObject<ServerUserData>();
            }
            catch (JsonException)
            {
                throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse);
            }
        }

        // 누락 필드가 기본값 0/빈 목록으로 바뀐 부분 응답을 상태 재조회 성공으로 취급하지 않는다.
        // 현재 PR59 user/me 형태만 받으며, ID를 Local ID로 변환하거나 시간대 없는 시각을 UTC로 바꾸지 않는다.
        private static bool IsCompleteState(JObject state) =>
            Integer(state["gold"], 0, int.MaxValue) && Integer(state["gem"], 0, int.MaxValue)
            && (state["exp"]?.Type == JTokenType.Null || Integer(state["exp"], 0, int.MaxValue))
            && Integer(state["energyStored"], 0, int.MaxValue) && Text(state["energyUpdatedAt"])
            && Rows(state["stageProgress"], "stageId", row => Integer(row["stageId"], int.MinValue, int.MaxValue)
                && Integer(row["clearRating"], 0, 3) && Integer(row["claimedRating"], 0, 3)
                && row.Value<int>("claimedRating") <= row.Value<int>("clearRating"))
            && Rows(state["upgrades"], "upgradeId", row => Text(row["upgradeId"])
                && (row["level"]?.Type == JTokenType.Null || Integer(row["level"], 0, int.MaxValue)))
            && Rows(state["items"], "itemId", row => Text(row["itemId"]) && Integer(row["quantity"], 0, int.MaxValue))
            && Rows(state["codex"], "monsterId", row => Integer(row["monsterId"], long.MinValue, long.MaxValue)
                && row["claimed"]?.Type == JTokenType.Boolean)
            && state["tutorials"] is JArray tutorials
            && tutorials.All(id => Integer(id, long.MinValue, long.MaxValue));

        private static bool Rows(JToken token, string identityKey, Func<JObject, bool> valid) =>
            token is JArray rows && rows.All(row => row is JObject value && valid(value))
            && rows.Select(row => row[identityKey].ToString()).Distinct(StringComparer.Ordinal).Count() == rows.Count;

        private static bool Text(JToken token) =>
            token?.Type == JTokenType.String && !string.IsNullOrWhiteSpace(token.Value<string>());

        private static bool Integer(JToken token, long min, long max) =>
            token?.Type == JTokenType.Integer
            && long.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value >= min && value <= max;

    }
}
