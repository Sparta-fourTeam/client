using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>스테이지가 끝나고 결과 제출까지 끝나면 결과를 보여준다.
    /// StageEnded는 종료 안내만 보여주고, 서버가 확정한 보상·통계는 StageResult 뒤에 공개한다.
    /// 판정 없이 끝나는 포기(Forfeit)는 Submitting에서 종료 안내를 시작한다.</summary>
    public sealed class ResultPopupView : HudView, IUiBindable
    {
        public void BindUi(IUiRegistry registry)
        {
            _detail = registry.Get<DetailPopupView>();
        }

        // 보상 칸을 누르면 여는 공용 상세 창
        private DetailPopupView _detail;

        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private TMP_Text _titleText;
        [Tooltip("제목 뒤 리본. 클리어와 실패에 따라 스프라이트를 바꾼다 (비우면 그대로)")]
        [SerializeField] private Image _titleRibbon;
        [SerializeField] private Sprite _clearRibbon, _failRibbon;
        [Tooltip("제목 리본 색. 같은 리본 이미지를 클리어(금색)와 실패(장밋빛)에서 색만 바꿔 쓴다")]
        [SerializeField] private Color _clearRibbonColor = Color.white, _failRibbonColor = new Color(1f, 0.55f, 0.6f, 1f);
        [SerializeField] private TMP_Text _stageText;
        [SerializeField] private TMP_Text _killsText;
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private TMP_Text _bubbleText;
        [SerializeField] private Button _lobbyButton;

        [Header("보상 (코인, EXP, 재료, 보석상자 순서)")]
        [SerializeField] private ResultRewardListView _rewardList;
        [SerializeField] private ItemIconTable _itemIcons;

        [SerializeField] private ResultStarsView _starsView;

        [Tooltip("실패 결과의 성장 팁 버튼으로 여는 TIP 패널. 결과를 가리지 않도록 자동으로 열지 않는다")]
        [SerializeField] private ResultTipView _tip;
        [SerializeField] private Button _tipButton;


        // 스킬별 피해량은 StageResult.SkillDamages로 온다. 보유 스킬(SkillChanged)의 레벨과 SkillAssetTable의 아이콘으로 줄을 만든다.
        // 목록이 비면 영역이 꺼진다
        [Header("인법 피해량")]
        [SerializeField] private ResultDamageListView _damageList;
        [SerializeField] private SkillAssetTable _skillAssets;

        private const string ClearBubble = "어때요? 이 정도쯤이야!";

        private StageContext _stageContext;
        private ISceneNavigator _navigator;
        private GameDataStore _data;
        private IReadOnlyList<ISkillStatus> _ownedSkills;
        private bool _firstStageClear;
        private GameObject _outcomePanel;
        private TMP_Text _outcomeTitle, _outcomeDescription;
        private float _outcomeShownAt;
        private Tween _reveal;
        private bool _hasResult;
        private bool _submissionBlocked;
        private const float OutcomeHoldSeconds = 1.1f;

        [Inject]
        public void Construct(ISubscriber<StageResult> stageResult, StageContext stageContext, ISceneNavigator navigator, GameDataStore data,
            IBufferedSubscriber<SkillChanged> skillChanged, PlayerProfile profile,
            ISubscriber<StageEnded> stageEnded = null, IBufferedSubscriber<StageStateChanged> stateChanged = null,
            ISubscriber<SubmitFailed> submitFailed = null, ISubscriber<SubmitRejected> submitRejected = null)
        {
            Track(skillChanged.Subscribe(message => _ownedSkills = message.Skills));
            _stageContext = stageContext;
            _firstStageClear = stageContext.StageId == 1 && !profile.IsStageCleared(1);
            _data = data;
            _navigator = navigator;
            Track(stageResult.Subscribe(OnStageResult));
            if (stageEnded != null)
            {
                Track(stageEnded.Subscribe(e =>
                {
                    if (!_hasResult && !_submissionBlocked) { ShowOutcome(e.Outcome == StageOutcome.Clear); }
                }));
            }
            if (stateChanged != null)
            {
                Track(stateChanged.Subscribe(e =>
                {
                    if (e.State == StageState.Submitting && !_hasResult)
                    {
                        _submissionBlocked = false;
                        if (_outcomePanel != null && !_outcomePanel.activeSelf) { ShowOutcome(null); }
                    }
                }));
            }
            if (submitFailed != null) { Track(submitFailed.Subscribe(_ => HideOutcome())); }
            if (submitRejected != null) { Track(submitRejected.Subscribe(_ => HideOutcome())); }
        }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _lobbyButton.onClick.AddListener(OnLobbyClicked);
            if (_tip != null) { _tip.Hide(); }
            if (_tipButton != null)
            {
                _tipButton.onClick.AddListener(() => { if (_tip != null) { _tip.Show(); } });
                _tipButton.interactable = true;
                var label = _tipButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) { label.text = "성장 팁"; }
                _tipButton.gameObject.SetActive(false);
            }
            CreateOutcomePanel();
        }

        private void CreateOutcomePanel()
        {
            _outcomePanel = new GameObject("BattleOutcome", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)_outcomePanel.transform;
            rect.SetParent(_panel.transform.parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _outcomePanel.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.11f, 0.82f);
            _outcomeTitle = Instantiate(_titleText, rect, false);
            _outcomeDescription = Instantiate(_bubbleText, rect, false);
            PositionOutcomeText(_outcomeTitle, new Vector2(0f, 80f), new Vector2(720f, 150f), 64f);
            PositionOutcomeText(_outcomeDescription, new Vector2(0f, -80f), new Vector2(720f, 180f), 32f);
            _outcomePanel.SetActive(false);
        }

        private static void PositionOutcomeText(TMP_Text text, Vector2 position, Vector2 size, float fontSize)
        {
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = size;
            text.rectTransform.localScale = Vector3.one;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = fontSize;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = fontSize * 0.75f;
            text.enableAutoSizing = true;
            text.color = Color.white;
            text.raycastTarget = false;
            text.gameObject.SetActive(true);
        }

        private void ShowOutcome(bool? cleared)
        {
            _reveal?.Kill();
            if (!_outcomePanel.activeSelf) { _outcomeShownAt = Time.realtimeSinceStartup; }
            _lobbyButton.interactable = false;
            _outcomeTitle.text = cleared == true ? "방어 성공!" : cleared == false ? "방어 실패" : "전투 종료";
            _outcomeTitle.color = cleared == true ? new Color32(255, 207, 91, 255) : Color.white;
            _outcomeDescription.text = cleared == true ? "마지막 적을 쓰러뜨렸어요.\n획득한 보상을 확인해요."
                : cleared == false ? "이번 도전은 여기까지예요.\n확보한 보상을 확인하고 다시 준비해요."
                : "이번 도전의 결과를 정리하고 있어요.";
            _outcomePanel.SetActive(true);
            _outcomePanel.transform.SetAsLastSibling();
        }

        private void HideOutcome()
        {
            _submissionBlocked = true;
            _reveal?.Kill();
            if (_outcomePanel != null) { _outcomePanel.SetActive(false); }
        }

        private void OnStageResult(StageResult result)
        {
            _hasResult = true;
            ShowOutcome(result.Cleared);
            _titleText.text = result.Cleared ? "방어 성공!" : "방어 실패";
            var ribbon = result.Cleared ? _clearRibbon : _failRibbon;
            if (_titleRibbon != null)
            {
                if (ribbon != null) { _titleRibbon.sprite = ribbon; }
                _titleRibbon.color = result.Cleared ? _clearRibbonColor : _failRibbonColor;
            }
            _stageText.text = _stageContext.StageId > 0 ? $"Stage {_stageContext.StageId}" : "-";
            _killsText.text = $"처치\n{result.Kills}";
            _waveText.text = result.ReachedWave > 0 ? $"도달 웨이브\n{result.ReachedWave}" : "도달 웨이브\n-";
            _timeText.text = $"플레이 시간\n{FormatTime(result.PlayTime)}";
            _bubbleText.text = result.Cleared
                ? (_firstStageClear ? "첫 클리어! 로비에서 새 무기를 확인해 봐요!" : ClearBubble)
                : "확보한 보상은 받을 수 있어요.\n강화한 뒤 다시 도전해 봐요!";
            var rewards = RewardRows.Build(_itemIcons, _data, result.RewardGold, result.RewardExp, result.RewardItems);
            _rewardList.Show(rewards, i => DetailContent.ShowReward(_detail, _data, rewards[i], "획득 {0}개"));
            _starsView.Show(result.Cleared, result.ClearRating);
            if (_damageList != null)
            {
                _damageList.Show(SkillDamageRows.Build(result.SkillDamages, _ownedSkills,
                    status => _skillAssets != null ? _skillAssets.GetHudIcon(status.Id) : null));
            }
            var lobbyLabel = _lobbyButton.GetComponentInChildren<TMP_Text>(true);
            if (lobbyLabel != null) { lobbyLabel.text = "로비로"; }
            if (_tip != null) { _tip.Hide(); }
            if (_tipButton != null) { _tipButton.gameObject.SetActive(!result.Cleared && _tip != null); }
            float remaining = Mathf.Max(0f, OutcomeHoldSeconds - (Time.realtimeSinceStartup - _outcomeShownAt));
            _reveal = DOVirtual.DelayedCall(remaining, () =>
            {
                _outcomePanel.SetActive(false);
                PopupPanel.Set(_panel, _transition, true);
                _lobbyButton.interactable = true;
            }, ignoreTimeScale: true).SetLink(gameObject);
        }

        private void OnLobbyClicked()
        {
            if (!_lobbyButton.interactable) { return; }
            // 연타로 씬 전환이 두 번 일어나지 않게 막는다
            _lobbyButton.interactable = false;
            _navigator.GoToLobby().Forget(Debug.LogException);
        }

        protected override void DisposeView()
        {
            _reveal?.Kill();
            if (_outcomePanel != null) { _outcomePanel.SetActive(false); }
        }

        // 초를 "mm:ss"로 바꾼다
        private static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
