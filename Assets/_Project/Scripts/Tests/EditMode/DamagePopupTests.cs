using System;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class DamagePopupTests
    {
        private static readonly EnemyAttackStats TestAttack = new EnemyAttackStats(AttackType.Melee, 10, 1f, 0f);

        private GameObject _enemyObject;

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            DamagePopupPool.Clear();
            if (_enemyObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_enemyObject);
            }
        }

        private static DamagePopupPool Pool => UnityEngine.Object.FindFirstObjectByType<DamagePopupPool>();

        private static DamagePopup[] ActivePopups => Pool.GetComponentsInChildren<DamagePopup>(false);

        private (Enemy enemy, EnemyModel model) BindEnemy(int maxHp = 100)
        {
            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<EnemyHpChanged>();
            builder.AddMessageBroker<EnemyDied>();
            IServiceProvider provider = builder.BuildServiceProvider();
            var model = new EnemyModel(1, 0f, EnemyType.Normal, maxHp, TestAttack,
                provider.GetRequiredService<IPublisher<EnemyHpChanged>>(), provider.GetRequiredService<IPublisher<EnemyDied>>());
            _enemyObject = new GameObject("Enemy");
            var enemy = _enemyObject.AddComponent<Enemy>();
            enemy.Bind(model, null,
                provider.GetRequiredService<ISubscriber<EnemyHpChanged>>(), provider.GetRequiredService<ISubscriber<EnemyDied>>());
            return (enemy, model);
        }

        // 편집 모드에서는 DOTween이 매 프레임 돌지 않으므로, 보이는 숫자들의 시퀀스 시간을 직접 앞당긴다
        private static void Tick(float seconds)
        {
            var field = typeof(DamagePopup).GetField("_sequence", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var popup in ActivePopups)
            {
                var sequence = (Sequence)field.GetValue(popup);
                if (sequence != null && sequence.active)
                {
                    sequence.Goto(sequence.Elapsed() + seconds);
                }
            }
        }

        private static void Show(int amount) => DamagePopupPool.Show(amount, Vector3.zero, SceneManager.GetActiveScene());

        [Test(Description = "데미지 숫자 프리팹이 Resources에 있고 글자 컴포넌트가 연결돼 있다")]
        public void Prefab_ExistsWithText()
        {
            var prefab = Resources.Load<DamagePopup>("UI/DamagePopup");

            Assert.IsNotNull(prefab);
            // 에셋을 직접 건드리지 않도록 복사본으로 확인한다
            var copy = UnityEngine.Object.Instantiate(prefab);
            copy.Show(8, Vector3.zero);
            Assert.AreEqual("8", copy.DisplayedText, "글자 컴포넌트가 연결돼 있어야 숫자가 쓰인다");
            UnityEngine.Object.DestroyImmediate(copy.gameObject);
        }

        [Test(Description = "숫자는 위로 떠오르고, 앞쪽 절반은 그대로 있다가 끝까지 투명해진다")]
        public void Popup_RisesAndFades()
        {
            Show(5);
            var popup = ActivePopups.Single();
            float startY = popup.transform.position.y;

            Tick(0.3f);
            Assert.Greater(popup.transform.position.y, startY, "떠오른다");
            Assert.AreEqual(1f, popup.Alpha, 0.0001f, "앞쪽 절반은 불투명");

            Tick(0.2f);
            Assert.Less(popup.Alpha, 1f, "뒤쪽은 투명해진다");
            Assert.Greater(popup.Alpha, 0f);
        }

        [Test(Description = "피해량이 숫자로 표시되고 0 이하는 표시하지 않는다")]
        public void Show_DisplaysAmount_AndIgnoresNonPositive()
        {
            Show(0);
            Show(-5);
            Assert.IsNull(Pool, "표시할 게 없으면 풀도 만들지 않는다");

            Show(37);

            Assert.AreEqual(1, Pool.ActiveCount);
            Assert.AreEqual("37", ActivePopups.Single().DisplayedText);
        }

        [Test(Description = "지속 시간이 지나면 숫자가 사라지고 풀로 돌아간다")]
        public void Popup_ReturnsToPoolAfterDuration()
        {
            Show(5);

            Tick(0.3f);
            Assert.AreEqual(1, Pool.ActiveCount, "아직 보이는 중");

            Tick(1f);
            Assert.AreEqual(0, Pool.ActiveCount);
            Assert.AreEqual(1, Pool.FreeCount);
            Assert.AreEqual(0, ActivePopups.Length, "비활성화된다");
        }

        [Test(Description = "돌아간 숫자를 다시 쓰고 새로 만들지 않는다")]
        public void Popup_IsReused()
        {
            Show(1);
            var pool = Pool;
            Tick(5f);
            int created = pool.transform.childCount;

            Show(2);

            Assert.AreEqual(created, pool.transform.childCount);
            var popup = ActivePopups.Single();
            Assert.AreEqual("2", popup.DisplayedText);
            Assert.AreEqual(1f, popup.Alpha, 0.0001f, "다시 쓸 때는 처음처럼 불투명하다");
        }

        [Test(Description = "동시에 너무 많이 맞아도 상한을 넘기지 않고, 오래된 숫자를 새 숫자로 바꾼다")]
        public void ManyHits_AreCappedAndRecycleOldest()
        {
            for (int i = 1; i <= DamagePopupPool.MaxActive + 10; i++)
            {
                Show(i);
            }

            var pool = Pool;
            Assert.AreEqual(DamagePopupPool.MaxActive, pool.ActiveCount);
            Assert.AreEqual(DamagePopupPool.MaxActive, pool.transform.childCount, "상한보다 많이 만들지 않는다");
            var shown = ActivePopups.Select(p => p.Amount).OrderBy(a => a).ToList();
            Assert.AreEqual(11, shown.First(), "가장 오래된 10개가 밀려났다");
            Assert.AreEqual(DamagePopupPool.MaxActive + 10, shown.Last(), "가장 새 숫자는 항상 보인다");
        }

        [Test(Description = "Clear하면 풀과 숫자가 모두 사라진다(스테이지가 끝나 씬이 내려갈 때와 같은 결과)")]
        public void Clear_RemovesEverything()
        {
            Show(1);
            Show(2);

            DamagePopupPool.Clear();

            Assert.IsNull(Pool);
        }

        [Test(Description = "적이 맞으면 실제로 깎인 체력이 숫자로 뜬다")]
        public void EnemyDamaged_ShowsAppliedDamage()
        {
            var (_, model) = BindEnemy(100);

            model.TakeDamage(30);

            Assert.AreEqual(30, ActivePopups.Single().Amount);
        }

        [Test(Description = "남은 체력을 넘는 과잉 피해는 숫자에서 빠진다")]
        public void EnemyDamaged_ExcludesOverkill()
        {
            var (_, model) = BindEnemy(100);
            model.TakeDamage(70);

            // 죽으면 Enemy가 Destroy를 부르는데 에디트 모드 테스트에서는 그 호출이 에러 로그를 남긴다
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode"));
            model.TakeDamage(500);

            Assert.IsTrue(ActivePopups.Any(p => p.Amount == 30), "남은 30만큼만 표시");
            Assert.IsFalse(ActivePopups.Any(p => p.Amount == 500));
        }

        [Test(Description = "회복이나 막힌 피해에는 숫자가 뜨지 않는다")]
        public void HealAndBlockedDamage_ShowNothing()
        {
            var (_, model) = BindEnemy(100);
            model.TakeDamage(40);
            int before = Pool.ActiveCount;

            model.Heal(20);
            model.TakeDamage(0);

            Assert.AreEqual(before, Pool.ActiveCount);
        }

        [Test(Description = "숫자는 적의 자식이 아니라서 적이 사라져도 남는다")]
        public void Popup_OutlivesEnemy()
        {
            var (_, model) = BindEnemy(100);
            model.TakeDamage(10);

            UnityEngine.Object.DestroyImmediate(_enemyObject);

            Assert.AreEqual(1, ActivePopups.Length);
        }

        [Test(Description = "숫자는 적 스프라이트 위쪽에 뜬다")]
        public void Popup_AppearsAboveSprite()
        {
            var (enemy, model) = BindEnemy(100);
            var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(enemy.transform, false);
            visual.AddComponent<SpriteRenderer>().sprite = sprite;
            float top = visual.GetComponent<SpriteRenderer>().bounds.max.y;

            model.TakeDamage(10);

            Assert.GreaterOrEqual(ActivePopups.Single().transform.position.y, top);
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
