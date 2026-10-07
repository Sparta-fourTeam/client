using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    /// <summary>몬스터의 표시용 정보(이름·아이콘)와 스테이지별 등장 목록. 서버 테이블이 아니라 클라이언트에만 두는 데이터라
    /// 서버는 몬스터 ID와 수치만 관리한다. 일시정지 창의 "등장 요마"와 로비 도감이 같이 쓴다.
    /// 이름은 아직 고정 텍스트다. 로컬라이징이 들어오면 로컬라이징 키로 바꾸고 조회만 바꾸면 된다</summary>
    [CreateAssetMenu(fileName = "MonsterDisplayTable", menuName = "Project Nova/Monster Display Table")]
    public sealed class MonsterDisplayTable : ScriptableObject
    {
        [Serializable]
        public sealed class MonsterEntry
        {
            [Tooltip("Monsters 테이블의 행 ID")] public int monsterId;
            public string displayName;
            public Sprite icon;
        }

        [Serializable]
        public sealed class StageEntry
        {
            public int stageId;
            [Tooltip("이 스테이지에 등장하는 몬스터 ID. 이 순서대로 보여준다")] public int[] monsterIds = Array.Empty<int>();
        }

        [SerializeField] private MonsterEntry[] _monsters = Array.Empty<MonsterEntry>();
        [SerializeField] private StageEntry[] _stages = Array.Empty<StageEntry>();

        public bool TryGetMonster(int monsterId, out MonsterEntry entry)
        {
            foreach (var monster in _monsters)
            {
                if (monster.monsterId == monsterId)
                {
                    entry = monster;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        /// <summary>stageId 스테이지의 등장 몬스터 ID. 등록되지 않은 스테이지는 빈 목록이다</summary>
        public IReadOnlyList<int> StageMonsterIds(int stageId)
        {
            foreach (var stage in _stages)
            {
                if (stage.stageId == stageId)
                {
                    return stage.monsterIds;
                }
            }

            return Array.Empty<int>();
        }

        /// <summary>코드로 표를 채운다. 에디터 도구와 테스트에서 쓴다</summary>
        public void Configure(MonsterEntry[] monsters, StageEntry[] stages)
        {
            _monsters = monsters;
            _stages = stages;
        }
    }
}
