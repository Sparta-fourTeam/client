using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Core.Combat;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    // 새 몬스터 5종(Id 21~25)의 실제 데이터가 의도한 능력을 갖는지 지킨다. 능력의 동작 자체는 각 패시브 테스트가 본다
    public sealed class NewMonsterDataTests
    {
        private const int Crab = 21, BrainSpider = 22, EyeJelly = 23, Ghost = 24, Golem = 25;
        private static readonly int[] BatchIds = { Crab, BrainSpider, EyeJelly, Ghost, Golem };
        private const string TablePath = "Assets/_Project/Data/MonsterAssetTable.asset";

        private sealed class Recorder<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }

        // 정해 둔 값을 차례로 돌려주고, 다 쓰면 마지막 값을 계속 돌려주는 랜덤
        private sealed class ScriptedRandom : IRandomProvider
        {
            private readonly Queue<float> _values;
            private float _last;

            public ScriptedRandom(params float[] values)
            {
                _values = new Queue<float>(values);
                _last = values.Length > 0 ? values[values.Length - 1] : 0.5f;
            }

            public float Range(float min, float max) => _values.Count > 0 ? _values.Dequeue() : _last;
        }

        private static GameDataStore Store() => new GameDataStore();
        private static int Hp(int id) => Store().Monsters.GetOrThrow(id).Hp;

        // EnemyFactory가 만드는 것과 같은 방식으로 데이터에서 모델을 만든다
        private static EnemyModel Build(int monsterId, IRandomProvider random = null)
        {
            var monster = Store().Monsters.GetOrThrow(monsterId);
            return new EnemyModel(
                1, monster.Speed, monster.GetEnemyType(), monster.Hp,
                new EnemyAttackStats(monster.GetAttackType(), monster.Damage, monster.AttackInterval, monster.AttackRange, monster.ProjectileSpeed,
                    monster.BurstCount, monster.BurstInterval),
                new Recorder<EnemyHpChanged>(), new Recorder<EnemyDied>(),
                PassiveBuilder.BuildPassives(monster, random ?? new ScriptedRandom(0.5f)), false, PassiveBuilder.BuildImmunities(monster),
                DamageProfile.From(monster), PassiveBuilder.BuildModifiers(monster));
        }

        private static void Hit(EnemyModel model, int amount, Element element = Element.Neutral, CastType cast = CastType.Projectile) =>
            model.TakeDamage(new DamageInfo(amount, 1, element, cast, true));

        // ───────── 공통 ─────────

        [Test]
        public void EveryNewMonster_BuildsFromData()
        {
            foreach (int id in BatchIds)
            {
                Assert.DoesNotThrow(() => Build(id), $"Monsters {id}");
            }
        }

        [Test(Description = "새 몬스터의 프리팹은 Enemy 컴포넌트와 애니메이터가 붙은 Visual을 가진다")]
        public void EveryNewMonster_PrefabHasEnemyAndAnimatedVisual()
        {
            var table = AssetDatabase.LoadAssetAtPath<MonsterAssetTable>(TablePath);

            foreach (int id in BatchIds)
            {
                Assert.IsTrue(table.TryGet(id, out var entry), $"MonsterId {id}: 표 항목이 없습니다");
                Assert.IsNotNull(entry.prefab, $"MonsterId {id}: prefab이 없습니다");
                var visual = entry.prefab.transform.Find("Visual");
                Assert.IsNotNull(visual, $"MonsterId {id}: Visual 자식이 없습니다");
                Assert.IsNotNull(visual.GetComponentInChildren<Animator>(), $"MonsterId {id}: Visual에 Animator가 없습니다");
            }
        }

        [Test(Description = "스테이지 1에는 넣지 않고, 스테이지 2는 21~23, 스테이지 3은 21~25가 나온다")]
        public void Stages_PlaceTheNewMonsters()
        {
            var store = Store();

            CollectionAssert.IsEmpty(store.Stages.GetOrThrow(1).MonsterIds.Intersect(BatchIds));
            CollectionAssert.AreEquivalent(new[] { Crab, BrainSpider, EyeJelly }, store.Stages.GetOrThrow(2).MonsterIds.Intersect(BatchIds));
            CollectionAssert.AreEquivalent(BatchIds, store.Stages.GetOrThrow(3).MonsterIds.Intersect(BatchIds));
        }

        // ───────── 갑옷 게: 방어형 ─────────

        [Test]
        public void Crab_ShieldBlocksSixHitsThenBreaks()
        {
            var model = Build(Crab);

            for (int i = 0; i < 6; i++)
            {
                Hit(model, 10);
            }

            Assert.AreEqual(Hp(Crab), model.Hp, "방어막이 6번을 막는다");

            Hit(model, 10);
            Assert.AreEqual(Hp(Crab) - 10, model.Hp);
        }

        [Test]
        public void Crab_ShieldBlocksDebuffsOnlyWhileUp()
        {
            var model = Build(Crab);

            model.ApplyStun(3f);
            model.ApplyFreeze(3f);
            Assert.IsFalse(model.IsStunned || model.IsFrozen, "방어막이 있는 동안 모든 디버프에 걸리지 않는다");

            for (int i = 0; i < 6; i++)
            {
                Hit(model, 1);
            }

            model.ApplyStun(3f);
            Assert.IsTrue(model.IsStunned, "방어막이 깨지면 걸린다");
            model.ApplyBurn(5f, 3f);
            Assert.AreEqual(0f, model.BurnRemaining, "점화는 방어막과 상관없이 면역이다");
        }

        [Test]
        public void Crab_ImmuneToLightning_AndPushedHalf()
        {
            var model = Build(Crab);
            for (int i = 0; i < 6; i++) { Hit(model, 1); } // 방어막을 깬다

            Hit(model, 10, Element.Lightning);

            Assert.AreEqual(Hp(Crab), model.Hp, "뇌속성 피해는 0");
            Assert.AreEqual(0.5f, model.ResolveKnockback(Vector2.up, 1f).y, 0.0001f);
        }

        // ───────── 뇌 거미: 속도형 ─────────

        [Test]
        public void BrainSpider_FastAndSurgesPeriodically()
        {
            var model = Build(BrainSpider);

            Assert.AreEqual(0.4f, model.MoveSpeed, 0.0001f);
            model.TickPassives(6f);
            Assert.AreEqual(1f, model.MoveSpeed, 0.0001f, "6초마다 2.5배");
            model.TickStatus(1.5f);
            Assert.AreEqual(0.4f, model.MoveSpeed, 0.0001f, "1.5초 뒤 끝난다");
        }

        [Test]
        public void BrainSpider_WeakToIce()
        {
            var model = Build(BrainSpider);

            Hit(model, 10, Element.Ice);

            Assert.AreEqual(Hp(BrainSpider) - 15, model.Hp, "빙속성 +50%");
        }

        // ───────── 눈 해파리: 회피·임시 방어막형 ─────────

        [Test(Description = "처음 맞고 나면 방어막이 켜져서 다음 공격을 막는다")]
        public void EyeJelly_TemporaryShieldArmsAfterAHit()
        {
            // 첫 공격: 회피 굴림 0.9(실패) → 맞는다. 피격 방어막 굴림 0.1(성공) → 켜진다
            var model = Build(EyeJelly, new ScriptedRandom(0.9f, 0.1f));

            Hit(model, 10);
            Assert.AreEqual(Hp(EyeJelly) - 10, model.Hp);

            Hit(model, 10);
            Assert.AreEqual(Hp(EyeJelly) - 10, model.Hp, "켜진 방어막이 막는다");
        }

        [Test]
        public void EyeJelly_CanEvade_AndIsWeakToEarth()
        {
            var evading = Build(EyeJelly, new ScriptedRandom(0.1f));
            Hit(evading, 10);
            Assert.AreEqual(Hp(EyeJelly), evading.Hp, "회피 굴림 0.1 < 0.2");

            var model = Build(EyeJelly, new ScriptedRandom(0.9f, 0.9f));
            Hit(model, 10, Element.Earth);
            Assert.AreEqual(Hp(EyeJelly) - 20, model.Hp, "토속성 +100%");
        }

        // ───────── 유령: 지상 공격 면역형 ─────────

        [Test]
        public void Ghost_IgnoresEarthButTakesOtherElements()
        {
            var model = Build(Ghost);

            Hit(model, 10, Element.Earth);
            Assert.AreEqual(Hp(Ghost), model.Hp, "토속성(지상 공격) 면역");

            Hit(model, 10, Element.Fire);
            Assert.AreEqual(Hp(Ghost) - 10, model.Hp);
        }

        [Test]
        public void Ghost_IsTheFastest()
        {
            Assert.AreEqual(0.6f, Build(Ghost).MoveSpeed, 0.0001f);
        }

        // ───────── 골렘: 투사체 차단형 ─────────

        [Test]
        public void Golem_BlocksProjectileImpactAndPierce()
        {
            var model = Build(Golem);

            Hit(model, 10, cast: CastType.Projectile);
            Assert.AreEqual(Hp(Golem), model.Hp, "투사체의 직접 피해는 0");
            Assert.IsTrue(model.BlocksPierce);

            Hit(model, 10, cast: CastType.Area);
            Assert.AreEqual(Hp(Golem) - 10, model.Hp, "투사체가 아니면 피해를 받는다");
        }

        [Test]
        public void Golem_WeakToFire_ImmuneToBurnParalysisFreeze()
        {
            var model = Build(Golem);

            Hit(model, 10, Element.Fire, CastType.Area);
            Assert.AreEqual(Hp(Golem) - 30, model.Hp, "화속성 +200%");

            model.ApplyBurn(5f, 3f);
            model.ApplyParalysis(3f);
            model.ApplyFreeze(3f);
            Assert.AreEqual(0f, model.BurnRemaining);
            Assert.IsFalse(model.IsParalyzed || model.IsFrozen);

            model.ApplyStun(3f);
            Assert.IsTrue(model.IsStunned, "기절은 걸린다");
            Assert.AreEqual(0.5f, model.ResolveKnockback(Vector2.up, 1f).y, 0.0001f);
        }
    }
}
