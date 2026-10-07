using System.Reflection;
using Game.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class ResultStarsViewTests
    {
        private GameObject _root;
        private ResultStarsView _view;
        private Image[] _stars;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Stars");
            _view = _root.AddComponent<ResultStarsView>();
            _stars = new Image[3];
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i] = new GameObject($"Star{i + 1}", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                _stars[i].transform.SetParent(_root.transform);
            }

            typeof(ResultStarsView).GetField("_stars", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_view, _stars);
            _root.SetActive(false);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        private int LitCount()
        {
            var lit = (Color)typeof(ResultStarsView).GetField("_litColor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_view);
            int count = 0;
            foreach (var star in _stars) { if (star.color == lit) { count++; } }
            return count;
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Clear_ShowsStarsLitByRating(int rating)
        {
            _view.Show(true, rating);

            Assert.IsTrue(_root.activeSelf);
            Assert.AreEqual(rating, LitCount());
        }

        [Test(Description = "실패하면 별 영역이 보이지 않는다")]
        public void Fail_HidesStars()
        {
            _view.Show(true, 3);

            _view.Show(false, 0);

            Assert.IsFalse(_root.activeSelf);
        }
    }
}
