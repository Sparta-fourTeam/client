using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Profiling;
using UnityEngine;
using VContainer;

namespace Game.Network
{
    /// <summary>기기 전용 전투 저장. 이전 완성본을 남기고 파일을 교체하며 계정·테이블·전투 발급 기록을 확인한다.</summary>
    public sealed class LocalBattleCheckpointStore : IBattleCheckpointStore, IDisposable
    {
        private static readonly ProfilerMarker WaitMarker = new("BattleCheckpoint.WaitForSave");
        private static readonly ProfilerMarker StoreMarker = new("BattleCheckpoint.Store");
        private static readonly ProfilerMarker ValidateMarker = new("BattleCheckpoint.Validate");
        private static readonly ProfilerMarker SerializeMarker = new("BattleCheckpoint.Serialize");
        private static readonly ProfilerMarker WriteMarker = new("BattleCheckpoint.Write");
        private static readonly ProfilerMarker FlushMarker = new("BattleCheckpoint.Flush");
        private static readonly ProfilerMarker ReplaceMarker = new("BattleCheckpoint.Replace");
        private readonly object _queueGate = new(), _fileGate = new();
        private WriteRequest _pending;
        private bool _writing, _disposed;
        private long _generation;
        private Exception _writeError;
        private string _authorizedOwner, _authorizedBattle;
#if UNITY_IOS && !UNITY_EDITOR
        private string _preparedBackupPath;
#endif
        private int _authorizedStage;
        private bool _battleMatches, _battleIssued;
        private sealed class WriteRequest
        {
            public string Path;
            public long Generation;
            public Envelope Envelope;
            public GameDataStore Data;
        }
        // 한 워커만 사용한다. 큰 JSON 문자열과 UTF-8 배열을 매번 새로 만들지 않는다.
        private readonly JsonSerializer _writerSerializer;
        private MemoryStream _jsonBuffer;
        private StreamWriter _jsonText;
        private readonly HashSet<int> _validationEnemyIds = new();
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
            _content = Content(data);
            _writerSerializer = JsonSerializer.Create(Settings);
            _writerSerializer.Converters.Add(new FloatArrayConverter());
        }
        private string Content(GameDataStore data) => ReferenceEquals(data, _data) && _content != null
            ? _content : Hash(Application.version + string.Concat(data.Revisions.Keys.OrderBy(n => n).Select(data.RawJson)));
        private string Owner => _credentials.HasCredential
            ? Hash($"{_credentials.Method}|{_credentials.AccountId}|{_profile.Nickname}") : null;
        private static string Hash(string value)
        {
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }
        public BattleCheckpoint Load()
        {
            Flush();
            if (Owner == null) { return null; }
#if UNITY_IOS && !UNITY_EDITOR
            PrepareIosStorage(_save.BattleCheckpointPath);
#endif
            foreach (string path in new[] { _save.BattleCheckpointPath, _save.BattleCheckpointPath + ".bak" })
            {
                if (!File.Exists(path)) { continue; }
                try
                {
                    if (new FileInfo(path).Length > MaximumBytes) { continue; }
                    var envelope = JsonConvert.DeserializeObject<Envelope>(File.ReadAllText(path), Settings);
                    if (envelope?.Owner == Owner && envelope.Checkpoint != null)
                    {
                        var data = envelope.Checkpoint.RestoreBattleData(_data);
                        if (envelope.Content == Content(data) && Valid(envelope.Checkpoint, data)) { return envelope.Checkpoint; }
                    }
                }
                catch (Exception error) when (error is IOException or JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException)
                { Debug.LogWarning($"[BattleCheckpoint] 저장 파일을 읽지 못했습니다: {error.GetType().Name}"); }
            }
            Clear();
            return null;
        }
        public void Save(BattleCheckpoint checkpoint)
        {
            QueueSave(checkpoint);
            Flush();
        }

        // Unity API·계정 상태·저장 경로는 메인 스레드에서 읽는다.
        public void QueueSave(BattleCheckpoint checkpoint)
        {
            ReportWriteError();
            if (checkpoint == null) { return; }
            lock (_queueGate) { if (_disposed) { return; } }
            string owner = Owner;
            if (owner == null) { return; }
            using (ValidateMarker.Auto())
            {
                // 매초 계정 파일을 다시 읽지 않는다. 결과 반영·삭제·새 전투 발급은 Clear에서 무효화한다.
                if (_authorizedOwner != owner || _authorizedBattle != checkpoint.BattleId || _authorizedStage != checkpoint.StageId)
                {
                    var battle = _save.Load().battles.Find(row => row.battleKey == checkpoint.BattleId);
                    _authorizedOwner = owner; _authorizedBattle = checkpoint.BattleId; _authorizedStage = checkpoint.StageId;
                    _battleMatches = battle != null && battle.stageId == checkpoint.StageId;
                    _battleIssued = battle?.status == "Issued";
                }
                if (!_battleMatches || (!_battleIssued && checkpoint.Stage?.State != StageState.Submitting)) { return; }
            }
            checkpoint.PinBattleData(_data);
            var pinnedData = checkpoint.RestoreBattleData(_data);
            string path = _save.BattleCheckpointPath;
#if UNITY_IOS && !UNITY_EDITOR
            PrepareIosStorage(path);
#endif
            bool start;
            lock (_queueGate)
            {
                if (_disposed) { return; }
                _pending = new WriteRequest
                {
                    Path = path,
                    Generation = _generation,
                    Data = pinnedData,
                    Envelope = new Envelope { Owner = owner, Content = Content(pinnedData), Checkpoint = checkpoint }
                };
                start = !_writing;
                if (start) { _writing = true; }
            }
            if (!start) { return; }
#if UNITY_WEBGL && !UNITY_EDITOR
            // 스레드를 지원하지 않는 WebGL은 동기 경로를 사용한다.
            DrainWrites();
#else
            _ = Task.Run(DrainWrites);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        private void PrepareIosStorage(string path)
        {
            if (_preparedBackupPath == path) { return; }
            // 파일이 만들어지기 전 폴더 전체를 제외한다. 이전 버전의 이어하기 기록도 보존한다.
            string folder = Path.GetDirectoryName(path);
            Directory.CreateDirectory(folder);
            UnityEngine.iOS.Device.SetNoBackupFlag(folder);
            foreach (string suffix in new[] { "", ".bak" })
            {
                string legacy = _save.LegacyBattleCheckpointPath + suffix;
                if (File.Exists(legacy) && !File.Exists(path + suffix)) { File.Move(legacy, path + suffix); }
            }
            _preparedBackupPath = path;
        }
#endif

        private void DrainWrites()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            UnityEngine.Profiling.Profiler.BeginThreadProfiling("BattleCheckpoint", "Writer");
#endif
            try { DrainQueue(); }
            finally
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                UnityEngine.Profiling.Profiler.EndThreadProfiling();
#endif
            }
        }

        private void DrainQueue()
        {
            while (true)
            {
                WriteRequest request;
                lock (_queueGate)
                {
                    request = _pending; _pending = null;
                    if (request == null)
                    {
                        _writing = false;
                        Monitor.PulseAll(_queueGate);
                        return;
                    }
                }
                try
                {
                    using var storeSample = StoreMarker.Auto();
                    using (ValidateMarker.Auto()) { if (!Valid(request.Envelope.Checkpoint, request.Data, checkBattle: false)) { continue; } }
                    ArraySegment<byte> bytes;
                    using (SerializeMarker.Auto()) { bytes = Serialize(request.Envelope); }
                    lock (_fileGate)
                    {
                        // Clear 후 끝난 이전 세대의 직렬화가 파일을 되살리지 못하게 한다.
                        lock (_queueGate) { if (request.Generation != _generation) { continue; } }
                        WriteFile(request.Path, bytes);
                    }
                }
                catch (Exception error)
                {
                    // Unity 콘솔 접근도 메인 스레드의 다음 요청 또는 Flush에서 한다.
                    lock (_queueGate) { _writeError = error; }
                    // 크기 제한을 넘긴 실패 요청 때문에 큰 버퍼를 영구 보관하지 않는다.
                    if (_jsonBuffer?.Capacity > MaximumBytes) { ReleaseJsonBuffer(); }
                }
            }
        }

        private ArraySegment<byte> Serialize(Envelope envelope)
        {
            if (_jsonBuffer == null)
            {
                _jsonBuffer = new MemoryStream(16 * 1024);
                _jsonText = new StreamWriter(_jsonBuffer, new UTF8Encoding(false), 4096, leaveOpen: true);
            }
            // 이전 직렬화가 중간 실패했어도 남은 인코더/문자 버퍼를 비운 다음 처음부터 쓴다.
            _jsonText.Flush();
            _jsonBuffer.Position = 0;
            _jsonBuffer.SetLength(0);
            using (var json = new JsonTextWriter(_jsonText) { CloseOutput = false, ArrayPool = JsonCharPool.Instance })
            {
                _writerSerializer.Serialize(json, envelope);
                json.Flush();
            }
            _jsonText.Flush();
            if (_jsonBuffer.Length > MaximumBytes) { throw new IOException("전투 저장 데이터가 허용 크기를 넘었습니다."); }
            return new ArraySegment<byte>(_jsonBuffer.GetBuffer(), 0, (int)_jsonBuffer.Length);
        }

        private void ReleaseJsonBuffer()
        {
            _jsonText?.Dispose(); _jsonBuffer?.Dispose();
            _jsonText = null; _jsonBuffer = null;
        }

        private sealed class JsonCharPool : IArrayPool<char>
        {
            internal static readonly JsonCharPool Instance = new();
            public char[] Rent(int minimumLength) => ArrayPool<char>.Shared.Rent(minimumLength);
            public void Return(char[] array) => ArrayPool<char>.Shared.Return(array);
        }

        // 스탯 배열을 비제네릭 열거하면서 float마다 boxing하는 기본 경로를 피한다.
        private sealed class FloatArrayConverter : JsonConverter
        {
            public override bool CanConvert(Type type) => type == typeof(float[]);
            public override bool CanRead => false;
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteStartArray();
                foreach (float number in (float[])value) { writer.WriteValue(number); }
                writer.WriteEndArray();
            }
            public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
                => throw new NotSupportedException();
        }

        private static void WriteFile(string path, ArraySegment<byte> bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var file = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1))
            {
                using (WriteMarker.Auto()) { file.Write(bytes.Array, bytes.Offset, bytes.Count); }
                using (FlushMarker.Auto()) { file.Flush(true); }
            }
            using (ReplaceMarker.Auto())
            {
                if (File.Exists(path)) { File.Replace(path + ".tmp", path, path + ".bak"); }
                else { File.Move(path + ".tmp", path); }
            }
        }

        public void Flush()
        {
            using var waitSample = WaitMarker.Auto();
            lock (_queueGate) { while (_writing) { Monitor.Wait(_queueGate); } }
            ReportWriteError();
        }

        private void ReportWriteError()
        {
            Exception error;
            lock (_queueGate) { error = _writeError; _writeError = null; }
            if (error != null) { Debug.LogWarning($"[BattleCheckpoint] 전투 저장 실패: {error.Message}"); }
        }

        public void Clear()
        {
            string path = _save.BattleCheckpointPath;
            lock (_queueGate)
            {
                _generation++; _pending = null;
                _authorizedOwner = _authorizedBattle = null;
            }
            // 진행 중인 파일 교체가 끝난 뒤 삭제한다. 직렬화 완료까지 기다릴 필요는 없다.
            lock (_fileGate)
            {
                foreach (string suffix in new[] { "", ".tmp", ".bak" })
                {
                    try
                    {
                        if (File.Exists(path + suffix)) { File.Delete(path + suffix); }
#if UNITY_IOS && !UNITY_EDITOR
                        string legacy = _save.LegacyBattleCheckpointPath + suffix;
                        if (File.Exists(legacy)) { File.Delete(legacy); }
#endif
                    }
                    catch (IOException error) { Debug.LogWarning($"[BattleCheckpoint] 저장 파일 정리 실패: {error.Message}"); }
                }
            }
        }

        public void Dispose()
        {
            lock (_queueGate)
            {
                if (_disposed) { return; }
                _disposed = true;
            }
            Flush();
            ReleaseJsonBuffer();
        }
        private bool Valid(BattleCheckpoint c, GameDataStore data, bool checkBattle = true)
        {
            if (c?.Version != 1 || string.IsNullOrWhiteSpace(c.BattleId) || c.Stage == null || c.Spawns == null || c.Skills == null
                || !data.Stages.Contains(c.StageId) || string.IsNullOrEmpty(c.RandomState)) { return false; }
            var stage = data.Stages.GetOrThrow(c.StageId);
            var s = c.Stage;
            if (s.State is StageState.Starting or StageState.Finished || !Enum.IsDefined(typeof(StageState), s.State)
                || !Finite(s.Clock) || s.Clock < 0 || !Finite(s.Speed) || s.Speed <= 0 || s.WallHp < 0 || s.WallHp > s.WallMaxHp
                || s.WallMaxHp <= 0 || c.Spawns.WaveIndex < 0 || c.Spawns.WaveIndex >= stage.Waves.Count
                || s.WaveIndex != c.Spawns.WaveIndex || s.CompletedWaves < 0 || s.CompletedWaves > stage.Waves.Count
                || c.Spawns.Enemies == null || c.Spawns.Enemies.Count > 10000 || c.Spawns.Projectiles == null) { return false; }
            if (s.DeadEnemyIds == null || s.Encountered == null || s.RewardedElites == null || s.SkillDamage == null
                || s.BuildLog == null || s.GeneralPicks == null || s.Choices == null || s.RouletteSlots == null
                || s.RouletteSelected == null || s.Waves == null || c.Spawns.Burst == null || c.Spawns.Pending == null
                || s.RouletteWinner >= s.RouletteSlots.Count || s.RouletteWinner < -1) { return false; }
            if (s.State == StageState.Submitting && (s.PendingRequest == null || s.PendingRequest.battleId != c.BattleId)) { return false; }
            _validationEnemyIds.Clear();
            foreach (var enemy in c.Spawns.Enemies)
            {
                if (enemy == null || enemy.Passives == null || enemy.Id <= 0 || !_validationEnemyIds.Add(enemy.Id)
                    || !data.Monsters.Contains(enemy.MonsterId) || enemy.Hp <= 0 || enemy.Status == null
                    || !Finite(enemy.Position.x) || !Finite(enemy.Position.y)) { return false; }
            }
            int owned = 0;
            foreach (var skill in c.Skills)
            {
                if (skill == null || skill.Config?.Stats == null || skill.Attacks == null || skill.Attacks.Count > 10000) { return false; }
                if (!skill.Child && ++owned > SkillSlotLimit.Max) { return false; }
                foreach (float value in skill.Config.Stats) { if (!Finite(value)) { return false; } }
            }
            if (!checkBattle) { return true; }
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
