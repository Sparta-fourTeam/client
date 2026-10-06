using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // 몬스터 데이터의 Passives 정의 → 패시브 객체, 소환 패시브의 시간 흐름
    public sealed class PassiveTests
    {
        private static readonly EnemyAttackStats Attack = new EnemyAttackStats(AttackType.Melee, 1, 1f, 0f);

        private sealed class Recorder<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }

        private static MonsterDefinition Monster(params PassiveDefinition[] passives)
        {
            return new MonsterDefinition
            {
                Id = 7, Hp = 10, AttackInterval = 1f,
                Passives = new List<PassiveDefinition>(passives),
            };
        }

        private static PassiveDefinition Split(int monsterId = 3, int count = 2) =>
            new PassiveDefinition { Kind = PassiveKind.SplitOnDeath, MonsterId = monsterId, Count = count };

        private static PassiveDefinition Summon(int monsterId = 6, int count = 2, float interval = 5f, int maxTotal = 4) =>
            new PassiveDefinition { Kind = PassiveKind.SummonPeriodic, MonsterId = monsterId, Count = count, Interval = interval, MaxTotal = maxTotal };

        private static PassiveDefinition Immune(params string[] statuses) =>
            new PassiveDefinition { Kind = PassiveKind.Immunity, Statuses = statuses };

        private static EnemyModel Create(Vector2 position, params IPassive[] passives)
        {
            return new EnemyModel(1, position, 0f, EnemyType.Normal, 10, Attack,
                new Recorder<EnemyHpChanged>(), new Recorder<EnemyDied>(), passives);
        }

        // ───────── 정의 검증 ─────────

        [Test(Description = "올바른 정의는 검증을 통과한다")]
        public void Validate_AcceptsValidDefinitions()
        {
            Assert.DoesNotThrow(() => Monster(Split(), Summon(), Immune("Stun")).Validate());
        }

        [Test(Description = "Passives가 없거나 null이어도 검증을 통과한다")]
        public void Validate_AcceptsMissingPassives()
        {
            var noPassives = Monster();
            var nullPassives = Monster();
            nullPassives.Passives = null;

            Assert.DoesNotThrow(() => noPassives.Validate());
            Assert.DoesNotThrow(() => nullPassives.Validate());
        }

        [Test(Description = "알 수 없는 Kind는 거부한다")]
        public void Validate_RejectsUnknownKind()
        {
            var monster = Monster(new PassiveDefinition { Kind = "Fly" });

            Assert.Throws<InvalidOperationException>(() => monster.Validate());
        }

        [Test(Description = "분열 수와 소환 값은 범위를 지켜야 한다")]
        public void Validate_RejectsOutOfRangeValues()
        {
            Assert.Throws<InvalidOperationException>(() => Monster(Split(count: 0)).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(Summon(interval: 0f)).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(Summon(maxTotal: 0)).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(Summon(count: 0)).Validate());
        }

        [Test(Description = "면역은 상태이상 이름이 있어야 하고 알 수 없는 이름은 거부한다")]
        public void Validate_RejectsBadImmunity()
        {
            Assert.Throws<InvalidOperationException>(() => Monster(Immune()).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(Immune("Teleport")).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(Immune("None")).Validate());
        }

        [Test(Description = "분열·소환이 없는 몬스터를 가리키면 거부한다")]
        public void ValidateReferences_RejectsMissingTarget()
        {
            var splitter = Monster(Split(monsterId: 99));
            var table = new Dictionary<int, MonsterDefinition> { { splitter.Id, splitter } };

            Assert.Throws<InvalidOperationException>(() => GameDataStore.ValidatePassiveReferences(table));
        }

        [Test(Description = "가리키는 몬스터가 있으면 통과한다")]
        public void ValidateReferences_AcceptsExistingTarget()
        {
            var child = Monster();
            child.Id = 3;
            var splitter = Monster(Split(monsterId: 3));
            var table = new Dictionary<int, MonsterDefinition> { { child.Id, child }, { splitter.Id, splitter } };

            Assert.DoesNotThrow(() => GameDataStore.ValidatePassiveReferences(table));
        }

        // ───────── 실제 데이터 ─────────

        [Test(Description = "Monsters.json의 패시브가 읽히고 검증을 통과한다")]
        public void RealData_LoadsPassives()
        {
            var store = new GameDataStore();

            var slime = store.Monsters.GetOrThrow(1);
            Assert.AreEqual(1, slime.Passives.Count);
            Assert.AreEqual(PassiveKind.SplitOnDeath, slime.Passives[0].Kind);

            var oni = store.Monsters.GetOrThrow(4);
            Assert.AreEqual(StatusImmunity.Stun, PassiveBuilder.BuildImmunities(oni));

            var spiderLord = store.Monsters.GetOrThrow(5);
            Assert.AreEqual(PassiveKind.SummonPeriodic, spiderLord.Passives[0].Kind);
        }

        // ───────── PassiveBuilder ─────────

        [Test(Description = "정의대로 패시브 객체를 만들고 면역은 객체가 아니라 플래그가 된다")]
        public void Builder_CreatesPassivesAndImmunities()
        {
            var monster = Monster(Split(), Summon(), Immune("Stun"));

            var passives = PassiveBuilder.BuildPassives(monster);

            Assert.AreEqual(2, passives.Count);
            Assert.IsInstanceOf<SplitOnDeath>(passives[0]);
            Assert.IsInstanceOf<SummonPeriodic>(passives[1]);
            Assert.AreEqual(StatusImmunity.Stun, PassiveBuilder.BuildImmunities(monster));
        }

        [Test(Description = "패시브가 없으면 null과 면역 없음을 돌려준다")]
        public void Builder_EmptyDefinition()
        {
            var monster = Monster();

            Assert.IsNull(PassiveBuilder.BuildPassives(monster));
            Assert.AreEqual(StatusImmunity.None, PassiveBuilder.BuildImmunities(monster));
        }

        [Test(Description = "상태를 가진 패시브라 호출마다 새 객체를 만든다")]
        public void Builder_CreatesFreshInstancesEachCall()
        {
            var monster = Monster(Summon());

            var first = PassiveBuilder.BuildPassives(monster);
            var second = PassiveBuilder.BuildPassives(monster);

            Assert.AreNotSame(first[0], second[0]);
        }

        // ───────── SummonPeriodic ─────────

        [Test(Description = "주기가 지나면 count마리를 가로로 벌려 요청하고 주기가 지나기 전에는 요청하지 않는다")]
        public void Summon_RequestsAfterInterval()
        {
            var enemy = Create(new Vector2(2f, 4f), new SummonPeriodic(6, 2, interval: 5f, maxTotal: 10));
            var requests = new List<EnemySpawnRequest>();
            enemy.SpawnRequested += requests.Add;

            enemy.TickPassives(4.9f);
            Assert.IsEmpty(requests);

            enemy.TickPassives(0.2f);

            Assert.AreEqual(2, requests.Count);
            Assert.AreEqual(6, requests[0].MonsterId);
            Assert.AreEqual(1.8f, requests[0].Position.x, 0.0001f);
            Assert.AreEqual(2.2f, requests[1].Position.x, 0.0001f);
        }

        [Test(Description = "총량 상한까지만 소환하고 더 이상 요청하지 않는다")]
        public void Summon_StopsAtMaxTotal()
        {
            var enemy = Create(Vector2.zero, new SummonPeriodic(6, 2, interval: 1f, maxTotal: 3));
            var requests = new List<EnemySpawnRequest>();
            enemy.SpawnRequested += requests.Add;

            for (int i = 0; i < 6; i++)
            {
                enemy.TickPassives(1f);
            }

            Assert.AreEqual(3, requests.Count); // 2 + (남은 1)
        }

        [Test(Description = "기절·빙결·마비 중에는 소환 시간이 흐르지 않는다")]
        public void Summon_PausedWhileDisabled()
        {
            var enemy = Create(Vector2.zero, new SummonPeriodic(6, 1, interval: 1f, maxTotal: 5));
            var requests = new List<EnemySpawnRequest>();
            enemy.SpawnRequested += requests.Add;

            enemy.ApplyStun(5f);
            enemy.TickPassives(3f);
            Assert.IsEmpty(requests);

            enemy.TickStatus(5f); // 기절이 풀린다
            enemy.TickPassives(1f);
            Assert.AreEqual(1, requests.Count);
        }

        [Test(Description = "죽은 몬스터는 소환하지 않는다")]
        public void Summon_NotWhenDead()
        {
            var enemy = Create(Vector2.zero, new SummonPeriodic(6, 1, interval: 1f, maxTotal: 5));
            var requests = new List<EnemySpawnRequest>();
            enemy.SpawnRequested += requests.Add;

            enemy.TakeDamage(10);
            enemy.TickPassives(5f);

            Assert.IsEmpty(requests);
        }

        [Test(Description = "소환 값이 잘못되면 생성 때 거부한다")]
        public void Summon_RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SummonPeriodic(6, 0, 1f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SummonPeriodic(6, 1, 0f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SummonPeriodic(6, 1, 1f, 0));
        }
    }
}
