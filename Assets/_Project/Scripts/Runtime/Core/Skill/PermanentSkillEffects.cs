using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>스킬 영구 강화가 전투에 주는 효과. progressionId(= 스킬 강화 UpgradeId)의 현재 강화 레벨만큼 쌓인 효과를 돌려준다</summary>
    public interface IPermanentSkillEffects
    {
        /// <summary>효과 목록. 강화 데이터가 없거나 레벨이 0이면 빈 목록이다</summary>
        IReadOnlyList<EffectDef> For(string progressionId);
    }

    /// <summary>Upgrades 테이블의 스킬 강화 행과 PlayerProfile의 강화 레벨로 효과를 만든다</summary>
    public sealed class ProfilePermanentSkillEffects : IPermanentSkillEffects
    {
        private readonly PlayerProfile profile;
        private readonly GameDataStore data;

        public ProfilePermanentSkillEffects(PlayerProfile profile, GameDataStore data)
        {
            this.profile = profile;
            this.data = data;
        }

        public IReadOnlyList<EffectDef> For(string progressionId)
        {
            if (string.IsNullOrEmpty(progressionId) || !data.Upgrades.Contains(progressionId)) { return Array.Empty<EffectDef>(); }
            return PermanentSkillEffect.At(data.Upgrades.GetOrThrow(progressionId), profile.UpgradeLevel(progressionId));
        }
    }

    /// <summary>스킬 강화 행의 Effects를 카드와 같은 효과(<see cref="EffectRegistry"/>의 키)로 바꿔 스킬의 기본 설정에 적용한다.
    /// 전투(SkillController)와 로비 화면(스킬 강화 팝업)이 같은 계산을 쓰므로 화면 수치와 전투 수치가 같다.
    /// TODO(data): #142 수치가 확정되면 Upgrades.json의 스킬 행 Effects만 바꾼다 (지금은 레벨당 damage +8% 임시값)</summary>
    public static class PermanentSkillEffect
    {
        /// <summary>level의 효과. 효과량은 ValuePerLevel × level로 단순 합산한다 (레벨 0 이하면 빈 목록)</summary>
        public static List<EffectDef> At(UpgradeDefinition def, int level)
        {
            var effects = new List<EffectDef>();
            if (level <= 0) { return effects; }
            foreach (var effect in def.Effects)
            {
                effects.Add(new EffectDef { kind = effect.Kind, value = effect.ValuePerLevel * level });
            }

            return effects;
        }

        /// <summary>스킬의 기본 설정에 효과를 적용한 설정. 판 안의 카드 강화는 이 설정 위에 쌓인다.
        /// 효과를 적용하지 못하면(데이터 검증을 통과했다면 오지 않는다) 기본 설정을 돌려준다</summary>
        public static SkillConfig Apply(SkillData data, IReadOnlyList<EffectDef> effects)
        {
            var config = SkillConfig.FromDefinition(data);
            if (effects == null || effects.Count == 0) { return config; }
            var builder = new SkillConfigBuilder(config);
            return builder.TryApplyCatalog(effects) ? builder.Build() : config;
        }

        /// <summary>스킬 강화 행(progressionId가 이 UpgradeId인 스킬이 있는 행)의 효과가 그 스킬에 적용될 수 있는지 부팅 때 확인한다.
        /// 등록되지 않은 키, 숫자 스탯이 아닌 키, 그 스킬의 공격 종류가 쓰지 않는 키(조용히 무시된다), 최대 레벨 값이 규칙에 맞지 않는 경우를 거부한다.
        /// 장비 같은 나머지 행은 효과 키가 따로라 검사하지 않는다</summary>
        public static void Validate(UpgradeDefinition def, IEnumerable<SkillData> skills)
        {
            foreach (var skill in skills)
            {
                if (skill.progressionId != def.UpgradeId) { continue; }
                foreach (var effect in def.Effects)
                {
                    if (!EffectRegistry.IsStatKind(effect.Kind))
                    {
                        throw new InvalidOperationException($"Upgrades {def.UpgradeId}: 스킬 강화 효과 {effect.Kind}은(는) EffectRegistry의 숫자 효과 키가 아닙니다");
                    }

                    if (!EffectRegistry.Supports(skill.castType, effect.Kind))
                    {
                        throw new InvalidOperationException($"Upgrades {def.UpgradeId}: 스킬 {skill.id}({skill.castType})은(는) 효과 {effect.Kind}을(를) 쓰지 않습니다");
                    }
                }

                var builder = new SkillConfigBuilder(SkillConfig.FromDefinition(skill));
                if (!builder.TryApplyCatalog(At(def, def.MaxLevel)))
                {
                    throw new InvalidOperationException($"Upgrades {def.UpgradeId}: 최대 레벨({def.MaxLevel}) 효과를 스킬 {skill.id}에 적용할 수 없습니다 (값 규칙 확인)");
                }
            }
        }
    }
}
