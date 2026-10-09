using Game.Core;

namespace Game.Sandbox
{
    /// <summary>모든 스킬에 같은 영구 레벨을 돌려준다. 영구 레벨에 따라 바뀌는 카드 변형((+) 명칭, 해금 조건)을 슬라이더로 시험하려고 progressionId와 무관하게 적용한다.</summary>
    public sealed class SandboxProgression : IWeaponProgression
    {
        public int Level { get; set; }

        public int GetLevel(string progressionId) => Level;
    }

    /// <summary>샌드박스 슬라이더의 영구 레벨을 실제 강화 테이블 효과로 변환한다.</summary>
    public sealed class SandboxPermanentSkillEffects : IPermanentSkillEffects
    {
        private readonly GameDataStore data;
        private readonly SandboxProgression progression;

        public SandboxPermanentSkillEffects(GameDataStore data, SandboxProgression progression)
        {
            this.data = data;
            this.progression = progression;
        }

        public System.Collections.Generic.IReadOnlyList<EffectDef> For(string progressionId)
        {
            if (string.IsNullOrEmpty(progressionId) || !data.Upgrades.Contains(progressionId))
            { return System.Array.Empty<EffectDef>(); }
            return PermanentSkillEffect.At(data.Upgrades.GetOrThrow(progressionId), progression.Level);
        }
    }

    /// <summary>샌드박스는 모든 스킬을 시험하는 곳이라 해금 레벨과 상관없이 전부 열린 것으로 본다. 스테이지 씬은 플레이어 레벨(ProfileSkillUnlock)을 따른다.</summary>
    public sealed class AllSkillsUnlocked : ISkillUnlock
    {
        public bool IsUnlocked(SkillData skill) => true;
    }

    /// <summary>샌드박스는 빈 상태에서 시작한다. 시험할 스킬은 패널에서 고른다.</summary>
    public sealed class NoStartingSkills : IStartingSkills
    {
        public System.Collections.Generic.IReadOnlyList<int> GetSkillIds() => System.Array.Empty<int>();
    }
}
