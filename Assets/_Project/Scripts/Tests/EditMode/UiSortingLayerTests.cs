using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>전투 화면의 UI 캔버스는 Screen Space - Camera라서 월드 스프라이트와 같은 기준으로 정렬된다.
    /// UI를 Default보다 위의 "UI" 정렬 레이어에 두어야 정렬 순서가 큰 몬스터·이펙트가 UI 위로 올라오지 않는다 (#249)</summary>
    public sealed class UiSortingLayerTests
    {
        private const string UiLayer = "UI";

        [Test(Description = "UI 정렬 레이어가 있고 월드가 쓰는 Default보다 위에 그려진다")]
        public void UiLayer_IsAboveDefault()
        {
            Assert.AreNotEqual(0, SortingLayer.NameToID(UiLayer), "UI 정렬 레이어가 없다");
            Assert.Greater(SortingLayer.GetLayerValueFromName(UiLayer), SortingLayer.GetLayerValueFromName("Default"));
        }

        [Test(Description = "HUD 캔버스는 UI 정렬 레이어를 쓴다")]
        public void HudCanvas_UsesUiLayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/HudCanvas.prefab");
            Assert.IsNotNull(prefab);

            Assert.AreEqual(UiLayer, prefab.GetComponent<Canvas>().sortingLayerName);
        }
    }
}
