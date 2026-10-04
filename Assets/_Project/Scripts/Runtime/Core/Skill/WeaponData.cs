using System.Collections.Generic;

namespace Game.Core
{
    public enum CastType { Projectile, Hitscan }
    public enum WeaponForm { Default, Enbakutsu }

    public class WeaponData
    {
        public int id;
        public string name;
        public string desc;

        /// <summary>HUD가 아이콘을 찾는 키 (SkillIconTable). 컨벤션상 아이콘은 테이블의 IconKey로 찾는다</summary>
        public string iconKey;
        public CastType castType;
        public WeaponBaseStats baseStats;
        public List<WeaponUpgradeOption> upgrades;
        // 최초 습득을 제외한 전투 중 성공한 강화 횟수 상한.
        public int maxLevel;
        // PlayerProfile의 영구 성장 ID. 미매핑 인술은 null이며 영구 레벨 0으로 처리.
        public string progressionId;
        // 새 인술의 기본 수치 자료가 없을 때 사용한 기존 프로토타입 ID.
        public int prototypeBalanceSourceId;

    }

    public class WeaponBaseStats
    {
        public float cooldown;
        public float baseDamage;
        public float range;
        public float speed;
        // Legacy hitCount is the number of projectiles/targets per cast, not penetration.
        public int hitCount;
        public int castCount = 1;
        public int pierceCount;
        public float castInterval = 0.1f;
        public float freezeDuration;
        public float paralysisDuration;
        public float paralysisChance = 1;
        public float freezeChance = 1;
        public float frostbiteChance = 1;
        public float burnChance = 1;
        public float knockbackDistance;
        public float explosionRadius;
        public float explosionDamageRatio;
    }

    // 기존 0..3 값은 Cards.json과의 호환성을 위해 순서를 유지한다.
    public enum UpgradeType { AttackSpeed, Damage, ProjectileCount, HitCount, CastCount, PierceCount, ProjectileSpeed, FreezeDuration, SplitCount, ShardDamage, Knockback, Frostbite, ShardFrostbite, ExplosionDamage, ExplosionRadius, EnableExplosion, Paralysis, LightningStrike, AuxiliaryLightning, AuxiliaryExplosion, BurnDuration, BurnRatio, BurnMaxHp, BurnDeathExplosion, ImpactDamage, Form, ParalysisDuration, AuxiliaryParalysis }

    public class StatEffect
    {
        public UpgradeType type;
        public float value;
    }

    public class WeaponUpgradeOption
    {
        public string id;
        public string name;
        public string desc;
        public List<StatEffect> effects;
        // 같은 카드 ID/횟수를 유지하고 영구 레벨에 따라 표시와 효과 전체를 교체한다.
        public WeaponUpgradeVariant[] variants;
        // 양쪽 카탈로그에서 같은 선택 횟수를 참조하는 공유 강화 키.
        public string sharedId;
        public int[] affectedWeaponIds;
        /// <summary>최대 등장 횟수. 선택 성공 시에만 사용하며, 미선택 제시는 차감하지 않는다.</summary>
        public int maxPickCount = 1;
        public string[] requiredCardIds;
        public int[] requiredWeaponIds;

        // 전투 중 레벨만 의미한다. 로비의 영구 인술 레벨과 구분한다.
        public int minBattleLevel = 1;
        public int minPermanentLevel;
        public CardCountRequirement[] requiredCardCounts;
        public CardExclusion[] exclusions;
        public bool enabled = true;
        public string disabledReason;
    }

    public class CardCountRequirement
    {
        // 0이면 이 강화가 속한 무기. 다른 무기의 강화도 참조할 수 있다.
        public int weaponId;
        public string cardId;
        public int count = 1;
    }

    public class WeaponUpgradeVariant
    {
        public int minPermanentLevel;
        public string name;
        public string desc;
        public List<StatEffect> effects;
    }

    public class CardExclusion
    {
        public int weaponId;
        public string cardId;
        // 0이면 항상 적용. 양수이면 소속 인술의 영구 성장 레벨이 이 값 미만일 때만 적용.
        public int belowPermanentLevel;
    }
}
