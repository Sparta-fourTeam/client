using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

namespace Game.Core
{
    /// <summary>몬스터 하나가 쓰는 에셋 묶음. 표의 키가 Monsters 테이블의 행 ID다</summary>
    [Serializable]
    public sealed class MonsterAssetEntry
    {
        [Tooltip("적 프리팹. Enemy 컴포넌트가 붙어 있어야 한다")] public Enemy prefab;

        [Tooltip("원거리 몬스터의 투사체 프리팹. 근접은 비워 둔다")] public EnemyProjectile projectilePrefab;
    }

    /// <summary>몬스터 ID로 프리팹과 투사체 프리팹을 찾는 클라이언트 전용 표. 서버는 ID와 수치만 관리하고 어떤 에셋을 쓰는지는 모른다.
    /// 새 몬스터를 추가하면 이 표에 항목 하나만 더하면 된다. 등급(보스·엘리트)과 공격 방식(근접·원거리)은 Monsters 행에서 정해지므로 여기에 적지 않는다</summary>
    [CreateAssetMenu(fileName = "MonsterAssetTable", menuName = "Project Nova/Monster Asset Table")]
    public sealed class MonsterAssetTable : ScriptableObject
    {
        [SerializeField, SerializedDictionary("MonsterId", "에셋")]
        private SerializedDictionary<int, MonsterAssetEntry> entries = new SerializedDictionary<int, MonsterAssetEntry>();

        public IReadOnlyDictionary<int, MonsterAssetEntry> Entries => entries;

        /// <summary>코드(테스트, 도구)에서 표를 만들 때 쓴다. 에셋 표는 인스펙터에서 채운다</summary>
        public static MonsterAssetTable Create(IEnumerable<KeyValuePair<int, MonsterAssetEntry>> entries)
        {
            var table = CreateInstance<MonsterAssetTable>();
            foreach (var pair in entries)
            {
                table.entries[pair.Key] = pair.Value;
            }

            return table;
        }

        public bool TryGet(int monsterId, out MonsterAssetEntry entry) => entries.TryGetValue(monsterId, out entry);

        public MonsterAssetEntry GetOrThrow(int monsterId) =>
            TryGet(monsterId, out var entry)
                ? entry
                : throw new InvalidOperationException($"MonsterId {monsterId}에 해당하는 항목이 MonsterAssetTable에 없습니다.");
    }
}
