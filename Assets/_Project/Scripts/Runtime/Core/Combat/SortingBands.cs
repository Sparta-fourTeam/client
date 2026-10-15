namespace Game.Core
{
    /// <summary>Default 정렬 레이어 안에서 전투 월드 오브젝트가 쓰는 sortingOrder 구간.
    /// 몬스터 몸(EnemyBack ~ EnemyFront)은 EnemyDepthSorter가 매 프레임 다시 쓰므로 다른 오브젝트가 이 구간을 쓰면 안 된다.
    /// 구간표는 docs/enemy-sorting.md에 있다</summary>
    public static class SortingBands
    {
        public const int Background = -500;

        /// <summary>바닥 장판·바닥 연출·나무뿌리. 몬스터 뒤, 배경 앞</summary>
        public const int GroundMin = -450;
        public const int GroundMax = -410;

        /// <summary>몬스터 몸. 사망 이펙트(몸 - 1)가 EnemyBack - 1까지 내려가므로 GroundMax와 간격을 둔다</summary>
        public const int EnemyBack = -402;
        public const int EnemyFront = -10;

        /// <summary>벽, 스킬·임팩트, 플레이어는 0 이상</summary>
        public const int World = 0;

        public static bool IsGround(int order) => order >= GroundMin && order <= GroundMax;

        public static bool IsEnemyReserved(int order) => order >= EnemyBack && order <= EnemyFront;
    }
}
