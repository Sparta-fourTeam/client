using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    /// <summary>카드 선택창에서 후보가 3장보다 적어도 카드 한 장의 크기가 달라지지 않는지 지킨다.
    /// 칸을 균등 분배로 늘리면 후보가 1~2장일 때 카드가 화면 폭까지 퍼졌다</summary>
    public class CardSelectLayoutTests
    {
        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null)
            {
                Object.DestroyImmediate(_instance);
            }
        }

        private float[] SlotWidths(int visibleCount)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/CardSelect.prefab");
            _instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var root = (RectTransform)_instance.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(1080f, 1920f);

            RectTransform cards = null;
            foreach (var t in _instance.GetComponentsInChildren<RectTransform>(true))
            {
                if (t.name == "Cards")
                {
                    cards = t;
                }
            }

            Assert.IsNotNull(cards, "Cards를 찾지 못했다");
            for (int i = 0; i < cards.childCount; i++)
            {
                cards.GetChild(i).gameObject.SetActive(i < visibleCount);
            }

            // 패널이 꺼져 있어도 레이아웃은 계산되도록 켠다
            cards.gameObject.SetActive(true);
            for (var p = cards.parent; p != null; p = p.parent)
            {
                p.gameObject.SetActive(true);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(cards);

            var widths = new float[visibleCount];
            for (int i = 0; i < visibleCount; i++)
            {
                widths[i] = ((RectTransform)cards.GetChild(i)).rect.width;
            }

            return widths;
        }

        [Test(Description = "후보가 1장이든 2장이든 카드 폭이 3장일 때와 같다")]
        public void FewerCards_KeepSameWidthAsFull([Values(1, 2)] int count)
        {
            var full = SlotWidths(3);
            Object.DestroyImmediate(_instance);

            var few = SlotWidths(count);

            Assert.AreEqual(full[0], few[0], 0.5f);
        }
    }
}
