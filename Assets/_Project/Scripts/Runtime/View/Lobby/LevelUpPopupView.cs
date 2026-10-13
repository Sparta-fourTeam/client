using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>로비에 돌아왔을 때 레벨업 소식을 로비 위에 전체 화면으로 보여준다 (창 없음).
    /// 넓은 리본이 옆에서 들어오고 → 빛이 번쩍이며 큰 레벨 숫자가 이전 레벨에서 새 레벨로 올라가고 →
    /// "새 기능 해금" 카드(ILevelUnlockSource)와 "보상" 카드(튜토리얼 보상 아이템 등)가 차례로 튀어나온다 → "탭하여 계속".
    /// 연출 중 탭하면 연출이 바로 끝나고, 끝난 뒤 한 번 더 탭하면 닫힌다. 닫을 때까지 ShowAsync가 기다린다.
    /// 연출용 칸(_levelLabel·_flash·_glow·_ribbon·_tapHint)은 비워 둬도 동작한다 (그 연출만 빠진다)</summary>
    public sealed class LevelUpPopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [Tooltip("리본 안의 제목")]
        [SerializeField] private TMP_Text _title;
        [Tooltip("화면 전체를 덮는 투명 버튼. 연출 중엔 스킵, 끝난 뒤엔 닫기")]
        [SerializeField] private Button _confirmButton;
        [Tooltip("해금 카드 줄. 이 칸이 첫 카드이고 모자라면 복제한다")]
        [SerializeField] private LevelUpLineView _unlockLine;
        [Tooltip("보상 카드 줄. 이 칸이 첫 카드이고 모자라면 복제한다")]
        [SerializeField] private LevelUpLineView _rewardLine;
        [SerializeField] private GameObject _unlockHeader;
        [SerializeField] private GameObject _rewardHeader;
        [SerializeField] private SkillAssetTable _skillAssets;
        [SerializeField] private ItemIconTable _itemIcons;

        [Header("연출")]
        [Tooltip("큰 레벨 숫자 (예: Lv.2). 이전 레벨에서 새 레벨로 세어 올라간다")]
        [SerializeField] private TMP_Text _levelLabel;
        [Tooltip("레벨업 순간 번쩍이는 흰 판 (CanvasGroup)")]
        [SerializeField] private CanvasGroup _flash;
        [Tooltip("레벨 숫자 뒤의 빛 (CanvasGroup). 숫자가 오르는 동안 커지며 나타난다")]
        [SerializeField] private CanvasGroup _glow;
        [Tooltip("제목 리본. 옆에서 미끄러져 들어온다")]
        [SerializeField] private RectTransform _ribbon;
        [Tooltip("\"탭하여 계속\" 안내. 연출이 끝나면 깜빡인다")]
        [SerializeField] private CanvasGroup _tapHint;
        [SerializeField, Min(0.05f)] private float _cardInterval = 0.18f;
        [SerializeField, Min(0.1f)] private float _countSeconds = 0.8f;

        private readonly List<LevelUpLineView> _unlockCards = new();
        private readonly List<LevelUpLineView> _rewardCards = new();
        private GameDataStore _data;
        private UniTaskCompletionSource _closed;
        private Sequence _sequence;
        private Tween _hintBlink;
        private Tween _glowPulse;
        private bool _playing;

        public bool IsOpen => _panel.activeSelf;

        /// <summary>등장 연출이 아직 재생 중인가 (이때 탭하면 연출만 건너뛴다)</summary>
        public bool IsPlaying => _playing;

        [Inject]
        public void Construct(GameDataStore data) => _data = data;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _unlockCards.Add(_unlockLine);
            _rewardCards.Add(_rewardLine);
            _confirmButton.onClick.AddListener(OnTap);
        }

        public UniTask ShowAsync(LevelUpNews news)
        {
            var unlocks = new List<(Sprite icon, string text)>();
            foreach (var unlock in news.Unlocks)
            {
                unlocks.Add((IconOf(unlock), TextOf(unlock)));
            }

            var rewards = new List<(Sprite icon, string text)>();
            foreach (var item in news.RewardItems)
            {
                var data = _data.Items.GetOrThrow(item.itemId);
                rewards.Add((_itemIcons != null ? _itemIcons.Get(data.IconKey) : null, $"{data.Name} ×{item.quantity}"));
            }

            Fill(_unlockCards, _unlockLine, unlocks);
            Fill(_rewardCards, _rewardLine, rewards);
            _unlockHeader.SetActive(unlocks.Count > 0);
            _rewardHeader.SetActive(rewards.Count > 0);
            _title.text = news.LeveledUp ? "레벨 업!" : "보상 획득";
            _closed = new UniTaskCompletionSource();
            PopupPanel.Set(_panel, _transition, true);
            Play(news, unlocks.Count, rewards.Count);
            return _closed.Task;
        }

        private static void Fill(List<LevelUpLineView> cards, LevelUpLineView template, List<(Sprite icon, string text)> rows)
        {
            while (cards.Count < rows.Count)
            {
                cards.Add(Instantiate(template, template.transform.parent));
            }

            for (int i = 0; i < cards.Count; i++)
            {
                if (i < rows.Count)
                {
                    cards[i].Bind(rows[i].icon, rows[i].text);
                }
                else
                {
                    cards[i].Hide();
                }
            }
        }

        // 시작 상태(숨김)로 맞춘 뒤 연출 시퀀스를 만든다. 시간은 timeScale과 무관하게 흐른다
        private void Play(LevelUpNews news, int unlockCount, int rewardCount)
        {
            KillTweens();
            _playing = true;

            foreach (var card in _unlockCards)
            {
                card.transform.localScale = Vector3.zero;
            }

            foreach (var card in _rewardCards)
            {
                card.transform.localScale = Vector3.zero;
            }

            _unlockHeader.transform.localScale = Vector3.zero;
            _rewardHeader.transform.localScale = Vector3.zero;
            if (_levelLabel != null)
            {
                _levelLabel.gameObject.SetActive(news.LeveledUp);
                _levelLabel.text = $"Lv.{news.FromLevel}";
                _levelLabel.transform.localScale = Vector3.one;
            }

            SetAlpha(_tapHint, 0f);
            SetAlpha(_flash, 0f);
            SetAlpha(_glow, 0f);
            if (_glow != null)
            {
                _glow.transform.localScale = Vector3.one * 0.4f;
            }

            var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            if (_ribbon != null)
            {
                float rest = _ribbon.anchoredPosition.x;
                _ribbon.anchoredPosition = new Vector2(rest + 1600f, _ribbon.anchoredPosition.y);
                seq.Append(DOTween.To(() => _ribbon.anchoredPosition.x, x => _ribbon.anchoredPosition = new Vector2(x, _ribbon.anchoredPosition.y), rest, 0.4f).SetEase(Ease.OutBack));
            }

            if (news.LeveledUp && _levelLabel != null)
            {
                if (_flash != null)
                {
                    seq.Append(Fade(_flash, 0.9f, 0.08f));
                    seq.Append(Fade(_flash, 0f, 0.4f));
                }

                if (_glow != null)
                {
                    seq.Join(Fade(_glow, 1f, 0.5f));
                    seq.Join(_glow.transform.DOScale(1f, 0.6f).SetEase(Ease.OutCubic));
                }

                seq.Append(DOVirtual.Int(news.FromLevel, news.ToLevel, _countSeconds, v => _levelLabel.text = $"Lv.{v}").SetEase(Ease.OutCubic));
                seq.Append(_levelLabel.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 6));
            }

            AppendCards(seq, _unlockHeader, _unlockCards, unlockCount);
            AppendCards(seq, _rewardHeader, _rewardCards, rewardCount);
            seq.OnComplete(OnSequenceDone);
            _sequence = seq;
        }

        private void AppendCards(Sequence seq, GameObject header, List<LevelUpLineView> cards, int count)
        {
            if (count == 0)
            {
                return;
            }

            seq.Append(header.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack));
            for (int i = 0; i < count; i++)
            {
                seq.Append(cards[i].transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
                seq.AppendInterval(_cardInterval);
            }
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

        private void OnSequenceDone()
        {
            _playing = false;
            if (_glow != null && _glow.alpha > 0f)
            {
                _glowPulse = _glow.transform.DOScale(1.08f, 1.2f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(gameObject);
            }

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

        // 해금 종류마다 아이콘 표가 다르다. 표가 없는 종류는 아이콘 없이 글만 보여준다
        private Sprite IconOf(UnlockNotice unlock) => unlock.Kind switch
        {
            UnlockKind.Weapon => _skillAssets != null ? _skillAssets.GetHudIcon(unlock.IconKey) : null,
            _ => null,
        };

        private static string TextOf(UnlockNotice unlock) => unlock.Kind switch
        {
            UnlockKind.Weapon => $"{unlock.Name}\n새 무기",
            UnlockKind.Equipment => $"{unlock.Name}\n새 장비",
            _ => unlock.Name,
        };

        private void Close()
        {
            KillTweens();
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
            _glowPulse?.Kill();
            _glowPulse = null;
            _playing = false;
        }

        protected override void DisposeView()
        {
            KillTweens();
            _closed?.TrySetCanceled();
        }
    }
}
