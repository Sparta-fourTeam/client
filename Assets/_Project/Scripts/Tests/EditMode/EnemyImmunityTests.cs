using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // 오니의 기절 면역: 면역으로 지정한 상태이상만 막는다
    public sealed class EnemyImmunityTests
    {
        private static readonly EnemyAttackStats Attack = new EnemyAttackStats(AttackType.Melee, 1, 1f, 0f);

        private sealed class Recorder<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }

        private static Enemy Create(StatusImmunity immunities)
        {
            var model = new EnemyModel(1, 1f, EnemyType.Normal, 10, Attack,
                new Recorder<EnemyHpChanged>(), new Recorder<EnemyDied>(),
                passives: null, isSummoned: false, immunities: immunities);
            return TestEnemy.Create(model, new Vector2(0f, 10f));
        }

        [Test(Description = "기절 면역이면 기절에 걸리지 않고 계속 움직인다")]
        public void StunImmune_IgnoresStun()
        {
            var enemy = Create(StatusImmunity.Stun);
            var before = enemy.Position;

            enemy.ApplyStun(2f);
            enemy.Move(1f);

            Assert.IsFalse(enemy.Model.IsStunned);
            Assert.AreEqual(0f, enemy.Model.StunRemaining);
            Assert.Less(enemy.Position.y, before.y);
        }

        [Test(Description = "면역이 없으면 기절에 걸려 움직이지 못한다")]
        public void NotImmune_IsStunned()
        {
            var enemy = Create(StatusImmunity.None);
            var before = enemy.Position;

            enemy.ApplyStun(2f);
            enemy.Move(1f);

            Assert.IsTrue(enemy.Model.IsStunned);
            Assert.AreEqual(before, enemy.Position);
        }

        [Test(Description = "기절 면역은 다른 상태이상(빙결)까지 막지 않는다")]
        public void StunImmune_StillAffectedByFreeze()
        {
            var enemy = Create(StatusImmunity.Stun);

            enemy.ApplyFreeze(2f);

            Assert.IsTrue(enemy.Model.IsFrozen);
        }
    }
}
