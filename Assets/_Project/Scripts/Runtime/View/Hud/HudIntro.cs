using System;
using DG.Tweening;
using UnityEngine;

namespace Game.View
{
    public enum HudIntroFrom
    {
        Left,
        Right,
        Top,
        Bottom,
        None,
    }

    /// <summary>전투가 시작될 때 HUD 조각이 가장자리에서 들어오는 등장 연출. 전투는 기다리지 않고 바로 시작한다.
    /// 일반 조각은 화면 밖에서 미끄러져 들어와 자리에 안착하고, 줄에 매달린 조각(Hanging)은 위에서 툭 떨어진 뒤
    /// 줄이 매인 점을 중심으로 흔들리다 가라앉는다. 레이아웃 그룹 안의 조각은 위치 대신 커지며(ScaleIn) 나타난다.
    /// 시간은 timeScale과 상관없이(unscaled) 흐르고, 연출이 끝나면 프리팹에 저장된 자리와 정확히 같아진다</summary>
    [DisallowMultipleComponent]
    public sealed class HudIntro : MonoBehaviour
    {
        [Serializable]
        public sealed class Item
        {
            public RectTransform Target;
            [Tooltip("들어오는 방향. 줄에 매달린 조각은 항상 위에서 떨어진다")]
            public HudIntroFrom From = HudIntroFrom.None;
            [Min(0f)] public float Delay;
            [Min(0f)] public float Distance = 240f;
            [Tooltip("레이아웃 그룹이 위치를 정하는 조각. 이동 대신 0에서 커지며 나타난다")]
            public bool ScaleIn;
            [Tooltip("안착하는 순간 한 번 튕기는 크기 (0이면 없음)")]
            [Range(0f, 0.5f)] public float Punch;
            [Header("줄에 매달린 조각")]
            public bool Hanging;
            [Tooltip("조각 위 끝에서 줄이 매인 점까지의 길이")]
            [Min(0f)] public float StringLength = 40f;
            [Range(0f, 40f)] public float SwingDegrees = 12f;
            [Tooltip("한 번 왕복하는 시간 (짧을수록 가볍다)")]
            [Min(0.2f)] public float SwingPeriod = 0.9f;
        }

        private const float SlideSeconds = 0.42f;
        private const float DropSeconds = 0.3f;
        private const float SwingSeconds = 2.4f;
        private const float SwingDecay = 2.2f;
        // 씬 전환 커튼이 이 정도까지 걷히면 연출을 시작한다 (다 걷힐 때까지 기다리면 첫 움직임을 놓친다)
        private const float CurtainOpenEnough = 0.55f;

        [SerializeField] private Item[] _items = Array.Empty<Item>();
        [Tooltip("켜면 오브젝트가 처음 시작될 때 자동으로 재생한다")]
        [SerializeField] private bool _playOnStart = true;

        private Vector2[] _home;
        private Vector3[] _homeScale;
        private Sequence _sequence;
        private TransitionCurtain _curtain;
        private bool _waitingForCurtain;

        private void Start()
        {
            if (_playOnStart)
            {
                Play();
            }
        }

        private void OnDestroy() => _sequence?.Kill();

        // 씬 전환 커튼에 가려진 동안 연출이 끝나 버리지 않도록, 커튼이 걷히기 시작할 때 재생한다
        private void Update()
        {
            if (_waitingForCurtain && (_curtain == null || _curtain.Coverage < CurtainOpenEnough))
            {
                _waitingForCurtain = false;
                _sequence?.Play();
            }
        }

        /// <summary>처음부터 다시 재생한다. 진행 중이던 연출은 자리로 돌려놓고 끊는다</summary>
        public void Play()
        {
            try
            {
                Begin();
            }
            catch (Exception error)
            {
                // 연출이 실패해도 HUD가 화면 밖에 남지 않게 모두 제자리로 돌려놓는다
                Debug.LogException(error, this);
                Stop();
            }
        }

        private void Begin()
        {
            CaptureHome();
            Stop();
            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            for (int i = 0; i < _items.Length; i++)
            {
                var item = _items[i];
                if (item?.Target == null) { continue; }
                _sequence.Insert(item.Delay, Build(item, _home[i], _homeScale[i]));
            }

            // 조각은 이미 화면 밖에 있으므로, 커튼이 덮고 있다면 멈춰 둔 채 기다린다
            _curtain = FindAnyObjectByType<TransitionCurtain>();
            _waitingForCurtain = _curtain != null && _curtain.Coverage >= CurtainOpenEnough;
            if (_waitingForCurtain)
            {
                _sequence.Pause();
            }
        }

        /// <summary>연출을 끊고 모든 조각을 프리팹에 저장된 자리로 돌려놓는다</summary>
        public void Stop()
        {
            _waitingForCurtain = false;
            _sequence?.Kill();
            _sequence = null;
            if (_home == null) { return; }
            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i]?.Target == null) { continue; }
                _items[i].Target.anchoredPosition = _home[i];
                _items[i].Target.localRotation = Quaternion.identity;
                _items[i].Target.localScale = _homeScale[i];
            }
        }

        // 자리는 처음 한 번만 읽는다. 연출 도중의 위치를 자리로 착각하지 않게 한다
        private void CaptureHome()
        {
            if (_home != null) { return; }
            _home = new Vector2[_items.Length];
            _homeScale = new Vector3[_items.Length];
            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i]?.Target == null) { continue; }
                _home[i] = _items[i].Target.anchoredPosition;
                _homeScale[i] = _items[i].Target.localScale;
            }
        }

        private static Tween Build(Item item, Vector2 home, Vector3 homeScale)
        {
            var rect = item.Target;
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(rect.gameObject);
            if (item.Hanging)
            {
                BuildHanging(seq, item, home);
                return seq;
            }

            if (item.ScaleIn)
            {
                Action hide = () => rect.localScale = Vector3.zero;
                hide();   // 지연 시간 동안에도 보이지 않게 지금 바로 숨긴다
                seq.AppendCallback(() => hide());
                seq.Append(rect.DOScale(homeScale, 0.3f).SetEase(Ease.OutBack));
                return seq;
            }

            var offset = item.From switch
            {
                HudIntroFrom.Left => new Vector2(-item.Distance, 0f),
                HudIntroFrom.Right => new Vector2(item.Distance, 0f),
                HudIntroFrom.Top => new Vector2(0f, item.Distance),
                HudIntroFrom.Bottom => new Vector2(0f, -item.Distance),
                _ => Vector2.zero,
            };
            rect.anchoredPosition = home + offset;   // 지연 시간 동안에도 화면 밖에 있게 지금 바로 옮긴다
            seq.AppendCallback(() => rect.anchoredPosition = home + offset);
            seq.Append(DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, home, SlideSeconds).SetEase(Ease.OutBack, 0.9f));
            if (item.Punch > 0f)
            {
                seq.Append(rect.DOPunchScale(Vector3.one * item.Punch, 0.25f, 6));
            }

            return seq;
        }

        // 위에서 툭 떨어져 멈추는 순간 줄이 당겨져 흔들린다. 흔들림은 줄이 매인 점을 중심으로 한 감쇠 진동이다
        private static void BuildHanging(Sequence seq, Item item, Vector2 home)
        {
            var rect = item.Target;
            float drop = rect.rect.height + item.StringLength + 120f;
            var topCenter = home + new Vector2((0.5f - rect.pivot.x) * rect.rect.width, (1f - rect.pivot.y) * rect.rect.height);
            var anchor = topCenter + new Vector2(0f, item.StringLength);
            float omega = Mathf.PI * 2f / item.SwingPeriod;

            Action lift = () =>
            {
                rect.anchoredPosition = home + new Vector2(0f, drop);
                rect.localRotation = Quaternion.identity;
            };
            lift();   // 지연 시간 동안에도 화면 밖에 있게 지금 바로 올려 둔다
            seq.AppendCallback(() => lift());
            seq.Append(DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, home, DropSeconds).SetEase(Ease.InQuad));
            seq.Append(DOTween.To(() => 0f, time =>
            {
                float angle = item.SwingDegrees * Mathf.Exp(-SwingDecay * time) * Mathf.Sin(omega * time);
                var rotation = Quaternion.Euler(0f, 0f, angle);
                rect.anchoredPosition = anchor + (Vector2)(rotation * (home - anchor));
                rect.localRotation = rotation;
            }, SwingSeconds, SwingSeconds).SetEase(Ease.Linear));
            seq.AppendCallback(() =>
            {
                rect.anchoredPosition = home;
                rect.localRotation = Quaternion.identity;
            });
        }
    }
}
