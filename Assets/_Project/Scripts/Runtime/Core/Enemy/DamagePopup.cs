using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Game.Core
{
    /// <summary>적이 받은 피해량을 그 자리에서 띄우는 숫자 하나. DOTween으로 위로 떠오르다가 사라진다.
    /// 다 보이면 Show에 넘긴 콜백을 불러 풀(DamagePopupPool)로 돌아간다</summary>
    public sealed class DamagePopup : MonoBehaviour
    {
        // 앞쪽 이 비율 동안은 그대로 두고 나머지 동안 투명해진다
        private const float HoldRatio = 0.5f;

        [SerializeField] private TMP_Text _text;
        [SerializeField, Min(0.05f)] private float _duration = 0.7f;
        [SerializeField] private float _riseDistance = 0.84f;

        private Color _baseColor;
        private bool _hasBaseColor;
        private Sequence _sequence;

        public int Amount { get; private set; }

        /// <summary>지금 화면에 쓰인 글자. 글자 컴포넌트가 없으면 빈 문자열</summary>
        public string DisplayedText => _text != null ? _text.text : string.Empty;

        /// <summary>지금 글자의 불투명도. 글자 컴포넌트가 없으면 0</summary>
        public float Alpha => _text != null ? _text.color.a : 0f;

        private void OnDestroy() => _sequence?.Kill();

        /// <summary>amount를 position에 띄우고 떠오르기 시작한다. 이미 보이는 중이었으면 처음부터 다시 한다.
        /// 다 보이면 onFinished를 부른다</summary>
        public void Show(int amount, Vector3 position, Action<DamagePopup> onFinished = null)
        {
            _sequence?.Kill();

            Amount = amount;
            transform.position = position;
            gameObject.SetActive(true);

            _sequence = DOTween.Sequence().SetLink(gameObject);
            _sequence.Append(transform.DOMoveY(position.y + _riseDistance, _duration).SetEase(Ease.OutCubic));

            if (_text != null)
            {
                // 처음 띄울 때의 색을 원래 색으로 기억한다 (Awake가 불리지 않는 편집 모드에서도 같게 동작한다)
                if (!_hasBaseColor)
                {
                    _baseColor = _text.color;
                    _hasBaseColor = true;
                }

                _text.SetText("{0}", amount);
                _text.color = _baseColor;
                float fadeSeconds = _duration * (1f - HoldRatio);
                _sequence.Insert(_duration * HoldRatio,
                    DOTween.ToAlpha(() => _text.color, c => _text.color = c, 0f, fadeSeconds).SetEase(Ease.Linear));
            }

            _sequence.OnComplete(() =>
            {
                _sequence = null;
                onFinished?.Invoke(this);
            });
        }
    }
}
