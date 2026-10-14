using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Game.View
{
    /// <summary>안내를 잠깐 표시한다. 입력을 막지 않고 unscaled 시간으로 등장·퇴장한다.</summary>
    public sealed class ComingSoonToastView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private float _duration = 1.5f;

        private Sequence _animation;
        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _home;
        private Vector3 _scale;
        private string _defaultMessage;

        private void Awake()
        {
            _panel.SetActive(false);
            _defaultMessage = _message != null ? _message.text : null;
            PrepareAnimation();
        }

        private void PrepareAnimation()
        {
            if (_rect != null) { return; }
            _rect = (RectTransform)_panel.transform;
            _home = _rect.anchoredPosition;
            _scale = _rect.localScale;
            _group = _panel.GetComponent<CanvasGroup>();
            if (_group == null) { _group = _panel.AddComponent<CanvasGroup>(); }
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        public void Show() => Show(_defaultMessage);
        public void Show(string message) => Show(message, _duration);

        public void Show(string message, float duration)
        {
            PrepareAnimation();
            _animation?.Kill();
            if (_message != null) { _message.text = message; }
            _rect.anchoredPosition = _home - Vector2.up * 24f;
            _rect.localScale = _scale * 0.94f;
            _group.alpha = 0f;
            _panel.SetActive(true);

            _animation = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _animation.Append(DOTween.To(() => _rect.anchoredPosition, value => _rect.anchoredPosition = value, _home, 0.24f).SetEase(Ease.OutCubic));
            _animation.Join(_rect.DOScale(_scale, 0.24f).SetEase(Ease.OutBack, 1.2f));
            _animation.Join(DOTween.To(() => _group.alpha, value => _group.alpha = value, 1f, 0.18f));
            _animation.AppendInterval(Mathf.Max(0f, duration - 0.24f));
            _animation.Append(DOTween.To(() => _group.alpha, value => _group.alpha = value, 0f, 0.18f));
            _animation.Join(DOTween.To(() => _rect.anchoredPosition, value => _rect.anchoredPosition = value, _home + Vector2.up * 12f, 0.18f).SetEase(Ease.InCubic));
            _animation.OnComplete(() => _panel.SetActive(false));
        }

        private void OnDisable()
        {
            _animation?.Kill();
            _panel.SetActive(false);
        }

        private void OnDestroy() => _animation?.Kill();
    }
}
