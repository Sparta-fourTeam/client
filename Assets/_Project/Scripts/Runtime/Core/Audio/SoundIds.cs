namespace Game.Core
{
    /// <summary>AudioLibrary에 등록하는 소리 ID. 코드에서 부르는 이름과 라이브러리 항목의 id를 맞춘다</summary>
    public static class SoundIds
    {
        public const string BattleBgm = "bgm_battle";

        public const string WaveStart = "sfx_wave_start";
        public const string EnemyDie = "sfx_enemy_die";
        public const string EliteDie = "sfx_elite_die";
        public const string BossDie = "sfx_boss_die";
        public const string SpecialWarning = "sfx_special_warning";
        public const string WallHit = "sfx_wall_hit";
        public const string WallDestroyed = "sfx_wall_destroyed";
        public const string StageClear = "sfx_stage_clear";
        public const string StageFail = "sfx_stage_fail";
    }
}
