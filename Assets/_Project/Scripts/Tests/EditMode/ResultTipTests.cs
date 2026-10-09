using System.Reflection;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

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

        [Test(Description = "패널을 누르면 닫힌다")]
        public void Click_Closes()
        {
            _tip.Show();

            _tip.OnPointerClick(new PointerEventData(null));

            Assert.IsFalse(_tip.IsShown);
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
