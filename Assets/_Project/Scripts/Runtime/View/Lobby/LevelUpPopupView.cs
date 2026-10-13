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
    /// <summary>로비에 돌아왔을 때 레벨업 소식을 로비 위에 전체 화면으로 보여준다 (창·박스 없음).
    /// 리본이 펼쳐지고 → "Lv.1 → Lv.2"에서 새 레벨이 쾅 찍히며 번쩍임·충격 링·스파클이 터지고 →
    /// "새 기능 해금"(ILevelUnlockSource)과 "보상"(튜토리얼 보상 코인·아이템 등) 아이콘이 차례로 튀어나온다 → "탭하여 계속".
    /// 연출 중 탭하면 연출이 바로 끝나고, 끝난 뒤 한 번 더 탭하면 닫힌다. 닫을 때까지 ShowAsync가 기다린다.
    /// 연출용 칸은 비워 둬도 동작한다 (그 연출만 빠진다)</summary>
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
        [Tooltip("해금 섹션 전체 (배경 + 제목 + 카드 줄). 해금이 없으면 숨긴다")]
        [SerializeField] private GameObject _unlockSection;
        [Tooltip("보상 섹션 전체 (배경 + 제목 + 카드 줄). 보상이 없으면 숨긴다")]
        [SerializeField] private GameObject _rewardSection;
        [SerializeField] private SkillAssetTable _skillAssets;
        [SerializeField] private ItemIconTable _itemIcons;

        [Header("연출")]
        [Tooltip("\"Lv.1 → Lv.2\" 한 줄 전체. 레벨이 오르지 않았으면 숨긴다")]
        [SerializeField] private GameObject _levelRow;
        [Tooltip("이전 레벨 (작고 흐리게)")]
        [SerializeField] private TMP_Text _prevLabel;
        [SerializeField] private TMP_Text _arrowLabel;
        [Tooltip("새 레벨 (크게). 쾅 찍히며 나타난다")]
        [SerializeField] private TMP_Text _levelLabel;
        [Tooltip("레벨이 찍히는 순간 번쩍이는 흰 판 (CanvasGroup)")]
        [SerializeField] private CanvasGroup _flash;
        [Tooltip("레벨이 찍히는 순간 재생하는 파티클 (스파클·충격 링). UIParticle 프리팹의 ParticleSystem을 넣는다")]
        [SerializeField] private ParticleSystem[] _impactFx;
        [Tooltip("제목 리본. 가운데에서 양옆으로 펼쳐진다")]
        [SerializeField] private RectTransform _ribbon;
        [Tooltip("\"탭하여 계속\" 안내. 연출이 끝나면 깜빡인다")]
        [SerializeField] private CanvasGroup _tapHint;
        [SerializeField, Min(0.05f)] private float _cardInterval = 0.1f;

        private readonly List<LevelUpLineView> _unlockCards = new();
        private readonly List<LevelUpLineView> _rewardCards = new();
        private GameDataStore _data;
        private UniTaskCompletionSource _closed;
        private Sequence _sequence;
        private Tween _hintBlink;
        private Tween _fxStop;
        private bool _playing;

        private const float ImpactEmitSeconds = 0.1f;

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
            if (news.RewardGold > 0)
            {
                rewards.Add((_itemIcons != null ? _itemIcons.Coin : null, $"코인\n<color=#FFD35A>×{news.RewardGold}</color>"));
            }

            foreach (var item in news.RewardItems)
            {
                var data = _data.Items.GetOrThrow(item.itemId);
                rewards.Add((_itemIcons != null ? _itemIcons.Get(item.itemId) : null, $"{data.Name}\n<color=#FFD35A>×{item.quantity}</color>"));
            }

            Fill(_unlockCards, _unlockLine, unlocks);
            Fill(_rewardCards, _rewardLine, rewards);
            _unlockSection.SetActive(unlocks.Count > 0);
            _rewardSection.SetActive(rewards.Count > 0);
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

            _unlockSection.transform.localScale = Vector3.zero;
            _rewardSection.transform.localScale = Vector3.zero;
            bool leveled = news.LeveledUp && _levelLabel != null;
            if (_levelRow != null)
            {
                _levelRow.SetActive(news.LeveledUp);
            }

            if (_prevLabel != null)
            {
                _prevLabel.text = $"Lv.{news.FromLevel}";
                _prevLabel.alpha = 0f;
            }

            if (_arrowLabel != null)
            {
                _arrowLabel.alpha = 0f;
            }

            if (_levelLabel != null)
            {
                _levelLabel.text = $"Lv.{news.ToLevel}";
                _levelLabel.alpha = 0f;
                _levelLabel.transform.localScale = Vector3.one * 2.4f;
            }

            SetAlpha(_tapHint, 0f);
            SetAlpha(_flash, 0f);
            StopImpactFx();

            var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            if (_ribbon != null)
            {
                // 가는 빛줄기로 시작해 좌우로 촥 펼쳐진다
                _ribbon.localScale = new Vector3(0f, 0.12f, 1f);
                seq.Append(_ribbon.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutCubic));
            }

            if (leveled)
            {
                if (_prevLabel != null)
                {
                    seq.Append(DOTween.To(() => _prevLabel.alpha, a => _prevLabel.alpha = a, 1f, 0.2f));
                }

                if (_arrowLabel != null)
                {
                    seq.Append(DOTween.To(() => _arrowLabel.alpha, a => _arrowLabel.alpha = a, 1f, 0.15f));
                }

                // 새 레벨이 크게 떨어져 쾅 찍힌다
                seq.Append(_levelLabel.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.InQuad));
                seq.Join(DOTween.To(() => _levelLabel.alpha, a => _levelLabel.alpha = a, 1f, 0.12f));
                float impact = seq.Duration();
                InsertImpact(seq, impact);
                seq.Append(_levelLabel.transform.DOPunchScale(Vector3.one * 0.18f, 0.3f, 6));
            }

            AppendCards(seq, _unlockSection, _unlockCards, unlockCount);
            AppendCards(seq, _rewardSection, _rewardCards, rewardCount);
            seq.OnComplete(OnSequenceDone);
            _sequence = seq;
        }

        // 레벨이 찍히는 순간: 짧은 번쩍임, 충격 링, 사방으로 튀는 스파클
        private void InsertImpact(Sequence seq, float at)
        {
            if (_flash != null)
            {
                seq.Insert(at, Fade(_flash, 0.4f, 0.04f));
                seq.Insert(at + 0.04f, Fade(_flash, 0f, 0.3f));
            }

            seq.InsertCallback(at, PlayImpactFx);
        }

        // UIParticle은 반복 재생 + 방출 중단 방식에서 확실히 그려진다 (WaveHudView의 _resetSparks와 같은 방식)
        private void PlayImpactFx()
        {
            foreach (var fx in _impactFx ?? System.Array.Empty<ParticleSystem>())
            {
                fx.Play(true);
            }

            _fxStop?.Kill();
            _fxStop = DOVirtual.DelayedCall(ImpactEmitSeconds, StopImpactEmission, true).SetLink(gameObject);
        }

        private void StopImpactEmission()
        {
            foreach (var fx in _impactFx ?? System.Array.Empty<ParticleSystem>())
            {
                fx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void StopImpactFx()
        {
            foreach (var fx in _impactFx ?? System.Array.Empty<ParticleSystem>())
            {
                fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void AppendCards(Sequence seq, GameObject section, List<LevelUpLineView> cards, int count)
        {
            if (count == 0)
            {
                return;
            }

            seq.AppendInterval(0.08f);
            seq.Append(section.transform.DOScale(1f, 0.18f).SetEase(Ease.OutCubic));
            for (int i = 0; i < count; i++)
            {
                seq.Append(cards[i].transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack));
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
            UnlockKind.Weapon => $"{unlock.Name}\n<color=#FFD35A>새 무기</color>",
            UnlockKind.Equipment => $"{unlock.Name}\n<color=#FFD35A>새 장비</color>",
            _ => unlock.Name,
        };

        private void Close()
        {
            KillTweens();
            StopImpactFx();
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
