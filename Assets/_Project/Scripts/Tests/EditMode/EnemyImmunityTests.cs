using System.Collections.Generic;
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

        private static EnemyModel Create(StatusImmunity immunities)
        {
            return new EnemyModel(1, new Vector2(0f, 10f), 1f, EnemyType.Normal, 10, Attack,
                new Recorder<EnemyHpChanged>(), new Recorder<EnemyDied>(),
                passives: null, isSummoned: false, immunities: immunities);
        }

        [Test(Description = "기절 면역이면 기절에 걸리지 않고 계속 움직인다")]
        public void StunImmune_IgnoresStun()
        {
            var enemy = Create(StatusImmunity.Stun);
            var before = enemy.Position;

            enemy.ApplyStun(2f);
            enemy.Move(1f);

            Assert.IsFalse(enemy.IsStunned);
            Assert.AreEqual(0f, enemy.StunRemaining);
            Assert.Less(enemy.Position.y, before.y);
        }

        [Test(Description = "면역이 없으면 기절에 걸려 움직이지 못한다")]
        public void NotImmune_IsStunned()
        {
            var enemy = Create(StatusImmunity.None);
            var before = enemy.Position;

            enemy.ApplyStun(2f);
            enemy.Move(1f);

            Assert.IsTrue(enemy.IsStunned);
            Assert.AreEqual(before, enemy.Position);
        }

        [Test(Description = "기절 면역은 다른 상태이상(빙결)까지 막지 않는다")]
        public void StunImmune_StillAffectedByFreeze()
        {
            var enemy = Create(StatusImmunity.Stun);

            enemy.ApplyFreeze(2f);

            Assert.IsTrue(enemy.IsFrozen);
        }

        [Test(Description = "면역 여부를 조회할 수 있다")]
        public void IsImmuneTo_ReflectsFlags()
        {
            Assert.IsTrue(Create(StatusImmunity.Stun).IsImmuneTo(StatusImmunity.Stun));
            Assert.IsFalse(Create(StatusImmunity.None).IsImmuneTo(StatusImmunity.Stun));
        }

        [Test(Description = "기본 생성(면역 인자 생략)은 면역이 없다")]
        public void DefaultConstructor_HasNoImmunity()
        {
            var enemy = new EnemyModel(1, Vector2.zero, 1f, EnemyType.Normal, 10, Attack,
                new Recorder<EnemyHpChanged>(), new Recorder<EnemyDied>());

            enemy.ApplyStun(1f);

            Assert.IsTrue(enemy.IsStunned);
        }
    }
}
