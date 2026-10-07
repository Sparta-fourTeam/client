using System;

namespace Game.Core.Combat
{
    /// <summary>적이 받은 피해가 어느 스킬에서 나왔는지 기록하기 위한 "현재 시전 중인 스킬" 표시.
    /// 시전기(SkillCaster)가 가장 바깥 스킬의 ID로 범위를 열고, 투사체·번개·장판 같은 피해 객체는 만들어질 때 그 값을 기억했다가
    /// 피해를 줄 때 다시 열어 준다. 그래서 자식 스킬(얼음 조각 등)의 피해도 그 자식을 시전한 부모 스킬로 모인다.
    /// 스킬 밖에서 나온 피해(상태 이상 지속 피해, 적의 공격 등)는 ID가 0이라 기록하지 않는다.
    /// 전투는 한 스레드에서만 돌아가므로 정적 상태를 쓴다.</summary>
    public static class DamageAttribution
    {
        private static DamageSource current;

        /// <summary>지금 시전 중인 스킬의 ID. 스킬 밖이면 0</summary>
        public static int Current => current.SkillId;

        /// <summary>지금 시전 중인 스킬의 ID, 속성, 시전 형태. 스킬 밖이면 SkillId가 0이다</summary>
        public static DamageSource CurrentSource => current;

        /// <summary>적이 스킬에서 나온 피해를 실제로 받았을 때 (스킬 ID, 실제로 깎인 체력)</summary>
        public static event Action<int, int> Dealt;

        /// <summary>skillId로 범위를 연다. 범위가 끝나면(Dispose) 이전 값으로 돌아간다. 0이면 "스킬 밖"으로 연다</summary>
        public static Scope Begin(int skillId) => Begin(new DamageSource(skillId));

        /// <summary>source로 범위를 연다. 피해 객체가 만들어질 때 기억해 둔 값을 다시 열 때 쓴다</summary>
        public static Scope Begin(DamageSource source)
        {
            var scope = new Scope(current);
            current = source;
            return scope;
        }

        /// <summary>이미 범위가 열려 있으면 그대로 두고(가장 바깥 스킬이 이긴다), 아니면 skillId로 연다</summary>
        public static Scope BeginOutermost(int skillId) => BeginOutermost(new DamageSource(skillId));

        /// <summary>이미 범위가 열려 있으면 그대로 두고(가장 바깥 스킬이 이긴다), 아니면 source로 연다. 자식 스킬은 부모의 속성과 시전 형태를 이어받는다</summary>
        public static Scope BeginOutermost(DamageSource source)
        {
            var scope = new Scope(current);
            if (!current.IsSkill) { current = source; }
            return scope;
        }

        /// <summary>실제로 깎인 체력을 현재 스킬 몫으로 알린다. 스킬 밖이거나 깎인 양이 없으면 아무것도 하지 않는다</summary>
        public static void Report(int actualDamage)
        {
            if (current.IsSkill && actualDamage > 0) { Dealt?.Invoke(current.SkillId, actualDamage); }
        }

        public readonly struct Scope : IDisposable
        {
            private readonly DamageSource previous;
            internal Scope(DamageSource previous) => this.previous = previous;
            public void Dispose() => current = previous;
        }
    }
}
