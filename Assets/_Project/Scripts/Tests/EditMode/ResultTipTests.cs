using System.Reflection;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Tests
{
    /// <summary>실패하면 결과 위에 겹쳐 뜨는 "성장을 위한 TIP!" 패널(#83). 실제 ResultPopup 프리팹으로 확인한다</summary>
    public sealed class ResultTipTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

        private GameObject _root;
        private ResultPopupView _popup;
        private ResultTipView _tip;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/ResultPopup.prefab");
            _root = Object.Instantiate(prefab);
            _popup = _root.GetComponentInChildren<ResultPopupView>(true);
            _tip = (ResultTipView)typeof(ResultPopupView).GetField("_tip", Flags).GetValue(_popup);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        private void ShowTip(bool cleared) =>
            typeof(ResultPopupView).GetMethod("ShowTip", Flags).Invoke(_popup, new object[] { cleared });

        [Test(Description = "결과 팝업에 TIP 패널이 연결돼 있고, 처음에는 꺼져 있고, 패널 배경이 클릭을 받으며 버튼이 아니다")]
        public void Prefab_TipIsWiredAndHidden()
        {
            Assert.IsNotNull(_tip);
            Assert.IsFalse(_tip.gameObject.activeSelf);
            Assert.IsTrue(_tip.GetComponent<Image>().raycastTarget, "패널 아무 곳이나 눌러 닫도록 배경이 클릭을 받는다");
            Assert.IsEmpty(_tip.GetComponentsInChildren<Button>(true), "버튼이 아니라서 눌림 효과(작아짐)가 적용되지 않는다");
        }

        [Test(Description = "패널을 누르면 닫힌다")]
        public void Click_Closes()
        {
            _tip.Show();

            _tip.OnPointerClick(new PointerEventData(null));

            Assert.IsFalse(_tip.IsShown);
        }

        [Test(Description = "TIP 카드는 레퍼런스의 문구 3개다")]
        public void Prefab_HasThreeTipCards()
        {
            // 테스트 어셈블리는 TextMeshPro를 참조하지 않으므로 글자 컴포넌트의 text를 이름으로 읽는다
            var tips = _tip.transform.Find("Card/Tips");
            var texts = new string[tips.childCount];
            for (int i = 0; i < tips.childCount; i++)
            {
                var label = tips.GetChild(i).Find("Text").GetComponent("TextMeshProUGUI");
                texts[i] = (string)label.GetType().GetProperty("text").GetValue(label);
            }

            CollectionAssert.AreEqual(new[] { "이전 단계 관문에\n재도전", "장비 강화\n장식품 합성", "인술 레벨업" }, texts);
        }

        [Test(Description = "실패하면 TIP이 뜨고 클리어하면 뜨지 않는다")]
        public void Tip_ShowsOnlyOnFail()
        {
            ShowTip(cleared: false);
            Assert.IsTrue(_tip.IsShown);

            ShowTip(cleared: true);
            Assert.IsFalse(_tip.IsShown);
        }

        [Test(Description = "누르면 닫히고 Closed가 한 번만 발생한다")]
        public void Close_HidesAndNotifiesOnce()
        {
            int closed = 0;
            _tip.Closed += () => closed++;
            _tip.Show();

            _tip.Close();
            _tip.Close();

            Assert.IsFalse(_tip.IsShown);
            Assert.AreEqual(1, closed);
        }
    }
}
