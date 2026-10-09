using System;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class EnemyHpBarTests
    {
        private sealed class FakePublisher<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }

        private static readonly EnemyAttackStats TestAttack = new EnemyAttackStats(AttackType.Melee, 10, 1f, 0f);

        private FakePublisher<EnemyHpChanged> _hpChanged;
        private FakePublisher<EnemyDied> _died;

        [SetUp]
        public void SetUp()
        {
            _hpChanged = new FakePublisher<EnemyHpChanged>();
            _died = new FakePublisher<EnemyDied>();
        }

        private EnemyModel NewModel(EnemyType type, int maxHp = 10, int id = 1)
            => new EnemyModel(id, 0f, type, maxHp, TestAttack, _hpChanged, _died);

        private static EnemyHpBar BindBar(Enemy enemy, EnemyModel model)
        {
            var bar = enemy.gameObject.AddComponent<EnemyHpBar>();
            bar.Bind(model);
            return bar;
        }

        private static Transform FillOf(Enemy enemy) => enemy.transform.Find("EnemyHpBar(Clone)/Fill");

        [TestCase(EnemyType.Elite)]
        [TestCase(EnemyType.Boss)]
        [Test(Description = "맞으면 체력 비율만큼 줄고, 회복하면 다시 늘어난다")]
        public void Fill_FollowsDamageAndHeal(EnemyType type)
        {
            var enemy = TestEnemy.Create(NewModel(type, 10));
            float fullWidth = Resources.Load<GameObject>("UI/EnemyHpBar").transform.Find("Fill").localScale.x;
            var bar = BindBar(enemy, enemy.Model);
            Assert.IsTrue(bar.IsVisible);
            Assert.AreEqual(fullWidth, FillOf(enemy).localScale.x, 0.0001f, "처음에는 가득 찬 체력바다");

            enemy.Model.TakeDamage(4);
            bar.Sync();
            Assert.AreEqual(fullWidth * 0.6f, FillOf(enemy).localScale.x, 0.0001f, "맞은 뒤");

            enemy.Model.Heal(2);
            bar.Sync();
            Assert.AreEqual(fullWidth * 0.8f, FillOf(enemy).localScale.x, 0.0001f, "회복 뒤");
        }

        [Test(Description = "죽으면 체력바가 사라진다")]
        public void Death_HidesBar()
        {
            var enemy = TestEnemy.Create(NewModel(EnemyType.Boss, 10));
            var bar = BindBar(enemy, enemy.Model);

            enemy.Model.TakeDamage(10);
            bar.Sync();

            Assert.IsFalse(bar.IsVisible);
        }

        [Test(Description = "같은 오브젝트를 새 엘리트 모델로 다시 Bind하면 가득 찬 체력바로 돌아온다(재사용)")]
        public void Rebind_ResetsBar()
        {
            var enemy = TestEnemy.Create(NewModel(EnemyType.Elite, 10, 1));
            float fullWidth = Resources.Load<GameObject>("UI/EnemyHpBar").transform.Find("Fill").localScale.x;
            var bar = BindBar(enemy, enemy.Model);
            enemy.Model.TakeDamage(10);
            bar.Sync();

            bar.Bind(NewModel(EnemyType.Elite, 10, 2));

            Assert.IsTrue(bar.IsVisible);
            Assert.AreEqual(fullWidth, FillOf(enemy).localScale.x, 0.0001f);
            Assert.AreEqual(1, enemy.transform.childCount, "체력바를 다시 만들지 않는다");
        }

        [Test(Description = "엘리트였던 오브젝트가 일반 몬스터로 재사용되면 체력바가 숨는다")]
        public void Rebind_ToNormalHidesBar()
        {
            var enemy = TestEnemy.Create(NewModel(EnemyType.Elite, 10, 1));
            var bar = BindBar(enemy, enemy.Model);

            bar.Bind(NewModel(EnemyType.Normal, 10, 2));

            Assert.IsFalse(bar.IsVisible);
        }

        [Test(Description = "체력바는 스프라이트 위쪽에 놓이고, 적 크기와 상관없이 같은 크기로 보인다")]
        public void Bar_SitsAboveSpriteAndIgnoresParentScale()
        {
            var enemy = TestEnemy.Create(NewModel(EnemyType.Boss));
            enemy.transform.localScale = new Vector3(2f, 2f, 1f);
            var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(enemy.transform, false);
            visual.AddComponent<SpriteRenderer>().sprite = sprite;
            float spriteTop = visual.GetComponent<SpriteRenderer>().bounds.max.y;
            float prefabWidth = Resources.Load<GameObject>("UI/EnemyHpBar").transform.localScale.x;

            BindBar(enemy, enemy.Model);

            var barTransform = enemy.transform.Find("EnemyHpBar(Clone)");
            Assert.Greater(barTransform.position.y, spriteTop);
            Assert.AreEqual(prefabWidth, barTransform.lossyScale.x, 0.0001f);

            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test(Description = "Enemy.Bind가 체력바를 붙이고, 엘리트는 보이고 일반은 안 보인다")]
        public void EnemyBind_AttachesBar()
        {
            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<EnemyHpChanged>();
            builder.AddMessageBroker<EnemyDied>();
            IServiceProvider provider = builder.BuildServiceProvider();
            var hp = provider.GetRequiredService<IPublisher<EnemyHpChanged>>();
            var died = provider.GetRequiredService<IPublisher<EnemyDied>>();
            var elite = new GameObject("Elite").AddComponent<Enemy>();
            var normal = new GameObject("Normal").AddComponent<Enemy>();
            try
            {
                elite.Bind(new EnemyModel(1, 0f, EnemyType.Elite, 10, TestAttack, hp, died), null,
                    provider.GetRequiredService<ISubscriber<EnemyHpChanged>>(), provider.GetRequiredService<ISubscriber<EnemyDied>>());
                normal.Bind(new EnemyModel(2, 0f, EnemyType.Normal, 10, TestAttack, hp, died), null,
                    provider.GetRequiredService<ISubscriber<EnemyHpChanged>>(), provider.GetRequiredService<ISubscriber<EnemyDied>>());

                Assert.IsTrue(elite.GetComponent<EnemyHpBar>().IsVisible);
                Assert.IsFalse(normal.GetComponent<EnemyHpBar>().IsVisible);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(elite.gameObject);
                UnityEngine.Object.DestroyImmediate(normal.gameObject);
            }
        }
    }
}
