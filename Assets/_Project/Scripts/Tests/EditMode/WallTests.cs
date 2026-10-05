using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Combat;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class WallTests
    {
        private sealed class FakePublisher<T> : IPublisher<T>, IBufferedPublisher<T>
        {
            public List<T> Published { get; } = new List<T>();

            public void Publish(T message)
            {
                Published.Add(message);
            }
        }

        private FakePublisher<WallHpChanged> _hp;
        private FakePublisher<WallDestroyed> _destroyed;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _hp = new FakePublisher<WallHpChanged>();
            _destroyed = new FakePublisher<WallDestroyed>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        // 주입만 되고 Initialize 전인 벽
        private Wall CreateUninitializedWall()
        {
            _go = new GameObject("Wall");
            Wall wall = _go.AddComponent<Wall>();
            wall.Construct(_hp, _destroyed, null); // EditMode에서는 Start가 호출되지 않아 스테이지 값을 쓰지 않는다
            return wall;
        }

        private Wall CreateWall(int maxHp)
        {
            Wall wall = CreateUninitializedWall();
            wall.Initialize(maxHp);
            return wall;
        }

        [Test(Description = "초기화 하면 초기 HP를 1번 발행한다")]
        public void Initialize_PublishesInitialHp()
        {
            CreateWall(10);

            Assert.AreEqual(1, _hp.Published.Count);
            Assert.AreEqual(10, _hp.Published[0].Current);
            Assert.AreEqual(10, _hp.Published[0].Max);
        }

        [Description("최대 HP가 0 이하면 예외가 난다")]
        [TestCase(0)]
        [TestCase(-1)]
        public void Initialize_NonPositiveMaxHp_Throws(int maxHp)
        {
            Wall wall = CreateUninitializedWall();

            Assert.Throws<ArgumentOutOfRangeException>(() => wall.Initialize(maxHp));
        }

        [Test(Description = "초기화 전에 공격받으면 무시한다")]
        public void TakeDamage_BeforeInitialize_IsIgnored()
        {
            Wall wall = CreateUninitializedWall();

            wall.TakeDamage(5);

            Assert.IsFalse(wall.IsDestroyed);
            Assert.AreEqual(0, _hp.Published.Count);
            Assert.AreEqual(0, _destroyed.Published.Count);
        }

        [Test(Description = "HP가 10에서 3을 맞으면 7이 되고 ,HUD에 보내는 값도 7이다")]
        public void TakeDamage_ReducesHpByAmount()
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(3);

            Assert.AreEqual(7, wall.CurrentHp);
            Assert.AreEqual(7, _hp.Published.Last().Current);
            Assert.IsFalse(wall.IsDestroyed);
        }

        [Test(Description = "IDamageable로 다뤄도 HP가 줄어든다")]
        public void TakeDamage_ViaIDamageable_ReducesHp()
        {
            Wall wall = CreateWall(10);
            IDamageable target = wall;

            target.TakeDamage(3);

            Assert.AreEqual(7, wall.CurrentHp);
            Assert.AreEqual(7, _hp.Published.Last().Current);
        }

        [Test(Description = "HP보다 큰 데미지를 입어도 음수가 아니라 0이 된다")]
        public void TakeDamage_OverMaxHp_ClampsToZero()
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(999);

            Assert.AreEqual(0, wall.CurrentHp);
            Assert.AreEqual(0, _hp.Published.Last().Current);
        }

        [Description("0이나 음수 데미지는 무시한다")]
        [TestCase(0)]
        [TestCase(-5)]
        public void TakeDamage_NonPositiveAmount_IsIgnored(int amount)
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(amount);

            Assert.AreEqual(10, wall.CurrentHp);
            Assert.AreEqual(1, _hp.Published.Count); // Initialize에서 발행한 초기값만
        }


        [Test(Description = "HP가 0이 된 뒤 또 맞아도 파괴 메시지는 1번만 발송된다 (실패 판정·결과 전송은 판당 1번이어야 함)")]
        public void TakeDamage_ReachesZero_PublishesDestroyedOnce()
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(10);
            wall.TakeDamage(10);

            Assert.IsTrue(wall.IsDestroyed);
            Assert.AreEqual(1, _destroyed.Published.Count);
        }

        [Test(Description = "파괴 된 뒤 남은 적이 계속 때려도 HP,파괴 메세지가 더 나가지 않는다")]
        public void TakeDamage_AfterDestroyed_PublishesNothing()
        {
            Wall wall = CreateWall(10);
            wall.TakeDamage(10);
            int hpCountAfterDestroy = _hp.Published.Count;

            wall.TakeDamage(1);

            Assert.AreEqual(hpCountAfterDestroy, _hp.Published.Count);
            Assert.AreEqual(1, _destroyed.Published.Count);
        }

        [Test(Description = "여러번 맞다가 마지막 한 방이 남은 HP보다 커도 0이 되며 파괴된다")]
        public void TakeDamage_MultipleHits_AccumulatesUntilDestroyed()
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(4);
            wall.TakeDamage(4);
            Assert.IsFalse(wall.IsDestroyed);

            wall.TakeDamage(4);

            Assert.AreEqual(0, wall.CurrentHp);
            Assert.IsTrue(wall.IsDestroyed);
            Assert.AreEqual(1, _destroyed.Published.Count);
        }

        [Test(Description = "파괴된 벽을 다시 초기화 하면 새 판처럼 HP가 채워지고 파괴상태가 풀린다")]
        public void Initialize_AfterDestroyed_ResetsWall()
        {
            Wall wall = CreateWall(10);
            wall.TakeDamage(10);

            wall.Initialize(20);

            Assert.IsFalse(wall.IsDestroyed);
            Assert.AreEqual(20, wall.CurrentHp);
            Assert.AreEqual(20, _hp.Published.Last().Current);
        }

        private Sprite[] CreateSprites(int count)
        {
            var sprites = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                var tex = new Texture2D(2, 2);
                sprites[i] = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.zero);
                sprites[i].name = "WallStage" + i;
            }

            return sprites;
        }

        private (Wall wall, SpriteRenderer renderer, Sprite[] sprites) CreateWallWithSprites()
        {
            Wall wall = CreateUninitializedWall();
            var renderer = _go.AddComponent<SpriteRenderer>();
            Sprite[] sprites = CreateSprites(3);
            typeof(Wall).GetField("_sprites", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(wall, sprites);
            return (wall, renderer, sprites);
        }

        [Test(Description = "HP 비율이 67%, 33% 이하로 내려가면 파손 단계 스프라이트로 바뀐다")]
        public void TakeDamage_SwapsSpriteByHpRatio()
        {
            var (wall, renderer, sprites) = CreateWallWithSprites();
            wall.Initialize(100);
            Assert.AreSame(sprites[0], renderer.sprite);

            wall.TakeDamage(32);
            Assert.AreSame(sprites[0], renderer.sprite, "68%는 아직 온전한 단계");

            wall.TakeDamage(1);
            Assert.AreSame(sprites[1], renderer.sprite, "67%부터 파손 1단계");

            wall.TakeDamage(34);
            Assert.AreSame(sprites[2], renderer.sprite, "33%부터 파손 2단계");
        }

        [Test(Description = "다시 초기화하면 온전한 스프라이트로 돌아온다")]
        public void Initialize_ResetsSpriteToFullState()
        {
            var (wall, renderer, sprites) = CreateWallWithSprites();
            wall.Initialize(100);
            wall.TakeDamage(100);
            Assert.AreSame(sprites[2], renderer.sprite);

            wall.Initialize(100);

            Assert.AreSame(sprites[0], renderer.sprite);
        }

        [Test(Description = "스프라이트 배열이 없어도 피해를 받을 수 있다")]
        public void TakeDamage_WithoutSprites_DoesNotThrow()
        {
            Wall wall = CreateWall(10);
            _go.AddComponent<SpriteRenderer>();

            Assert.DoesNotThrow(() => wall.TakeDamage(10));
            Assert.IsTrue(wall.IsDestroyed);
        }

        [Test(Description = "피해를 받으면 적과 같은 피격 플래시가 붙고, 무시되는 피해에는 붙지 않는다")]
        public void TakeDamage_AttachesHitFlashOnlyWhenDamaged()
        {
            Wall wall = CreateWall(10);
            _go.AddComponent<SpriteRenderer>();

            wall.TakeDamage(0);
            Assert.IsNull(_go.GetComponent<Game.Core.HitFlash>());

            wall.TakeDamage(1);
            Assert.IsNotNull(_go.GetComponent<Game.Core.HitFlash>());
        }
    }
}
