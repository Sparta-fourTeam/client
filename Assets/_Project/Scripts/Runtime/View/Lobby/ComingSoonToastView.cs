using System.Collections;
using TMPro;
using UnityEngine;

namespace Game.View
{
    /// <summary>아직 없는 기능을 눌렀을 때 "미완성입니다." 안내를 잠깐 띄운다. 다른 문구(잠긴 기능 안내 등)도 띄울 수 있다</summary>
    public sealed class ComingSoonToastView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private float _duration = 1.5f;

        private Coroutine _hide;
        private string _defaultMessage;

        private void Awake()
        {
            _panel.SetActive(false);
            _defaultMessage = _message != null ? _message.text : null;
        }

        /// <summary>기본 문구("미완성입니다.")를 띄운다</summary>
        public void Show() => Show(_defaultMessage);

        public void Show(string message)
        {
            if (_message != null)
            {
                _message.text = message;
            }

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
