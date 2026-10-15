using System.Linq;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Tests
{
    /// <summary>적 발밑 그림자(#243). 실제 몬스터 프리팹으로 확인한다</summary>
    public sealed class EnemyShadowTests
    {
        private GameObject _enemy;

        // 테스트 어셈블리는 DOTween을 참조하지 않으므로 필요한 두 함수만 이름으로 부른다
        private static System.Type DOTweenType(object tween, string name) => tween.GetType().Assembly.GetType("DG.Tweening." + name);

        private static void Goto(object tween, float time) =>
            DOTweenType(tween, "TweenExtensions").GetMethod("Goto").Invoke(null, new[] { tween, time, (object)false });

        private object _anyTween;

        [TearDown]
        public void TearDown()
        {
            if (_anyTween != null)
            {
                DOTweenType(_anyTween, "DOTween").GetMethod("KillAll", new[] { typeof(bool) }).Invoke(null, new object[] { false });
            }

            if (_enemy != null) { Object.DestroyImmediate(_enemy); }
        }

        private EnemyShadow Spawn(string prefab, float y = 0f)
        {
            _enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Stage/{prefab}.prefab"));
            _enemy.transform.position = new Vector3(0f, y, 0f);
            var shadow = _enemy.AddComponent<EnemyShadow>();
            shadow.Bind();
            return shadow;
        }

        private Transform Body => _enemy.GetComponentInChildren<Animator>().transform;

        [Test(Description = "그림자는 비주얼 SortingGroup 안, 애니메이션되는 Body 밖에 하나 생기고 모든 파츠보다 뒤에 그려진다")]
        public void Bind_CreatesShadowInsideGroupOutsideBody()
        {
            var shadow = Spawn("Golem");

            var group = _enemy.GetComponentInChildren<SortingGroup>();
            Assert.AreSame(group.transform, shadow.Shadow.parent);
            Assert.IsFalse(shadow.Shadow.IsChildOf(Body), "Body가 흔들려도 그림자는 바닥에 있다");
            int lowestPart = Body.GetComponentsInChildren<SpriteRenderer>().Min(r => r.sortingOrder);
            Assert.Less(shadow.Shadow.GetComponent<SpriteRenderer>().sortingOrder, lowestPart);
        }

        [Test(Description = "다시 Bind해도 그림자가 늘어나지 않는다 (재사용·재스폰)")]
        public void Rebind_DoesNotDuplicate()
        {
            var shadow = Spawn("Slime");

            shadow.Bind();
            shadow.Bind();

            Assert.AreEqual(1, _enemy.GetComponentsInChildren<Transform>(true).Count(t => t.name == EnemyShadow.ShadowName));
        }

        [Test(Description = "그림자는 몸 스프라이트의 아래쪽 끝(발끝) 가운데에 있고, 몸보다 좁다")]
        public void Shadow_SitsAtFeet()
        {
            var shadow = Spawn("Spider", y: 1f);
            float feet = VisualBounds.BottomY(Body, 0f);
            var renderers = Body.GetComponentsInChildren<SpriteRenderer>().Where(r => r.sprite != null).ToArray();
            var body = renderers[0].bounds;
            foreach (var r in renderers) { body.Encapsulate(r.bounds); }

            VisualBounds.GetVerticalExtents(Body, 0f, out float top, out float bottom);
            Assert.AreEqual(Mathf.Max(Body.position.y, body.max.y), top, 0.001f);
            Assert.AreEqual(body.min.y, bottom, 0.001f);

            Assert.AreEqual(feet, shadow.Shadow.position.y, 0.001f);
            Assert.AreEqual(body.center.x, shadow.Shadow.position.x, 0.001f);
            Assert.Less(shadow.Shadow.GetComponent<SpriteRenderer>().bounds.size.x, body.size.x);
        }

        [Test(Description = "앞뒤 정렬의 발끝은 그림자를 빼고 잰다")]
        public void DepthSort_IgnoresShadow()
        {
            Spawn("Golem");
            var withShadow = _enemy.AddComponent<EnemyDepthSort>();
            withShadow.Bind();
            int order = withShadow.Order;

            float feet = VisualBounds.BottomY(_enemy.GetComponentInChildren<SortingGroup>().transform, 0f);
            Assert.Less(feet, VisualBounds.BottomY(Body, 0f), "그림자를 넣으면 그룹 전체의 아래 끝은 더 낮아진다");
            Assert.AreEqual(EnemyDepthSort.OrderAt(VisualBounds.BottomY(Body, 0f)), order, "정렬은 몸의 발끝 기준");
        }

        [Test(Description = "피격 플래시는 그림자를 칠하지 않는다")]
        public void HitFlash_SkipsShadow()
        {
            var shadow = Spawn("Golem");
            var flash = _enemy.AddComponent<HitFlash>();

            flash.Flash();

            var renderers = (SpriteRenderer[])typeof(HitFlash).GetField("_renderers", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(flash);
            CollectionAssert.DoesNotContain(renderers, shadow.Shadow.GetComponent<SpriteRenderer>());
        }

        [Test(Description = "사망하면 그림자가 서서히 사라지고, 다시 Bind하면 원래 진하기로 돌아온다")]
        public void FadeOut_ThenRebindRestores()
        {
            var shadow = Spawn("Bat");
            var renderer = shadow.Shadow.GetComponent<SpriteRenderer>();
            float alpha = renderer.color.a;

            shadow.FadeOut(1f);
            var fade = typeof(EnemyShadow).GetField("_fade", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shadow);
            _anyTween = fade;
            Goto(fade, 0.5f);
            Assert.Less(renderer.color.a, alpha);
            Assert.Greater(renderer.color.a, 0f);
            Goto(fade, 1f);
            Assert.AreEqual(0f, renderer.color.a, 0.0001f);

            shadow.Bind();
            Assert.AreEqual(alpha, renderer.color.a, 0.0001f);
        }
    }
}
