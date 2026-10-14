using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Game.Core
{
    /// <summary>적이 받은 피해량을 그 자리에서 띄우는 숫자 하나. DOTween으로 위로 떠오르다가 사라진다.
    /// 종류(DamagePopupKind)마다 글자 머티리얼(색·외곽선)과 움직임이 다르다. 치명타는 크게 튕기며 "!"가 붙고 뒤에서 반짝이가 터지고,
    /// 상태이상 피해는 작고 느리게 뜬다. 다 보이면 Show에 넘긴 콜백을 불러 풀(DamagePopupPool)로 돌아간다</summary>
    public sealed class DamagePopup : MonoBehaviour
    {
        // 앞쪽 이 비율 동안은 그대로 두고 나머지 동안 투명해진다
        private const float HoldRatio = 0.5f;

        // 종류별 크기 배율·시간 배율·떠오르는 거리 배율
        private const float NormalScale = 1f, CriticalScale = 1.2f, StatusScale = 0.78f;
        private const float CriticalDurationRatio = 1.3f, StatusDurationRatio = 1.25f;
        private const float CriticalRiseRatio = 1.2f, StatusRiseRatio = 0.7f;
        private const float CriticalPunchSeconds = 0.14f, BurstSeconds = 0.36f, BurstEndScale = 1.25f;
        private const float StatusStartAlpha = 0.9f;
        // 강조 선은 맞은 몬스터의 어깨 쪽(가로로 좌우 무작위, 세로로 위)에 뜨고 바깥으로 퍼진다
        private static readonly Vector2 BurstOffset = new Vector2(0.42f, 0.18f);

        [SerializeField] private TMP_Text _text;
        [SerializeField, Min(0.05f)] private float _duration = 0.7f;
        [SerializeField] private float _riseDistance = 0.84f;

        [Header("종류별 글자 머티리얼 (비우면 프리팹의 것을 그대로 쓴다)")]
        [SerializeField] private Material _normalMaterial;
        [SerializeField] private Material _criticalMaterial;
        [SerializeField] private Material _statusMaterial;
        [Tooltip("치명타일 때만 글자 옆에서 튀어나오는 강조 선. 비우면 쓰지 않는다")]
        [SerializeField] private SpriteRenderer _burst;
        [Tooltip("치명타일 때만 글자 뒤에서 번지는 빛. 비우면 쓰지 않는다")]
        [SerializeField] private SpriteRenderer _glow;

        private Color _baseColor;
        private Vector3 _burstBaseScale = Vector3.one, _glowBaseScale = Vector3.one;
        private float _glowBaseAlpha = 1f;
        private bool _hasBurstBase, _hasGlowBase;
        private Vector3 _burstWorld;
        private bool _hasBaseColor;
        private Sequence _sequence;

        public int Amount { get; private set; }
        public DamagePopupKind Kind { get; private set; }

        /// <summary>지금 화면에 쓰인 글자. 글자 컴포넌트가 없으면 빈 문자열</summary>
        public string DisplayedText => _text != null ? _text.text : string.Empty;

        /// <summary>지금 글자의 불투명도. 글자 컴포넌트가 없으면 0</summary>
        public float Alpha => _text != null ? _text.color.a : 0f;

        private void OnDestroy() => _sequence?.Kill();

        /// <summary>일반 피해로 띄운다</summary>
        public void Show(int amount, Vector3 position, Action<DamagePopup> onFinished = null) =>
            Show(amount, position, DamagePopupKind.Normal, onFinished);

        /// <summary>amount를 position에 kind의 모양으로 띄우고 떠오르기 시작한다. 이미 보이는 중이었으면 처음부터 다시 한다.
        /// hitPoint는 맞은 몬스터의 중심이다. 치명타의 강조 선이 거기에 뜬다(없으면 숫자 옆에 뜬다). 다 보이면 onFinished를 부른다</summary>
        public void Show(int amount, Vector3 position, DamagePopupKind kind, Action<DamagePopup> onFinished = null, Vector3? hitPoint = null)
        {
            _sequence?.Kill();

            Amount = amount;
            Kind = kind;
            transform.position = position;
            gameObject.SetActive(true);

            bool critical = kind == DamagePopupKind.Critical;
            bool status = kind == DamagePopupKind.Status;
            float duration = _duration * (critical ? CriticalDurationRatio : status ? StatusDurationRatio : 1f);
            float rise = _riseDistance * (critical ? CriticalRiseRatio : status ? StatusRiseRatio : 1f);
            float scale = critical ? CriticalScale : status ? StatusScale : NormalScale;

            _sequence = DOTween.Sequence().SetLink(gameObject);
            _sequence.Append(transform.DOMoveY(position.y + rise, duration).SetEase(Ease.OutCubic));

            // 크기: 치명타는 작게 시작해 튀어 오르고, 나머지는 처음부터 제 크기다
            if (critical)
            {
                transform.localScale = Vector3.one * (scale * 0.55f);
                _sequence.Insert(0f, transform.DOScale(scale, CriticalPunchSeconds).SetEase(Ease.OutBack, 2.4f));
            }
            else
            {
                transform.localScale = Vector3.one * scale;
            }

            ShowBurst(critical, hitPoint ?? position);

            if (_text != null)
            {
                // 처음 띄울 때의 색을 원래 색으로 기억한다 (Awake가 불리지 않는 편집 모드에서도 같게 동작한다)
                if (!_hasBaseColor)
                {
                    _baseColor = _text.color;
                    _hasBaseColor = true;
                }

                var material = critical ? _criticalMaterial : status ? _statusMaterial : _normalMaterial;
                if (material != null) { _text.fontSharedMaterial = material; }

                _text.SetText(critical ? "{0}!" : "{0}", amount);
                var color = _baseColor;
                color.a = status ? _baseColor.a * StatusStartAlpha : _baseColor.a;
                _text.color = color;
                float fadeSeconds = duration * (1f - HoldRatio);
                _sequence.Insert(duration * HoldRatio,
                    DOTween.ToAlpha(() => _text.color, c => _text.color = c, 0f, fadeSeconds).SetEase(Ease.Linear));
            }

            _sequence.OnComplete(() =>
            {
                _sequence = null;
                onFinished?.Invoke(this);
            });
        }

        // 치명타일 때만 글자 뒤 빛을 한 번 부풀리며 사라지게 한다
        private void ShowGlow(bool on)
        {
            if (_glow == null) { return; }

            _glow.gameObject.SetActive(on);
            if (!on) { return; }

            var t = _glow.transform;
            if (!_hasGlowBase) { _glowBaseScale = t.localScale; _glowBaseAlpha = _glow.color.a; _hasGlowBase = true; }
            t.localScale = _glowBaseScale * 0.6f;
            var c = _glow.color; c.a = _glowBaseAlpha; _glow.color = c;
            _sequence.Insert(0f, t.DOScale(_glowBaseScale * 1.2f, BurstSeconds * 1.4f).SetEase(Ease.OutCubic));
            _sequence.Insert(BurstSeconds * 0.3f, DOTween.ToAlpha(() => _glow.color, x => _glow.color = x, 0f, BurstSeconds * 1.1f));
        }

        // 치명타일 때만 글자 뒤 반짝이를 튀어나오며 투명해지게 한다. 다른 종류에서는 꺼 둔다
        private void ShowBurst(bool on, Vector3 anchor)
        {
            ShowGlow(on);
            if (_burst == null) { return; }

            _burst.gameObject.SetActive(on);
            if (!on) { return; }

            var t = _burst.transform;
            if (!_hasBurstBase) { _burstBaseScale = t.localScale; _hasBurstBase = true; }

            // 좌우 중 한쪽을 골라 몬스터 어깨 쪽에 둔다. 스프라이트는 오른쪽 아래 원점에서 왼쪽 위로 퍼지므로 왼쪽은 그대로, 오른쪽은 뒤집어 항상 바깥으로 퍼지게 한다
            float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            _burst.flipX = side > 0f;
            _burstWorld = anchor + new Vector3(BurstOffset.x * side, BurstOffset.y, 0f);
            t.position = _burstWorld;
            // 숫자는 떠오르지만 강조 선은 맞은 자리에 남는다
            _sequence.OnUpdate(() => { if (_burst != null) { _burst.transform.position = _burstWorld; } });
            t.localScale = _burstBaseScale * 0.5f;
            var c = _burst.color; c.a = 1f; _burst.color = c;
            _sequence.Insert(0f, t.DOScale(_burstBaseScale * BurstEndScale, BurstSeconds).SetEase(Ease.OutCubic));
            _sequence.Insert(BurstSeconds * 0.4f, DOTween.ToAlpha(() => _burst.color, x => _burst.color = x, 0f, BurstSeconds * 0.6f));
        }
    }
}
