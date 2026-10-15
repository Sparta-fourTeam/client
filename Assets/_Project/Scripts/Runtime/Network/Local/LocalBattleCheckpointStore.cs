using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VContainer;

namespace Game.Network
{
    /// <summary>기기 전용 전투 저장. 이전 완성본을 남기고 파일을 교체하며 계정·테이블·전투 발급 기록을 확인한다.</summary>
    public sealed class LocalBattleCheckpointStore : IBattleCheckpointStore
    {
        private const int MaximumBytes = 8 * 1024 * 1024;
        private readonly LocalSaveStore _save;
        private readonly IAuthCredentialStore _credentials;
        private readonly PlayerProfile _profile;
        private readonly GameDataStore _data;
        private readonly string _content;
        private static readonly JsonSerializerSettings Settings = new()
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 64,
            Converters = { new VectorConverter() }
        };
        private sealed class Envelope
        {
            public string Owner, Content;
            public BattleCheckpoint Checkpoint;
        }
        [Inject]
        public LocalBattleCheckpointStore(LocalSaveStore save, IAuthCredentialStore credentials, PlayerProfile profile, GameDataStore data)
        {
            _save = save; _credentials = credentials; _profile = profile; _data = data;
            _content = Hash(Application.version + string.Concat(data.Revisions.Keys.OrderBy(n => n).Select(data.RawJson)));
        }
        private string Owner => _credentials.HasCredential
            ? Hash($"{_credentials.Method}|{_credentials.AccountId}|{_profile.Nickname}") : null;
        private static string Hash(string value)
        {
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }
        public BattleCheckpoint Load()
        {
            if (Owner == null) { return null; }
            foreach (string path in new[] { _save.BattleCheckpointPath, _save.BattleCheckpointPath + ".bak" })
            {
                if (!File.Exists(path)) { continue; }
                try
                {
                    if (new FileInfo(path).Length > MaximumBytes) { continue; }
                    var envelope = JsonConvert.DeserializeObject<Envelope>(File.ReadAllText(path), Settings);
                    if (envelope?.Owner == Owner && envelope.Content == _content && Valid(envelope.Checkpoint)) { return envelope.Checkpoint; }
                }
                catch (Exception error) when (error is IOException or JsonException or ArgumentException)
                { Debug.LogWarning($"[BattleCheckpoint] 저장 파일을 읽지 못했습니다: {error.GetType().Name}"); }
            }
            Clear();
            return null;
        }
        public void Save(BattleCheckpoint checkpoint)
        {
            string owner = Owner;
            if (owner == null || !Valid(checkpoint)) { return; }
            string path = _save.BattleCheckpointPath;
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Envelope { Owner = owner, Content = _content, Checkpoint = checkpoint }, Settings));
            if (bytes.Length > MaximumBytes) { throw new IOException("전투 저장 데이터가 허용 크기를 넘었습니다."); }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var file = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
            { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
            if (File.Exists(path)) { File.Replace(path + ".tmp", path, path + ".bak"); }
            else { File.Move(path + ".tmp", path); }
#if UNITY_IOS && !UNITY_EDITOR
            UnityEngine.iOS.Device.SetNoBackupFlag(path);
            if (File.Exists(path + ".bak")) { UnityEngine.iOS.Device.SetNoBackupFlag(path + ".bak"); }
#endif
        }
        public void Clear()
        {
            foreach (string suffix in new[] { "", ".tmp", ".bak" })
            {
                try { if (File.Exists(_save.BattleCheckpointPath + suffix)) { File.Delete(_save.BattleCheckpointPath + suffix); } }
                catch (IOException error) { Debug.LogWarning($"[BattleCheckpoint] 저장 파일 정리 실패: {error.Message}"); }
            }
        }
        private bool Valid(BattleCheckpoint c)
        {
            if (c?.Version != 1 || string.IsNullOrWhiteSpace(c.BattleId) || c.Stage == null || c.Spawns == null || c.Skills == null
                || !_data.Stages.Contains(c.StageId) || string.IsNullOrEmpty(c.RandomState)) { return false; }
            var stage = _data.Stages.GetOrThrow(c.StageId);
            var s = c.Stage;
            if (s.State is StageState.Starting or StageState.Finished || !Enum.IsDefined(typeof(StageState), s.State)
                || !Finite(s.Clock) || s.Clock < 0 || !Finite(s.Speed) || s.Speed <= 0 || s.WallHp < 0 || s.WallHp > s.WallMaxHp
                || s.WallMaxHp <= 0 || c.Spawns.WaveIndex < 0 || c.Spawns.WaveIndex >= stage.Waves.Count
                || s.WaveIndex != c.Spawns.WaveIndex || s.CompletedWaves < 0 || s.CompletedWaves > stage.Waves.Count
                || c.Spawns.Enemies == null || c.Spawns.Enemies.Count > 10000 || c.Spawns.Projectiles == null
                || c.Spawns.Enemies.Where(e => e != null).Select(e => e.Id).Distinct().Count() != c.Spawns.Enemies.Count
                || c.Skills.Count(skill => skill != null && !skill.Child) > SkillSlotLimit.Max) { return false; }
            if (s.DeadEnemyIds == null || s.Encountered == null || s.RewardedElites == null || s.SkillDamage == null
                || s.BuildLog == null || s.GeneralPicks == null || s.Choices == null || s.RouletteSlots == null
                || s.RouletteSelected == null || s.Waves == null || c.Spawns.Burst == null || c.Spawns.Pending == null
                || s.RouletteWinner >= s.RouletteSlots.Count || s.RouletteWinner < -1) { return false; }
            if (s.State == StageState.Submitting && (s.PendingRequest == null || s.PendingRequest.battleId != c.BattleId)) { return false; }
            if (c.Spawns.Enemies.Any(e => e == null || e.Passives == null || e.Id <= 0 || !_data.Monsters.Contains(e.MonsterId) || e.Hp <= 0 || e.Status == null || !Finite(e.Position.x) || !Finite(e.Position.y))) { return false; }
            if (c.Skills.Any(skill => skill == null || skill.Config?.Stats == null || skill.Config.Stats.Any(value => !Finite(value)) || skill.Attacks == null || skill.Attacks.Count > 10000)) { return false; }
            var battle = _save.Load().battles.Find(row => row.battleKey == c.BattleId);
            return battle != null && battle.stageId == c.StageId && (battle.status == "Issued" || s.State == StageState.Submitting);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class VectorConverter : JsonConverter
        {
            public override bool CanConvert(Type type) => type == typeof(Vector2) || type == typeof(Vector3);
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteStartArray();
                if (value is Vector2 v2) { writer.WriteValue(v2.x); writer.WriteValue(v2.y); }
                else if (value is Vector3 v3) { writer.WriteValue(v3.x); writer.WriteValue(v3.y); writer.WriteValue(v3.z); }
                writer.WriteEndArray();
            }
            public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
            {
                var values = JArray.Load(reader);
                if (type == typeof(Vector2) && values.Count == 2) { return new Vector2((float)values[0], (float)values[1]); }
                if (type == typeof(Vector3) && values.Count == 3) { return new Vector3((float)values[0], (float)values[1], (float)values[2]); }
                throw new JsonSerializationException("좌표 저장 형식이 다릅니다.");
            }
        }
    }
}
