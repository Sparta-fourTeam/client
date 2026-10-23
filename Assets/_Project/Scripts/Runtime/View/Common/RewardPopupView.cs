using System.Collections.Generic;
using DG.Tweening;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>관문, 우편, 도감 등에서 수령한 보상 목록을 전체 화면으로 보여준다 (창·박스 없음).
    /// 리본이 가운데에서 펼쳐지며 제목이 나오고 → 보상 칸이 차례로 튀어나오며 번쩍임이 인다 → "탭하여 계속".
    /// 연출 중 탭하면 연출이 바로 끝나고, 끝난 뒤 한 번 더 탭하면 닫힌다. 연출용 칸은 비워 둬도 동작한다</summary>
    public sealed class RewardPopupView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private TMP_Text _title, _description;
        [Tooltip("화면 전체를 덮는 투명 버튼. 연출 중엔 스킵, 끝난 뒤엔 닫기")]
        [SerializeField] private Button _tapArea;
        [SerializeField] private ResultRewardListView _rewards;

        [Header("연출")]
        [Tooltip("제목 리본. 가운데에서 양옆으로 펼쳐진다")]
        [SerializeField] private RectTransform _ribbon;
        [Tooltip("보상이 나타나는 순간 번쩍이는 흰 판 (CanvasGroup)")]
        [SerializeField] private CanvasGroup _flash;
        [Tooltip("\"탭하여 계속\" 안내. 연출이 끝나면 깜빡인다")]
        [SerializeField] private CanvasGroup _tapHint;
        [SerializeField, Min(0.02f)] private float _cellInterval = 0.08f;

        private Sequence _sequence;
        private Tween _hintBlink;
        private bool _playing;
        private GridLayoutGroup _rewardGrid;
        private RectTransform _rewardContent;
        private float _rewardAreaHeight;
        private Vector2 _rewardPosition;
        private IAudioService _audio;

        [Inject]
        public void Construct(IAudioService audio) => _audio = audio;

        /// <summary>등장 연출이 아직 재생 중인가 (이때 탭하면 연출만 건너뛴다)</summary>
        public bool IsPlaying => _playing;

        private void Awake()
        {
            _panel.SetActive(false);
            _tapArea.onClick.AddListener(OnTap);
            if (_rewards.Cells.Count > 0)
            {
                _rewardContent = _rewards.Cells[0].transform.parent as RectTransform;
                _rewardGrid = _rewardContent.GetComponent<GridLayoutGroup>();
                _rewardAreaHeight = _rewardContent.rect.height;
                _rewardPosition = _rewardContent.anchoredPosition;
            }
        }

        private void OnDestroy() => KillTweens();

        /// <summary>보상 목록을 표시하고, 목록 크기에 맞춘 전체 화면 등장 연출을 시작한다</summary>
        public void Show(IReadOnlyList<ResultRewardItem> rewards, string description = null, string title = null)
        {
            if (title != null) { _title.text = title; }
            else { _title.SetL10n("reward.popupTitle"); }
            if (description != null) { _description.text = description; }
            else { _description.SetL10n("reward.popupDesc"); }
            _rewards.Show(rewards);
            PopupPanel.Set(_panel, _transition, true);
            FitRewards(rewards.Count);
            Play(rewards.Count);
        }

        // 두 행까지는 원래 크기를 유지하고, 더 많은 보상은 같은 영역 안에 모두 보여 준다.
        // 칸의 크기만 줄이면 글자/아이콘은 넘칠 수 있으므로 콘텐츠 전체를 함께 축소한다.
        private void FitRewards(int count)
        {
            if (_rewardGrid == null || _rewardGrid.constraint != GridLayoutGroup.Constraint.FixedColumnCount)
            {
                return;
            }

            int rows = Mathf.CeilToInt(count / (float)_rewardGrid.constraintCount);
            float height = _rewardGrid.padding.vertical + rows * _rewardGrid.cellSize.y
                + Mathf.Max(0, rows - 1) * _rewardGrid.spacing.y;
            height = Mathf.Max(_rewardAreaHeight, height);
            _rewardContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            float inset = 0f;
            if (height > _rewardAreaHeight)
            {
                var description = RectTransformUtility.CalculateRelativeRectTransformBounds(_rewards.transform, _description.rectTransform);
                inset = Mathf.Clamp(_rewardAreaHeight * 0.5f - description.min.y + _rewardGrid.spacing.y, 0f, _rewardAreaHeight * 0.5f);
            }

            _rewardContent.anchoredPosition = _rewardPosition - new Vector2(0f, inset * 0.5f);
            float scale = (_rewardAreaHeight - inset) / height;
            _rewardContent.localScale = new Vector3(scale, scale, 1f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rewardContent);
        }

        public void Close()
        {
            KillTweens();
            PopupPanel.Set(_panel, _transition, false);
        }

        // 시작 상태(숨김)로 맞춘 뒤 연출 시퀀스를 만든다. 시간은 timeScale과 무관하게 흐른다
        private void Play(int count)
        {
            KillTweens();
            _playing = true;
            SetAlpha(_flash, 0f);
            SetAlpha(_tapHint, 0f);
            _description.alpha = 0f;

            var cells = new List<Transform>();
            for (int i = 0; i < count && i < _rewards.Cells.Count; i++)
            {
                var cell = _rewards.Cells[i].transform;
                cell.localScale = Vector3.zero;
                cells.Add(cell);
            }

            var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            if (_ribbon != null)
            {
                // 가는 빛줄기로 시작해 좌우로 촥 펼쳐진다
                _ribbon.localScale = new Vector3(0f, 0.12f, 1f);
                seq.Append(_ribbon.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutCubic));
            }

            seq.Append(Fade(_description, 1f, 0.2f));
            float reveal = seq.Duration();
            if (count > 0) { seq.InsertCallback(reveal, () => _audio?.PlaySfx(SoundIds.LevelUp)); }
            foreach (var cell in cells)
            {
                seq.Append(cell.DOScale(1f, 0.28f).SetEase(Ease.OutBack));
                seq.AppendInterval(_cellInterval);
            }

            if (cells.Count > 0)
            {
                InsertFlash(seq, reveal);
            }

            seq.OnComplete(OnSequenceDone);
            _sequence = seq;
        }

        // 첫 보상이 튀어나오는 순간의 짧은 번쩍임
        private void InsertFlash(Sequence seq, float at)
        {
            if (_flash != null)
            {
                seq.Insert(at, Fade(_flash, 0.3f, 0.04f));
                seq.Insert(at + 0.04f, Fade(_flash, 0f, 0.3f));
            }
        }

        private void OnSequenceDone()
        {
            _playing = false;
            if (_tapHint == null)
            {
                return;
            }

            _tapHint.alpha = 1f;
            _hintBlink = Fade(_tapHint, 0.25f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
        }

        private void OnTap()
        {
            if (_playing)
            {
                // 연출을 끝 상태로 건너뛴다 (OnComplete가 불려 _playing이 꺼진다)
                _sequence?.Complete(true);
                return;
            }

            Close();
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null)
            {
                group.alpha = alpha;
            }
        }

        private static Tweener Fade(CanvasGroup group, float to, float seconds) =>
            DOTween.To(() => group.alpha, a => group.alpha = a, to, seconds);

        private static Tweener Fade(TMP_Text text, float to, float seconds) =>
            DOTween.To(() => text.alpha, a => text.alpha = a, to, seconds);

        // 이 뷰가 만든 트윈만 정리한다 (전역 트윈은 건드리지 않는다)
        private void KillTweens()
        {
            _sequence?.Kill();
            _sequence = null;
            _hintBlink?.Kill();
            _hintBlink = null;
            _playing = false;
        }
    }
}
