using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>HUD 조각 뒤로 적이 지나가면 그 조각을 반투명하게 해서 적이 가려지지 않게 한다.
    /// 적이 비켜 가면 잠깐 기다렸다가 천천히 원래대로 돌아와 깜빡이지 않는다. 판정은 화면 좌표에서 0.1초마다 한다.
    /// 웨이브 게이지가 가득 차 카드나 룰렛을 고르는 동안은 반투명하게 하지 않고 항상 또렷하게 보인다(WaveHudView의 가득 찬 표시와 같은 기준)</summary>
    [DisallowMultipleComponent]
    public sealed class HudAutoFade : MonoBehaviour
    {
        private const float CheckSeconds = 0.1f;
        private const float FadeOutPerSecond = 6f, FadeInPerSecond = 2.2f;
        private const float RestoreDelaySeconds = 0.5f;
        private const int MaxChecked = 64;

        [Tooltip("적이 이 영역과 겹치면 반투명해진다")]
        [SerializeField] private RectTransform _area;
        [SerializeField] private CanvasGroup _group;
        [Range(0f, 1f)][SerializeField] private float _dimmedAlpha = 0.35f;
        [Tooltip("영역을 이만큼(화면 픽셀) 넓혀서 적이 닿기 전에 미리 비킨다")]
        [SerializeField, Min(0f)] private float _marginPixels = 40f;

        private readonly List<IEnemyTarget> _targets = new();
        private readonly Vector3[] _corners = new Vector3[4];
        private IEnemyTargetProvider _enemies;
        private Canvas _canvas;
        private float _checkTimer, _restoreTimer;
        private bool _blocked, _selecting;
        private IDisposable _subscription;

        [Inject]
        public void Construct(IEnemyTargetProvider enemies, IBufferedSubscriber<StageStateChanged> state)
        {
            _enemies = enemies;
            _subscription?.Dispose();
            _subscription = state.Subscribe(OnStateChanged);
        }

        private void OnDestroy() => _subscription?.Dispose();

        // 카드·룰렛을 고르는 동안은 일시정지해도 그대로 또렷하게 둔다
        private void OnStateChanged(StageStateChanged message)
        {
            if (message.State is StageState.CardSelect or StageState.EliteRoulette) { _selecting = true; }
            else if (message.State != StageState.Paused) { _selecting = false; }
        }

        private void Update()
        {
            if (_group == null || _area == null || _enemies == null) { return; }

            _checkTimer -= Time.unscaledDeltaTime;
            if (_checkTimer <= 0f)
            {
                _checkTimer = CheckSeconds;
                bool blocked = IsBlocked();
                if (blocked) { _restoreTimer = RestoreDelaySeconds; }
                _blocked = blocked;
            }

            _restoreTimer -= Time.unscaledDeltaTime;
            bool dim = !_selecting && (_blocked || _restoreTimer > 0f);
            float target = dim ? _dimmedAlpha : 1f;
            float speed = dim ? FadeOutPerSecond : FadeInPerSecond;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, speed * Time.unscaledDeltaTime);
        }

        private bool IsBlocked()
        {
            var cam = Camera.main;
            if (cam == null) { return false; }

            if (_canvas == null) { _canvas = _area.GetComponentInParent<Canvas>(); }
            var uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

            _area.GetWorldCorners(_corners);
            var min = RectTransformUtility.WorldToScreenPoint(uiCam, _corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(uiCam, _corners[2]);
            var rect = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));

            var center = cam.ScreenToWorldPoint(new Vector3(rect.center.x, rect.center.y, -cam.transform.position.z));
            _enemies.GetNearest(new Vector2(center.x, center.y), MaxChecked, _targets);
            var padded = Rect.MinMaxRect(rect.xMin - _marginPixels, rect.yMin - _marginPixels, rect.xMax + _marginPixels, rect.yMax + _marginPixels);
            foreach (var target in _targets)
            {
                var screen = cam.WorldToScreenPoint(new Vector3(target.Position.x, target.Position.y, 0f));
                if (padded.Contains(screen)) { return true; }
            }

            return false;
        }
    }
}
