namespace Game.Core
{
    /// <summary>AudioLibrary에 등록하는 소리 ID. 코드에서 부르는 이름과 라이브러리 항목의 id를 맞춘다</summary>
    public static class SoundIds
    {
        public const string BattleBgm = "bgm_battle";
        public const string IntroBgm = "bgm_intro";
        public const string LobbyBgm = "bgm_lobby";
        public const string UiClick = "sfx_ui_click";
        public const string UiLocked = "sfx_ui_locked";
        public const string Upgrade = "sfx_upgrade_success";
        public const string LevelUp = "sfx_level_up";
        public const string RouletteSpin = "sfx_roulette_spin";
        public const string CardShow = "sfx_card_show";

        // 보조 공격도 같은 계열 ID를 공유해 다중 타격의 재생 간격 제한을 따른다.
        public static string SkillCast(int skillId) => skillId switch
        {
            1 or 10 or 17 => "sfx_arrow_cast",
            2 or 11 => "sfx_fireball_cast",
            3 or 12 => "sfx_lightning_cast",
            4 or 8 or 9 => "sfx_ice_cast",
            5 => "sfx_vine_cast",
            6 => "sfx_cold_zone_cast",
            7 => "sfx_cloud_cast",
            13 or 14 => "sfx_beam_cast",
            15 => "sfx_chain_cast",
            16 => "sfx_focus_cast",
            _ => null,
        };

        public static string SkillHit(Combat.DamageInfo damage) =>
            damage.Amount > 0 && damage.IsFromSkill ? SkillHit(damage.SkillId) : null;

        public static string SkillHit(int skillId) => skillId switch
        {
            1 or 10 or 17 => "sfx_arrow_hit",
            2 or 11 => "sfx_fireball_hit",
            3 or 12 => "sfx_lightning_hit",
            4 or 8 or 9 => "sfx_ice_hit",
            5 => "sfx_vine_hit",
            6 => "sfx_cold_zone_hit",
            7 => "sfx_cloud_hit",
            13 or 14 => "sfx_beam_hit",
            15 => "sfx_chain_hit",
            16 => "sfx_focus_hit",
            _ => null,
        };

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
