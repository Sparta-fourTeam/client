using System.Linq;
using System.Reflection;
using DG.Tweening;
using Game.View;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class RewardPopupTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);

        [Test]
        public void Reuse_FromStageToMail_ReplacesRowsAndTextAndResetsScroll()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/RewardPopup.prefab"));
            var canvasRoot = new GameObject("RewardPopupTestCanvas", typeof(RectTransform), typeof(Canvas));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.transform.SetParent(canvasRoot.transform, false);
            try
            {
                var popup = root.GetComponent<RewardPopupView>();
                var many = Enumerable.Range(1, 40).Select(i => new ResultRewardItem(null, i)).ToArray();
                popup.Show(many, "관문 보상을 받았어요.");
                var slots = Get<ResultRewardListView>(popup, "_rewards").transform.Find("Slots");
                Assert.AreEqual(40, slots.Cast<Transform>().Count(t => t.gameObject.activeSelf));
                Get<ScrollRect>(popup, "_scroll").verticalNormalizedPosition = 0;
                popup.Close();

                popup.Show(new[] { new ResultRewardItem(null, 50) }, "우편 보상을 받았어요.", "우편 수령 완료");
                Assert.AreEqual(1, slots.Cast<Transform>().Count(t => t.gameObject.activeSelf));
                Assert.AreEqual("x50", slots.GetChild(0).Find("Count").GetComponent<TMP_Text>().text);
                Assert.AreEqual("우편 수령 완료", Get<TMP_Text>(popup, "_title").text);
                Assert.AreEqual("우편 보상을 받았어요.", Get<TMP_Text>(popup, "_description").text);
                Assert.AreEqual(0f, Get<ScrollRect>(popup, "_scroll").content.anchoredPosition.y, 0.01f);
                Assert.IsTrue(Get<GameObject>(popup, "_panel").activeSelf);

                popup.Show(many);
                var scroll = Get<ScrollRect>(popup, "_scroll");
                Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
                Assert.AreEqual(1f, scroll.verticalNormalizedPosition, 0.01f);
            }
            finally
            {
                DOTween.KillAll();
                Object.DestroyImmediate(canvasRoot);
            }
        }
    }
}
