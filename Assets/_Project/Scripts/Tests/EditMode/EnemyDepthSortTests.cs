using Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Tests
{
    public sealed class EnemyDepthSortTests
    {
        private Texture2D _texture;
        private Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4);
            _sprite = Sprite.Create(_texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f); // 1x1 유닛, 중심 피벗
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sprite);
            Object.DestroyImmediate(_texture);
        }

        // 적 루트 아래 Visual(SortingGroup) + 스프라이트 한 장. 스프라이트 아래쪽 끝(발끝)은 루트보다 halfHeight만큼 아래다
        private EnemyDepthSort Enemy(float y, float halfHeight = 0.5f)
        {
            var root = new GameObject("Enemy");
            root.transform.position = new Vector3(0f, y, 0f);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<SortingGroup>();
            var body = new GameObject("Body");
            body.transform.SetParent(visual.transform, false);
            body.transform.localScale = Vector3.one * (halfHeight * 2f);
            body.AddComponent<SpriteRenderer>().sprite = _sprite;
            var sort = root.AddComponent<EnemyDepthSort>();
            sort.Bind();
            return sort;
        }

        [Test(Description = "화면 아래쪽 적이 위쪽 적보다 앞에 그려지고, 움직여 위치가 바뀌면 앞뒤도 바뀐다")]
        public void LowerEnemy_DrawsInFront_AndFollowsMovement()
        {
            var upper = Enemy(2f);
            var lower = Enemy(0f);
            Assert.Greater(lower.Order, upper.Order);

            upper.transform.position = new Vector3(0f, -1f, 0f);
            upper.Sync();

            Assert.Greater(upper.Order, lower.Order);
            Object.DestroyImmediate(upper.gameObject);
            Object.DestroyImmediate(lower.gameObject);
        }

        [Test(Description = "기준은 루트가 아니라 발끝이다: 루트가 같아도 키가 큰(발끝이 낮은) 적이 앞")]
        public void SortsByFeet_NotRoot()
        {
            var small = Enemy(0f, halfHeight: 0.2f);
            var tall = Enemy(0f, halfHeight: 0.6f);

            Assert.Greater(tall.Order, small.Order);
            Object.DestroyImmediate(small.gameObject);
            Object.DestroyImmediate(tall.gameObject);
        }

        [Test(Description = "SortingGroup이 없는 적은 아무것도 바꾸지 않는다")]
        public void WithoutSortingGroup_DoesNothing()
        {
            var root = new GameObject("Enemy");
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 7;
            var sort = root.AddComponent<EnemyDepthSort>();

            sort.Bind();
            sort.Sync();

            Assert.AreEqual(7, renderer.sortingOrder);
            Object.DestroyImmediate(root);
        }

        [Test(Description = "실제 몬스터 프리팹의 비주얼 SortingGroup에도 적용된다")]
        public void RealMonsterPrefab_GetsDepthOrder()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Stage/Golem.prefab");
            var golem = Object.Instantiate(prefab);
            golem.transform.position = new Vector3(0f, 1f, 0f);
            var sort = golem.AddComponent<EnemyDepthSort>();

            sort.Bind();

            var group = golem.GetComponentInChildren<SortingGroup>();
            Assert.AreEqual(sort.Order, group.sortingOrder);
            Assert.That(group.sortingOrder, Is.InRange(EnemyDepthSort.BackOrder, EnemyDepthSort.FrontOrder));
            Object.DestroyImmediate(golem);
        }
    }
}
