using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>새 컨텐츠(예: 하단 스킬 탭)가 열릴 때 보여주는 전체 화면 연출. 레벨업 화면보다 먼저 나온다.
    /// 자물쇠가 등장해 흔들리고 → 빛줄기가 좌우로 펼쳐지며 "새 컨텐츠 해금!" → 자물쇠가 열리며 번쩍이고 → 컨텐츠 이름과 아이콘이 튀어나온다 → "탭하여 계속".
    /// 연출 중 탭하면 연출이 바로 끝나고, 끝난 뒤 한 번 더 탭하면 닫힌다. 닫을 때까지 ShowAsync가 기다린다</summary>
    public sealed class ContentUnlockView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [Tooltip("화면 전체를 덮는 투명 버튼. 연출 중엔 스킵, 끝난 뒤엔 닫기")]
        [SerializeField] private Button _tapArea;
        [SerializeField] private Image _lock;
        [SerializeField] private Sprite _lockClosed;
        [SerializeField] private Sprite _lockOpen;
        [Tooltip("좌우로 펼쳐지는 빛줄기")]
        [SerializeField] private RectTransform _streak;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _contentName;
        [SerializeField] private Image _contentIcon;
        [SerializeField] private CanvasGroup _flash;
        [SerializeField] private CanvasGroup _tapHint;
        [Tooltip("자물쇠가 열리는 순간 재생하는 파티클 (UIParticle 프리팹의 ParticleSystem)")]
        [SerializeField] private ParticleSystem[] _burstFx;

        private const float BurstEmitSeconds = 0.1f;

        private UniTaskCompletionSource _closed;
        private Sequence _sequence;
        private Tween _hintBlink;
        private Tween _fxStop;
        private bool _playing;
        private IAudioService _audio;

        [Inject]
        public void Construct(IAudioService audio) => _audio = audio;

        public bool IsOpen => _panel.activeSelf;

        /// <summary>등장 연출이 아직 재생 중인가 (이때 탭하면 연출만 건너뛴다)</summary>
        public bool IsPlaying => _playing;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _tapArea.onClick.AddListener(OnTap);
        }

        public UniTask ShowAsync(string contentName, Sprite icon)
        {
            _contentName.text = contentName;
            _contentIcon.sprite = icon;
            _contentIcon.gameObject.SetActive(icon != null);
            _closed = new UniTaskCompletionSource();
            PopupPanel.Set(_panel, _transition, true);
            Play();
            return _closed.Task;
        }

        private void Play()
        {
            KillTweens();
            _playing = true;
            _lock.sprite = _lockClosed;
            _lock.transform.localScale = Vector3.zero;
            _lock.transform.localRotation = Quaternion.identity;
            _title.alpha = 0f;
            _contentName.transform.localScale = Vector3.zero;
            _contentIcon.transform.localScale = Vector3.zero;
            _streak.localScale = new Vector3(0f, 0.12f, 1f);
            SetAlpha(_flash, 0f);
            SetAlpha(_tapHint, 0f);
            StopFx();

            var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            seq.Append(_lock.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack));
            seq.Append(_streak.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutCubic));
            seq.Join(DOTween.To(() => _title.alpha, a => _title.alpha = a, 1f, 0.3f).SetDelay(0.15f));
            seq.Append(_lock.transform.DOPunchRotation(new Vector3(0f, 0f, 14f), 0.4f, 12));
            // 자물쇠가 열리는 순간
            float open = seq.Duration();
            seq.InsertCallback(open, () =>
            {
                _lock.sprite = _lockOpen;
                _audio?.PlaySfx(SoundIds.LevelUp);
                PlayFx();
            });
            seq.Insert(open, DOTween.To(() => _flash.alpha, a => _flash.alpha = a, 0.35f, 0.04f));
            seq.Insert(open + 0.04f, DOTween.To(() => _flash.alpha, a => _flash.alpha = a, 0f, 0.3f));
            seq.Insert(open, _lock.transform.DOPunchScale(Vector3.one * 0.3f, 0.35f, 6));
            seq.Append(_contentName.transform.DOScale(Vector3.one, 0.28f).SetEase(Ease.OutBack));
            seq.Append(_contentIcon.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack));
            seq.OnComplete(OnSequenceDone);
            _sequence = seq;
        }

        private void OnSequenceDone()
        {
            _playing = false;
            SetAlpha(_tapHint, 1f);
            _hintBlink = DOTween.To(() => _tapHint.alpha, a => _tapHint.alpha = a, 0.25f, 0.7f)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
        }

        private void OnTap()
        {
            if (_playing)
            {
                // 연출을 끝 상태로 건너뛴다 (콜백도 불려 자물쇠가 열린 상태가 된다)
                _sequence?.Complete(true);
                return;
            }

            Close();
        }

        // 반복 재생 + 짧은 방출 후 중단 (UIParticle에서 확실히 그려지는 방식)
        private void PlayFx()
        {
            foreach (var fx in _burstFx ?? System.Array.Empty<ParticleSystem>())
            {
                fx.Play(true);
            }

            _fxStop?.Kill();
            _fxStop = DOVirtual.DelayedCall(BurstEmitSeconds, () =>
            {
                foreach (var fx in _burstFx ?? System.Array.Empty<ParticleSystem>())
                {
                    fx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }, true).SetLink(gameObject);
        }

        private void StopFx()
        {
            foreach (var fx in _burstFx ?? System.Array.Empty<ParticleSystem>())
            {
                fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null)
            {
                group.alpha = alpha;
            }
        }

        private void Close()
        {
            KillTweens();
            StopFx();
            PopupPanel.Set(_panel, _transition, false);
            _closed?.TrySetResult();
        }

        // 이 뷰가 만든 트윈만 정리한다 (전역 트윈은 건드리지 않는다)
        private void KillTweens()
        {
            _sequence?.Kill();
            _sequence = null;
            _hintBlink?.Kill();
            _hintBlink = null;
            _fxStop?.Kill();
            _fxStop = null;
            _playing = false;
        }

        protected override void DisposeView()
        {
            KillTweens();
            _closed?.TrySetCanceled();
        }
    }
}
