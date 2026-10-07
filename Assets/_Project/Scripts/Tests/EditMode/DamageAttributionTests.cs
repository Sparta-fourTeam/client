using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Core.Combat;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class DamageAttributionTests
    {
        private readonly List<(int skill, int damage)> _dealt = new();

        private void OnDealt(int skill, int damage) => _dealt.Add((skill, damage));

        [SetUp]
        public void SetUp()
        {
            _dealt.Clear();   // 테스트마다 새로 시작한다 (NUnit은 한 인스턴스를 재사용한다)
            DamageAttribution.Dealt += OnDealt;
        }

        [TearDown]
        public void TearDown()
        {
            DamageAttribution.Dealt -= OnDealt;
            Assert.AreEqual(0, DamageAttribution.Current, "범위가 모두 닫혀야 한다");
        }

        private sealed class Publisher<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }

        private static Game.Core.Enemy Enemy(int hp) =>
            TestEnemy.Create(new EnemyModel(1, 0f, EnemyType.Normal, hp, new EnemyAttackStats(AttackType.Melee, 10, 1f, 0f),
                new Publisher<EnemyHpChanged>(), new Publisher<EnemyDied>()));

        [Test(Description = "범위를 열면 현재 스킬이 바뀌고 닫으면 이전 값으로 돌아간다 (중첩 포함)")]
        public void Scopes_NestAndRestore()
        {
            using (DamageAttribution.Begin(1))
            {
                Assert.AreEqual(1, DamageAttribution.Current);
                using (DamageAttribution.Begin(2)) { Assert.AreEqual(2, DamageAttribution.Current); }
                Assert.AreEqual(1, DamageAttribution.Current);
            }
        }

        [Test(Description = "가장 바깥 범위가 이긴다: 이미 열려 있으면 안쪽 시전은 바깥 스킬 몫이다 (자식 스킬 피해가 부모로 모인다)")]
        public void BeginOutermost_KeepsOuterSkill()
        {
            using (DamageAttribution.BeginOutermost(7))
            {
                using (DamageAttribution.BeginOutermost(8)) { Assert.AreEqual(7, DamageAttribution.Current); }
                Assert.AreEqual(7, DamageAttribution.Current);
            }
        }

        [Test(Description = "스킬 밖에서 받은 피해는 기록하지 않는다")]
        public void Damage_OutsideScope_IsNotRecorded()
        {
            Enemy(10).TakeDamage(5);

            Assert.IsEmpty(_dealt);
        }

        [Test(Description = "스킬 범위 안에서 받은 피해는 그 스킬 몫으로 실제로 깎인 체력만 기록한다 (남은 체력을 넘는 과잉 피해는 뺀다)")]
        public void Damage_InsideScope_RecordsActualHpLost()
        {
            var enemy = Enemy(10);
            using (DamageAttribution.Begin(3))
            {
                enemy.TakeDamage(6);      // 10 → 4: 6
                enemy.TakeDamage(100);    // 4 → 0: 4 (과잉 96은 뺀다)
                enemy.TakeDamage(5);      // 이미 죽음: 기록 없음
            }

            CollectionAssert.AreEqual(new[] { (3, 6), (3, 4) }, _dealt);
        }

        [Test(Description = "시전기가 아니라 피해 객체가 만들어질 때의 스킬을 기억한다: 시전 범위가 끝난 뒤 틱에서 준 피해도 그 스킬 몫이다")]
        public void DamageObject_RemembersSkillFromCreation()
        {
            var go = new GameObject("FieldAttributionTest");
            try
            {
                var field = go.AddComponent<ElectromagneticField>();
                var enemy = Enemy(100);
                var provider = new Provider(enemy);
                var pool = new ObjectPool<ElectromagneticField>(() => field);
                using (DamageAttribution.Begin(42)) { pool.Get().Init(pool, provider, Vector2.zero, 5f, 10f, 3, 0.3f); }
                Assert.AreEqual(0, DamageAttribution.Current);

                typeof(ElectromagneticField).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(field, new object[] { 1f });

                CollectionAssert.AreEqual(new[] { (42, 10) }, _dealt);
            }
            finally { Object.DestroyImmediate(go); }
        }

        private sealed class Provider : IEnemyTargetProvider
        {
            private readonly IEnemyTarget _target;
            public Provider(IEnemyTarget target) => _target = target;
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results) { results.Clear(); results.Add(_target); return 1; }
        }
    }
}
