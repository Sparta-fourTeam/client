using System;
using UnityEngine;

namespace Game.View
{
    /// <summary>몬스터의 표시용 정보(이름·아이콘). 서버 테이블이 아니라 클라이언트에만 두는 데이터라 서버는 몬스터 ID와 수치만 관리한다.
    /// 스테이지에 어떤 몬스터가 나오는지는 밸런스라 서버 Stages 테이블의 웨이브 구성(Waves[].Spawns)이 정한다. 일시정지 창의 "등장 요마"와 로비 도감이 같이 쓴다.
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

        [SerializeField] private MonsterEntry[] _monsters = Array.Empty<MonsterEntry>();

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

        /// <summary>코드로 표를 채운다. 에디터 도구와 테스트에서 쓴다</summary>
        public void Configure(MonsterEntry[] monsters)
        {
            _monsters = monsters;
        }
    }
}
