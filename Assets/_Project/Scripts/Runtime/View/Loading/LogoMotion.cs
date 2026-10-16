using DG.Tweening;
using UnityEngine;

namespace Game.View
{
    /// <summary>로고가 살아 있는 것처럼 보이게 한다. 켜질 때 튀어 오르며 나타난 뒤, 위아래로 둥실 떠다니고 살짝 기울며 숨 쉬듯 커졌다 작아진다.
    /// 모양은 그대로 두고 트위닝만 쓴다. 꺼지면 원래 자리로 돌려놓는다</summary>
    public sealed class LogoMotion : MonoBehaviour
    {
        [SerializeField] private float _floatDistance = 16f;
        [SerializeField] private float _floatSeconds = 1.8f;
        [SerializeField] private float _tiltDegrees = 1.8f;
        [SerializeField] private float _tiltSeconds = 2.6f;
        [SerializeField] private float _breathScale = 0.035f;
        [SerializeField] private float _breathSeconds = 1.4f;
        [SerializeField] private float _introSeconds = 0.7f;

        private RectTransform _rect;
        private Vector2 _basePosition;
        private Sequence _motion;

        private void OnEnable()
        {
            _rect = (RectTransform)transform;
            _basePosition = _rect.anchoredPosition;
            _rect.localScale = Vector3.one * 0.55f;
            _rect.localRotation = Quaternion.Euler(0f, 0f, -6f);

            _motion = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(_rect.DOScale(1f, _introSeconds).SetEase(Ease.OutBack))
                .Join(_rect.DOLocalRotate(Vector3.zero, _introSeconds).SetEase(Ease.OutBack))
                .AppendCallback(StartIdle);
        }

        private void StartIdle()
        {
            _rect.DOLocalMoveY(_rect.localPosition.y + _floatDistance, _floatSeconds)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject).SetId(this);
            _rect.DOLocalRotate(new Vector3(0f, 0f, _tiltDegrees), _tiltSeconds)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject).SetId(this);
            _rect.DOScale(1f + _breathScale, _breathSeconds)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject).SetId(this);
        }

        private void OnDisable()
        {
            _motion?.Kill();
            DOTween.Kill(this);
            if (_rect != null)
            {
                _rect.anchoredPosition = _basePosition;
                _rect.localScale = Vector3.one;
                _rect.localRotation = Quaternion.identity;
            }
        }
    }
}
