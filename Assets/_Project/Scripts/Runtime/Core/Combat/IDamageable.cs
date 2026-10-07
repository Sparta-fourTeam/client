namespace Game.Core.Combat
{
    public interface IDamageable
    {
        void TakeDamage(int amount);

        /// <summary>속성·시전 형태를 가진 피해. 이를 쓰지 않는 대상은 피해량만 받는다</summary>
        void TakeDamage(DamageInfo info) => TakeDamage(info.Amount);
    }
}
