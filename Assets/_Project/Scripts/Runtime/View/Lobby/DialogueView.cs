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
    /// 탭하면 지금 줄을 끝까지 보여주고, 한 번 더 탭하면 다음 줄로 간다. SKIP은 남은 줄을 모두 건너뛴다. 끝날 때까지 PlayAsync가 기다린다</summary>
    public sealed class DialogueView : HudView
    {
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

        public bool IsOpen => _root.activeSelf;

        protected override void InitializeView()
        {
            _root.SetActive(false);
            _tapArea.onClick.AddListener(OnTap);
            _skipButton.onClick.AddListener(() =>
            {
                _skipAll = true;
                _step?.TrySetResult();
            });
        }

        public async UniTask PlayAsync(IReadOnlyList<DialogueLine> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return;
            }

            _skipAll = false;
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
