using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Network
{
    /// <summary>서버 #78/#79의 필수 필드·기간·영수증을 검증한다. 누락을 완료나 빈 보드로 바꾸지 않는다.</summary>
    internal static class ServerProgressContract
    {
        internal static T Parse<T>(Func<T> parse)
        {
            try { return parse(); }
            catch (JsonException) { throw Invalid(); }
            catch (ArgumentException) { throw Invalid(); }
            catch (InvalidCastException) { throw Invalid(); }
            catch (FormatException) { throw Invalid(); }
            catch (OverflowException) { throw Invalid(); }
        }

        internal static JToken Read(string json)
        {
            try
            {
                using var reader = new JsonTextReader(new StringReader(json ?? string.Empty))
                { DateParseHandling = DateParseHandling.None };
                var token = JToken.Load(reader, new JsonLoadSettings
                { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) { throw Invalid(); }
                return token;
            }
            catch (JsonException) { throw Invalid(); }
        }

        internal static ServerUserData ReadMe(string json)
        {
            var state = Read(json) as JObject;
            if (state == null || !Integer(state["gold"])
                || !(state.Property("gem") == null ? Integer(state["jem"]) : Integer(state["gem"]))
                || !(Null(state["exp"]) || Integer(state["exp"]))
                || !Integer(state["energyStored"]) || !EnergyTime(state["energyUpdatedAt"])
                || state["tutorials"] is not JArray
                || !Rows(state["stageProgress"], "stageId", row => Integer(row["stageId"], int.MinValue)
                    && Integer(row["clearRating"], 0, 3) && Integer(row["claimedRating"], 0, row.Value<int>("clearRating")))
                || !Rows(state["upgrades"], "upgradeId", row => Text(row["upgradeId"])
                    && (Null(row["level"]) || Integer(row["level"])))
                || !ItemRows(state["items"])
                || !Rows(state["codex"], "monsterId", row => Integer(row["monsterId"], 1)
                    && Boolean(row["claimed"])))
            { throw Invalid(); }
            ValidateTutorials(state["tutorials"], state["tutorialStates"]);
            ValidateBoards(state["quests"]);
            return state.ToObject<ServerUserData>();
        }

        internal static List<ServerTutorialState> ReadTutorials(string json)
        {
            var token = Read(json);
            ValidateTutorials(null, token);
            return token.ToObject<List<ServerTutorialState>>();
        }

        internal static List<ServerQuestBoard> ReadBoards(string json)
        {
            var token = Read(json);
            ValidateBoards(token);
            return token.ToObject<List<ServerQuestBoard>>();
        }

        internal static void ValidateTutorials(JToken ids, JToken states)
        {
            if (!Rows(states, "tutorialId", row => TutorialId(row["tutorialId"], false)
                && Boolean(row["dialogueSeen"]) && Boolean(row["completed"]) && Boolean(row["rewardClaimed"])
                && row.Value<bool>("completed") == row.Value<bool>("rewardClaimed")
                && FlagTime(row, "dialogueSeen", "dialogueSeenAt") && FlagTime(row, "completed", "completedAt")
                && (row.Value<bool>("dialogueSeen") || row.Value<bool>("completed"))))
            { throw Invalid(); }
            if (ids == null) { return; }
            if (ids is not JArray list || !list.All(id => TutorialId(id, true))
                || list.Select(id => id.Value<string>()).Distinct(StringComparer.Ordinal).Count() != list.Count)
            { throw Invalid(); }
            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var state in (JArray)states)
            {
                var id = state.Value<string>("tutorialId");
                if (state.Value<bool>("dialogueSeen")) { expected.Add(id + ".dialogue"); }
                if (state.Value<bool>("completed")) { expected.Add(id); }
            }
            if (!expected.SetEquals(list.Select(id => id.Value<string>()))) { throw Invalid(); }
        }

        internal static void ValidateBoards(JToken boards)
        {
            if (boards is not JArray array || array.Count != 2
                || !Rows(array, "period", board => Period(board["period"]) && DayToken(board["periodKey"])
                    && Utc(board["nextResetAt"]) && (Null(board["lastLoginDay"]) || DayToken(board["lastLoginDay"]))
                    && Integer(board["loginCount"], 0, board.Value<string>("period") == "DAILY" ? 1 : 7)
                    && Integer(board["spentEnergy"]) && Integer(board["points"], 0, long.MaxValue)))
            { throw Invalid(); }
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var board in array.Cast<JObject>())
            {
                if (!Rows(board["quests"], "id", row => Id(row["id"]) && usedIds.Add(row.Value<string>("id"))
                    && (row.Value<string>("kind") == "LOGIN" || row.Value<string>("kind") == "SPEND_ENERGY" || row.Value<string>("kind") == "KILL_MONSTERS")
                    && Integer(row["target"], 1) && Integer(row["points"], 1)
                    && Integer(row["count"], 0, row.Value<int>("target")) && Boolean(row["claimed"])
                    && (!row.Value<bool>("claimed") || row.Value<int>("count") == row.Value<int>("target"))
                    && FlagTime(row, "claimed", "claimedAt"))
                    || !Rows(board["chests"], "id", row => Id(row["id"]) && usedIds.Add(row.Value<string>("id"))
                        && Integer(row["points"], 1) && Boolean(row["claimed"])
                        && FlagTime(row, "claimed", "claimedAt")
                        && (!row.Value<bool>("claimed") || row.Value<int>("points") <= board.Value<long>("points"))))
                { throw Invalid(); }
                long points = ((JArray)board["quests"]).Where(row => row.Value<bool>("claimed"))
                    .Sum(row => (long)row.Value<int>("points"));
                if (points != board.Value<long>("points")) { throw Invalid(); }
            }
        }

        internal static TutorialCompletionReceipt ReadTutorialReceipt(string json, TutorialCompletionRequest request)
        {
            var receipt = Read(json) as JObject;
            bool dialogue = request.tutorialId.EndsWith(".dialogue", StringComparison.Ordinal);
            if (receipt == null || receipt.Value<string>("tutorialId") != request.tutorialId
                || !Boolean(receipt["dialogueOnly"]) || receipt.Value<bool>("dialogueOnly") != dialogue
                || !Integer(receipt["gold"]) || !Integer(receipt["expBefore"]) || !Integer(receipt["expAfter"])
                || !Utc(receipt["recordedAt"]))
            { throw Invalid(); }
            var items = Items(receipt["items"]);
            if (dialogue && (receipt.Value<int>("gold") != 0 || items.Count != 0
                || receipt.Value<int>("expBefore") != 0 || receipt.Value<int>("expAfter") != 0))
            { throw Invalid(); }
            return new TutorialCompletionReceipt(request.tutorialId, dialogue, receipt.Value<int>("gold"), items,
                receipt.Value<int>("expBefore"), receipt.Value<int>("expAfter"), receipt.Value<string>("recordedAt"));
        }

        internal static QuestClaimReceipt ReadQuestReceipt(string json, QuestClaimRequest request, bool chest)
        {
            var receipt = Read(json) as JObject;
            if (receipt == null || receipt.Value<string>("id") != request.id
                || receipt.Value<string>("period") != request.period || receipt.Value<string>("periodKey") != request.periodKey
                || !Boolean(receipt["chest"]) || receipt.Value<bool>("chest") != chest
                || !Integer(receipt["points"], chest ? 0 : 1, chest ? 0 : int.MaxValue)
                || !Integer(receipt["gold"], 0, chest ? int.MaxValue : 0)
                || !Integer(receipt["energy"], 0, chest ? int.MaxValue : 0)
                || !Integer(receipt["gems"], 0, chest ? int.MaxValue : 0) || !Utc(receipt["claimedAt"]))
            { throw Invalid(); }
            var items = Items(receipt["items"]);
            if (!chest && items.Count != 0) { throw Invalid(); }
            return new QuestClaimReceipt(request, chest, receipt.Value<int>("points"), receipt.Value<int>("gold"),
                receipt.Value<int>("energy"), receipt.Value<int>("gems"), items, receipt.Value<string>("claimedAt"));
        }

        private static Dictionary<string, int> Items(JToken token)
        {
            if (token is not JObject rows || rows.Properties().Any(p => string.IsNullOrWhiteSpace(p.Name) || !Integer(p.Value, 1)))
            { throw Invalid(); }
            return rows.Properties().ToDictionary(p => p.Name, p => p.Value.Value<int>(), StringComparer.Ordinal);
        }

        internal static bool Day(string value) => value != null && Regex.IsMatch(value, @"\A[0-9]{8}\z")
            && DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        private static bool DayToken(JToken token) => Text(token) && Day(token.Value<string>());
        private static bool Period(JToken token) => Text(token) && (token.Value<string>() == "DAILY" || token.Value<string>() == "WEEKLY");
        private static bool Id(JToken token) => Text(token) && Regex.IsMatch(token.Value<string>(), @"\A[A-Za-z0-9_.-]{1,64}\z");
        private static bool TutorialId(JToken token, bool dialogue) => Text(token)
            && Regex.IsMatch(token.Value<string>(), dialogue ? @"\A[A-Za-z0-9_-]{1,64}(\.dialogue)?\z" : @"\A[A-Za-z0-9_-]{1,64}\z");
        private static bool FlagTime(JObject row, string flag, string time) => row.Value<bool>(flag) ? Utc(row[time]) : Null(row[time]);
        private static bool Boolean(JToken token) => token?.Type == JTokenType.Boolean;
        private static bool Null(JToken token) => token?.Type == JTokenType.Null;
        private static bool Text(JToken token) => token?.Type == JTokenType.String && !string.IsNullOrWhiteSpace(token.Value<string>());
        private static bool Integer(JToken token, long min = 0, long max = int.MaxValue) => token?.Type == JTokenType.Integer
            && long.TryParse(token.ToString(Formatting.None), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value >= min && value <= max;
        private static bool ItemRows(JToken token) => token is JArray rows
            && rows.All(row => row is JObject item && (Text(item["itemId"])
                || Integer(item["itemId"], long.MinValue, long.MaxValue)) && Integer(item["quantity"]))
            && rows.Where(row => row["itemId"].Type == JTokenType.String).Select(row => row.Value<string>("itemId"))
                .Distinct(StringComparer.Ordinal).Count() == rows.Count(row => row["itemId"].Type == JTokenType.String);
        private static bool EnergyTime(JToken token) => Utc(token) || (Text(token)
            && DateTime.TryParseExact(token.Value<string>(), new[] { "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
        private static bool Utc(JToken token) => Text(token) && DateTimeOffset.TryParseExact(token.Value<string>(),
            new[] { "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'" }, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _);
        private static bool Rows(JToken token, string key, Func<JObject, bool> valid) => token is JArray rows
            && rows.All(row => row is JObject value && valid(value))
            && rows.Select(row => row[key].ToString()).Distinct(StringComparer.Ordinal).Count() == rows.Count;
        internal static ApiException Invalid() => new(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse);
    }
}
