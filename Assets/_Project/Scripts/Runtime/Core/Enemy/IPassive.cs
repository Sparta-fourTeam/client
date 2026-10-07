using Game.Core.Combat;

namespace Game.Core
{
    /// <summary>몬스터가 가진 패시브 동작. 필요한 훅만 구현한다</summary>
    public interface IPassive
    {
        /// <summary>몬스터가 만들어진 직후 한 번. 처음부터 켜져 있어야 하는 효과(시작 방어막 등)를 건다</summary>
        void OnSpawn(EnemyModel self) { }

        /// <summary>죽은 직후 한 번</summary>
        void OnDied(EnemyModel self) { }

        /// <summary>살아 있는 동안 매 프레임. 빙결·마비·기절 중에는 불리지 않는다</summary>
        void OnTick(EnemyModel self, float deltaTime) { }

        /// <summary>피해가 들어오기 전(속성·취약 계산보다 앞). 줄인 피해량을 돌려준다. 0 이하면 피해를 막은 것이다.
        /// 상태이상 지속 피해처럼 스킬 밖의 피해도 오므로 info.IsFromSkill로 걸러서 쓴다</summary>
        int OnBeforeDamage(EnemyModel self, DamageInfo info, int amount) => amount;

        /// <summary>피해를 받고 살아남은 뒤. appliedDamage는 실제로 깎인 체력이다</summary>
        void OnDamaged(EnemyModel self, DamageInfo info, int appliedDamage) { }
    }
}
