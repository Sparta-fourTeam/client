using System.Collections;
using UnityEngine;

namespace Game.View
{
    /// <summary>아직 없는 기능을 눌렀을 때 "미완성입니다." 안내를 잠깐 띄운다</summary>
    public sealed class ComingSoonToastView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private float _duration = 1.5f;

        private Coroutine _hide;

        private void Awake()
        {
            _panel.SetActive(false);
        }

        public void Show()
        {
            _panel.SetActive(true);
            if (_hide != null)
            {
                StopCoroutine(_hide);
            }

            _hide = StartCoroutine(HideLater());
        }

        // timeScale과 상관없이 사라지도록 unscaled 시간으로 센다
        private IEnumerator HideLater()
        {
            yield return new WaitForSecondsRealtime(_duration);
            _panel.SetActive(false);
            _hide = null;
        }
    }
}
