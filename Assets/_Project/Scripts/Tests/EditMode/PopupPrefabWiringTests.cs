using System.Reflection;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>팝업 프리팹마다 PopupTransition이 패널에 붙어 있고 뷰에 연결되어 있는지 확인한다.
    /// 연결이 빠지면 컴파일도 테스트도 통과하지만 전환만 조용히 사라지기 때문이다</summary>
    public sealed class PopupPrefabWiringTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

        [TestCase("PauseMenu", typeof(PauseMenuView))]
        [TestCase("ResultPopup", typeof(ResultPopupView))]
        [TestCase("EquipUpgradePopup", typeof(EquipUpgradePopupView))]
        [TestCase("SkillUpgradePopup", typeof(SkillUpgradePopupView))]
        [TestCase("CardSelect", typeof(CardSelectView))]
        [TestCase("EnergyRecoverPopup", typeof(EnergyRecoverPopupView))]
        public void PopupPrefab_HasTransitionOnItsPanel_AndViewReferencesIt(string prefabName, System.Type viewType)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/UI/{prefabName}.prefab");
            Assert.IsNotNull(prefab, prefabName);
            var view = prefab.GetComponentInChildren(viewType, true);
            Assert.IsNotNull(view, $"{prefabName}: 뷰가 없다");

            var panel = (GameObject)viewType.GetField("_panel", Flags).GetValue(view);
            var transition = (PopupTransition)viewType.GetField("_transition", Flags).GetValue(view);

            Assert.IsNotNull(transition, $"{prefabName}: 뷰의 _transition이 비어 있다");
            Assert.AreSame(panel, transition.gameObject, $"{prefabName}: PopupTransition이 패널(_panel)에 붙어 있어야 한다");
        }
    }
}
