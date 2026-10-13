using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>튜토리얼 대사 장면. 화자 초상이 옆에서 미끄러져 들어오고, 이름표와 대사가 타자기처럼 나온다.
    /// 탭하면 지금 줄을 끝까지 보여주고, 한 번 더 탭하면 다음 줄로 간다. SKIP은 확인창에서 건너뛰기를 고르면 남은 줄을 모두 건너뛴다.
    /// AUTO를 켜면 줄이 다 나온 뒤 잠시 기다렸다가 다음 줄로 간다 (대화를 시작할 때마다 꺼진 상태로 시작한다). 끝날 때까지 PlayAsync가 기다린다</summary>
    public sealed class DialogueView : HudView, IUiBindable
    {
        public void BindUi(IUiRegistry registry) => _confirm = registry.Get<ConfirmPopupView>();

        [Serializable]
        private struct Portrait
        {
            public string Speaker;
            public Sprite Sprite;
            public Color Tint;
        }

        [SerializeField] private GameObject _root;
        [SerializeField] private Image _leftPortrait;
        [SerializeField] private Image _rightPortrait;
        [SerializeField] private RectTransform _nameTag;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _bodyText;
        [Tooltip("화면 전체를 덮는 투명 버튼. 탭으로 줄을 넘긴다")]
        [SerializeField] private Button _tapArea;
        [SerializeField] private Button _skipButton;
        [SerializeField] private Button _autoButton;
        [SerializeField] private Image _autoBackground;
        [SerializeField] private TMP_Text _autoLabel;
        [SerializeField] private Color _autoOnColor = new Color(0.95f, 0.65f, 0.25f, 1f);
        [SerializeField] private Color _autoOffColor = new Color(0.55f, 0.55f, 0.62f, 1f);
        [Tooltip("AUTO일 때 줄이 다 나온 뒤 다음 줄로 넘어가기까지 기다리는 시간(초)")]
        [SerializeField, Min(0f)] private float _autoDelay = 1.2f;
        [Tooltip("화자별 초상. 목록에 없는 화자는 흰색 칸으로 보인다 (아트가 오면 교체)")]
        [SerializeField] private Portrait[] _portraits;
        [SerializeField, Min(1f)] private float _charsPerSecond = 32f;
        [SerializeField] private float _slideDistance = 360f;
        [SerializeField] private float _nameTagInset = 90f;

        private UniTaskCompletionSource _step;
        private Tween _typing;
        private Tween _slide;
        private bool _skipAll;
        private bool _typingDone;
        private bool _confirming;
        private float _autoWaited;
        private ConfirmPopupView _confirm;
        private bool _auto;

        public bool IsOpen => _root.activeSelf;

        protected override void InitializeView()
        {
            _root.SetActive(false);
            _tapArea.onClick.AddListener(OnTap);
            _skipButton.onClick.AddListener(() => AskSkipAsync().Forget(Debug.LogException));
            _autoButton.onClick.AddListener(() =>
            {
                _auto = !_auto;
                RefreshAuto();
            });
            RefreshAuto();
        }

        private async UniTask AskSkipAsync()
        {
            if (_confirming)
            {
                return;
            }

            _confirming = true;
            try
            {
                bool skip = _confirm == null || await _confirm.ShowAsync(new ConfirmRequest
                {
                    Title = "대화 건너뛰기",
                    Message = "남은 대화를 건너뛸까요?",
                    ConfirmLabel = "건너뛰기",
                    CancelLabel = "취소",
                });
                if (skip && this != null && _root.activeSelf)
                {
                    _skipAll = true;
                    _step?.TrySetResult();
                }
            }
            finally
            {
                _confirming = false;
            }
        }

        private void RefreshAuto()
        {
            if (_autoBackground != null)
            {
                _autoBackground.color = _auto ? _autoOnColor : _autoOffColor;
            }

            if (_autoLabel != null)
            {
                _autoLabel.text = _auto ? "AUTO ON" : "AUTO OFF";
            }
        }

        private void Update()
        {
            // 확인창이 떠 있는 동안에는 넘기지 않는다
            if (!_auto || !_typingDone || _confirming || _step == null || !_root.activeSelf)
            {
                return;
            }

            _autoWaited += Time.unscaledDeltaTime;
            if (_autoWaited >= _autoDelay)
            {
                _step.TrySetResult();
            }
        }

        public async UniTask PlayAsync(IReadOnlyList<DialogueLine> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return;
            }

            _skipAll = false;
            _auto = false;
            RefreshAuto();
            _root.SetActive(true);
            _leftPortrait.gameObject.SetActive(false);
            _rightPortrait.gameObject.SetActive(false);
            try
            {
                foreach (var line in lines)
                {
                    Show(line);
                    _step = new UniTaskCompletionSource();
                    await _step.Task;
                    if (_skipAll)
                    {
                        break;
                    }
                }
            }
            finally
            {
                KillTweens();
                if (this != null)
                {
                    _root.SetActive(false);
                }
            }
        }

        private void Show(DialogueLine line)
        {
            KillTweens();
            var portrait = line.IsRight ? _rightPortrait : _leftPortrait;
            var other = line.IsRight ? _leftPortrait : _rightPortrait;
            Apply(portrait, line.Speaker);
            bool entering = !portrait.gameObject.activeSelf;
            portrait.gameObject.SetActive(true);
            portrait.color = Color.Lerp(portrait.color, Color.white, 0f);
            if (other.gameObject.activeSelf)
            {
                other.rectTransform.localScale = Vector3.one * 0.95f;
                other.canvasRenderer.SetColor(new Color(0.45f, 0.45f, 0.45f, 1f));
            }

            portrait.canvasRenderer.SetColor(Color.white);
            portrait.rectTransform.localScale = Vector3.one;
            if (entering)
            {
                float rest = portrait.rectTransform.anchoredPosition.x;
                float from = rest + (line.IsRight ? _slideDistance : -_slideDistance);
                portrait.rectTransform.anchoredPosition = new Vector2(from, portrait.rectTransform.anchoredPosition.y);
                _slide = DOTween.To(() => portrait.rectTransform.anchoredPosition.x,
                        x => portrait.rectTransform.anchoredPosition = new Vector2(x, portrait.rectTransform.anchoredPosition.y), rest, 0.3f)
                    .SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
            }

            _nameText.text = line.Speaker;
            float tagX = line.IsRight ? _nameTagInset : -_nameTagInset;
            _nameTag.anchoredPosition = new Vector2(tagX, _nameTag.anchoredPosition.y);
            _nameTag.pivot = new Vector2(line.IsRight ? 1f : 0f, _nameTag.pivot.y);
            _nameTag.anchorMin = _nameTag.anchorMax = new Vector2(line.IsRight ? 1f : 0f, _nameTag.anchorMin.y);
            _nameTag.anchoredPosition = new Vector2(line.IsRight ? -_nameTagInset : _nameTagInset, _nameTag.anchoredPosition.y);

            _autoWaited = 0f;
            _bodyText.text = line.Text;
            _bodyText.maxVisibleCharacters = 0;
            _typingDone = false;
            _bodyText.ForceMeshUpdate();
            int total = _bodyText.textInfo.characterCount;
            _typing = DOTween.To(() => _bodyText.maxVisibleCharacters, n => _bodyText.maxVisibleCharacters = n, total, total / _charsPerSecond)
                .SetEase(Ease.Linear).SetUpdate(true).SetLink(gameObject)
                .OnComplete(() => _typingDone = true);
        }

        private void Apply(Image image, string speaker)
        {
            image.sprite = null;
            image.color = Color.white;
            foreach (var portrait in _portraits ?? Array.Empty<Portrait>())
            {
                if (portrait.Speaker == speaker)
                {
                    image.sprite = portrait.Sprite;
                    image.color = portrait.Tint.a > 0f ? portrait.Tint : Color.white;
                    return;
                }
            }
        }

        private void OnTap()
        {
            if (!_typingDone)
            {
                _typing?.Complete(true);
                return;
            }

            _step?.TrySetResult();
        }

        private void KillTweens()
        {
            _typing?.Kill();
            _slide?.Kill();
            _typing = _slide = null;
        }

        protected override void DisposeView()
        {
            KillTweens();
            _step?.TrySetCanceled();
        }
    }
}
