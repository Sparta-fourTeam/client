using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    // 적 한 마리의 상태이상 전부. 점화·동상의 틱 피해는 dealDamage로 되돌려 보낸다
    public sealed class EnemyStatus
    {
        private readonly int _maxHp;
        private readonly StatusImmunity _immunities;
        private readonly Func<bool> _isDead;
        private readonly Action<int> _dealDamage;

        public EnemyStatus(int maxHp, StatusImmunity immunities, Func<bool> isDead, Action<int> dealDamage)
        {
            _maxHp = maxHp;
            _immunities = immunities;
            _isDead = isDead;
            _dealDamage = dealDamage;
        }

        private bool IsDead => _isDead();

        private static bool PositiveFinite(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);

        public bool IsImmuneTo(StatusImmunity status) => (_immunities & status) != 0;

        // 빙결·마비·기절 중에는 이동, 공격, 패시브 시간이 멈춘다
        public bool IsDisabled => IsFrozen || IsParalyzed || IsStunned;

        public float FreezeRemaining { get; private set; }
        public bool IsFrozen => FreezeRemaining > 0;
        public float ParalysisRemaining { get; private set; }
        public bool IsParalyzed => ParalysisRemaining > 0;
        public float StunRemaining { get; private set; }
        public bool IsStunned => StunRemaining > 0;
        public float SlowRemaining { get; private set; }
        private float slowRatio;
        public float VulnerabilityRemaining { get; private set; }
        public float VulnerabilityRatio { get; private set; }

        private readonly Dictionary<object, float> areaSlows = new();

        public float MovementMultiplier
        {
            get
            {
                float strongest = SlowRemaining > 0 ? slowRatio : 0;
                foreach (var ratio in areaSlows.Values) { strongest = Math.Max(strongest, ratio); }
                return 1 - strongest;
            }
        }

        public void ApplyParalysis(float duration)
        {
            if (IsDead || duration <= 0 || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                return;
            }

            ParalysisRemaining = Math.Max(ParalysisRemaining, duration);
        }

        public void SetAreaSlow(object source, float ratio)
        {
            if (source == null || IsDead || ratio <= 0 || ratio >= 1 || float.IsNaN(ratio) || float.IsInfinity(ratio)) { return; }
            areaSlows[source] = ratio;
        }

        public void RemoveAreaSlow(object source)
        {
            if (source != null) { areaSlows.Remove(source); }
        }

        public void ApplyStun(float duration)
        {
            if (!IsDead && !IsImmuneTo(StatusImmunity.Stun) && PositiveFinite(duration))
            {
                StunRemaining = Math.Max(StunRemaining, duration);
            }
        }

        public void ApplySlow(float ratio, float duration)
        {
            if (IsDead || !PositiveFinite(ratio) || ratio >= 1 || !PositiveFinite(duration)) { return; }
            slowRatio = Math.Max(slowRatio, ratio); SlowRemaining = Math.Max(SlowRemaining, duration);
        }

        public void ApplyVulnerability(float ratio, float duration)
        {
            if (IsDead || !PositiveFinite(ratio) || !PositiveFinite(duration)) { return; }
            VulnerabilityRatio = Math.Max(VulnerabilityRatio, ratio);
            VulnerabilityRemaining = Math.Max(VulnerabilityRemaining, duration);
        }

        public void ApplyFreeze(float duration)
        {
            if (IsDead || duration <= 0 || float.IsNaN(duration) || float.IsInfinity(duration)) { return; }
            FreezeRemaining = Math.Max(FreezeRemaining, duration);
        }

        // 취약 상태면 받는 피해를 늘린다
        public int AmplifyDamage(int amount)
        {
            if (VulnerabilityRemaining <= 0)
            {
                return amount;
            }

            double amplified = Math.Floor(Math.Round(amount * (1d + VulnerabilityRatio), 4));
            return (int)Math.Min(int.MaxValue, amplified);
        }

        // 사망 때 상태를 정리하고, 점화 중이었다면 사망 폭발 콜백을 돌려준다
        public Action<Vector2> ClearOnDeath()
        {
            var deathExplosion = BurnRemaining > 0 ? burnOnDeath : null;
            ClearBurn();
            areaSlows.Clear();
            StunRemaining = SlowRemaining = VulnerabilityRemaining = 0;
            slowRatio = VulnerabilityRatio = 0;
            return deathExplosion;
        }

        public float BurnRemaining { get; private set; }
        private float burnDamage;
        private float burnElapsed;
        private float burnMaxHpRatio;
        private Action<Vector2> burnOnDeath;

        public void ApplyBurn(float damagePerSecond, float duration, float maxHpRatio = 0, Action<Vector2> onDeath = null)
        {
            if (IsDead || damagePerSecond <= 0 || duration <= 0
                || float.IsNaN(damagePerSecond) || float.IsInfinity(damagePerSecond)
                || float.IsNaN(duration) || float.IsInfinity(duration)
                || maxHpRatio < 0 || float.IsNaN(maxHpRatio) || float.IsInfinity(maxHpRatio))
            {
                return;
            }

            if (BurnRemaining <= 0)
            {
                ClearBurn();
            }

            BurnRemaining = Math.Max(BurnRemaining, duration);
            burnDamage = Math.Max(burnDamage, damagePerSecond);
            burnMaxHpRatio = Math.Max(burnMaxHpRatio, maxHpRatio);
            if (onDeath != null)
            {
                burnOnDeath = onDeath;
            }
        }

        private void ClearBurn()
        {
            BurnRemaining = 0; burnElapsed = 0; burnDamage = 0;
            burnMaxHpRatio = 0; burnOnDeath = null;
        }

        private void TickBurn(float deltaTime)
        {
            if (IsDead) { ClearBurn(); return; }
            if (BurnRemaining <= 0)
            {
                return;
            }

            burnElapsed += Math.Min(deltaTime, BurnRemaining);
            float remaining = Math.Max(0, BurnRemaining - deltaTime);
            while (burnElapsed >= 1 && !IsDead)
            {
                burnElapsed -= 1;
                _dealDamage(Math.Max(1, (int)(burnDamage + Math.Round(_maxHp * (double)burnMaxHpRatio, 4))));
            }
            BurnRemaining = remaining;
            if (BurnRemaining <= 0 || IsDead)
            {
                ClearBurn();
            }
        }

        private sealed class FrostbiteStack
        {
            public float Damage;
            public float Remaining = 10;
            public float Elapsed;
        }

        private readonly List<FrostbiteStack> frostbite = new();
        public int FrostbiteStacks => frostbite.Count;

        public void ApplyFrostbite(float damagePerSecond)
        {
            if (IsDead || damagePerSecond <= 0 || float.IsNaN(damagePerSecond) || float.IsInfinity(damagePerSecond))
            {
                return;
            }

            if (frostbite.Count >= 5)
            {
                frostbite.RemoveAt(0);
            }

            frostbite.Add(new FrostbiteStack { Damage = damagePerSecond });
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) { return; }
            if (VulnerabilityRemaining > 0 && deltaTime > VulnerabilityRemaining)
            {
                float active = VulnerabilityRemaining;
                TickSlice(active); TickSlice(deltaTime - active);
            }
            else { TickSlice(deltaTime); }
        }

        private void TickSlice(float deltaTime)
        {
            StunRemaining = Math.Max(0, StunRemaining - deltaTime);
            SlowRemaining = Math.Max(0, SlowRemaining - deltaTime);
            if (SlowRemaining <= 0) { slowRatio = 0; }
            FreezeRemaining = Math.Max(0, FreezeRemaining - deltaTime);
            ParalysisRemaining = Math.Max(0, ParalysisRemaining - deltaTime);
            TickBurn(deltaTime);
            if (IsDead) { frostbite.Clear(); return; }
            foreach (var stack in frostbite)
            {
                float activeTime = Math.Min(deltaTime, stack.Remaining);
                stack.Elapsed += activeTime;
                stack.Remaining -= activeTime;
                while (stack.Elapsed >= 1 && !IsDead)
                {
                    stack.Elapsed -= 1;
                    _dealDamage(Math.Max(1, (int)stack.Damage));
                }
            }
            frostbite.RemoveAll(stack => stack.Remaining <= 0);
            VulnerabilityRemaining = Math.Max(0, VulnerabilityRemaining - deltaTime);
            if (VulnerabilityRemaining <= 0) { VulnerabilityRatio = 0; }
            if (IsDead)
            {
                frostbite.Clear();
            }
        }
    }
}
