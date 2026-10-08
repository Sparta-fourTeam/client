using System.Collections.Generic;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    /// <summary>등장 요마·보상 목록이 칸이 많아져도 크기를 지키며 창 폭 안에서 줄바꿈되는지 지킨다.
    /// 한 줄 가로 배치였을 때는 칸이 많아지면 폭이 찌그러져 아이콘이 납작해졌다</summary>
    public class IconListWrapTests
    {
        private const float ListWidth = 820f; // PauseMenu의 Sections 폭
        private const float SlotSize = 110f; // 칸 프리팹의 기본 크기
        private const int ManyItems = 14;
        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null)
            {
                Object.DestroyImmediate(_instance);
            }
        }

        private GameObject Spawn(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            _instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var rt = (RectTransform)_instance.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(ListWidth, 0f);
            return _instance;
        }

        private static void AssertAllInside(GameObject root)
        {
            Canvas.ForceUpdateCanvases();
            // 루트 폭은 ListWidth로 고정돼 있어야 한다. 목록이 스스로 넓어지면 검사가 의미를 잃는다
            var rootRect = (RectTransform)root.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
            Assert.AreEqual(ListWidth, rootRect.rect.width, 0.5f);

            var slots = root.transform.Find("Slots");
            var corners = new Vector3[4];
            int visible = 0;
            foreach (Transform slot in slots)
            {
                if (!slot.gameObject.activeSelf)
                {
                    continue;
                }

                visible++;
                var size = ((RectTransform)slot).rect.size;
                Assert.GreaterOrEqual(size.x, SlotSize - 0.5f, $"{slot.name} 폭이 찌그러졌다");
                Assert.GreaterOrEqual(size.y, SlotSize - 0.5f, $"{slot.name} 높이가 찌그러졌다");
                ((RectTransform)slot).GetWorldCorners(corners);
                var local0 = rootRect.InverseTransformPoint(corners[0]);
                var local2 = rootRect.InverseTransformPoint(corners[2]);
                Assert.GreaterOrEqual(local0.x, -ListWidth / 2f - 0.5f, $"{slot.name} 왼쪽이 창 밖");
                Assert.LessOrEqual(local2.x, ListWidth / 2f + 0.5f, $"{slot.name} 오른쪽이 창 밖");
                Assert.GreaterOrEqual(local0.y, rootRect.rect.yMin - 0.5f, $"{slot.name} 아래가 목록 밖");
            }

            Assert.AreEqual(ManyItems, visible);
        }

        [Test(Description = "등장 요마가 많아도 모든 칸이 목록 폭 안에 들어온다")]
        public void MonsterList_ManyItems_StaysInsideWidth()
        {
            var root = Spawn("Assets/_Project/Prefabs/UI/MonsterList.prefab");
            var items = new List<StageMonsterItem>();
            for (int i = 0; i < ManyItems; i++)
            {
                items.Add(new StageMonsterItem(null, $"#{i}", i == 0));
            }

            root.GetComponent<StageMonsterListView>().Show(items);

            AssertAllInside(root);
        }

        [Test(Description = "보상이 많아도 모든 칸이 목록 폭 안에 들어온다")]
        public void RewardList_ManyItems_StaysInsideWidth()
        {
            var root = Spawn("Assets/_Project/Prefabs/UI/RewardList.prefab");
            var items = new List<ResultRewardItem>();
            for (int i = 0; i < ManyItems; i++)
            {
                items.Add(new ResultRewardItem(null, 10 + i));
            }

            root.GetComponent<ResultRewardListView>().Show(items);

            AssertAllInside(root);
        }
    }
}
