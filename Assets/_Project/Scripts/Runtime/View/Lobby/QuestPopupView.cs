using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>퀘스트 팝업. 일일·주간 탭은 퀘스트 목록과 보물상자 게이지를 보여주고, 그 밖의 탭(업적)은 틀의 빈 목록 안내를 띄운다.
    /// 퀘스트를 받으면 점수가 오르고, 게이지가 닿은 보물상자를 누르면 골드와 에너지를 받는다. 틀은 LobbyListPopupView</summary>
    public sealed class QuestPopupView : LobbyListPopupView, IUiBindable
    {
        /// <summary>점수 보상 칸의 ItemId. 아이템이 아니라서 Items 표에는 없다</summary>
        public const string PointsId = "quest.points";
        /// <summary>에너지 보상 칸의 ItemId. 아이템이 아니라서 Items 표에는 없다</summary>
        public const string EnergyId = "currency.energy";
        /// <summary>잼 보상 칸의 ItemId. 아이템이 아니라서 Items 표에는 없다</summary>
        public const string GemId = RewardRows.GemId;

        [Header("탭")]
        [Tooltip("탭 순서대로, 받을 수 있는 것이 있을 때 켜는 빨간 점. 보드 수보다 길어도 된다(남는 탭은 건드리지 않는다)")]
        [SerializeField] private GameObject[] _tabBadges;

        [Header("퀘스트 목록")]
        [SerializeField] private QuestRowView _rowPrefab;
        [SerializeField] private Transform _rowParent;
        [SerializeField] private Button _claimAllButton;

        [Header("게이지와 보물상자")]
        [SerializeField] private GameObject _gauge;
        [Tooltip("막대 안을 채우는 이미지. 왼쪽 끝에 붙여 두고 오른쪽 앵커로 길이를 바꾼다")]
        [SerializeField] private Image _gaugeFill;
        [SerializeField] private TMP_Text _pointsText;
        [Tooltip("점수가 낮은 상자부터. 보드의 상자 수와 같거나 많아야 한다")]
        [SerializeField] private MissionRewardNodeView[] _chestNodes;
        [SerializeField] private TMP_Text[] _chestLabels;

        [Header("보상 표시")]
        [SerializeField] private ItemIconTable _itemIcons;
        [SerializeField] private Sprite _energyIcon;
        [SerializeField] private Sprite _gemIcon;
        [Tooltip("비워 두면 점수 보상 칸의 아이콘이 비어 보인다")]
        [SerializeField] private Sprite _pointsIcon;

        private readonly List<QuestRowView> _rows = new();
        private PlayerProfile _profile;
        private GameDataStore _data;
        private IQuestApi _api;
        private RewardPopupView _rewardPopup;
        private MissionRewardInfoPopupView _chestInfoPopup;
        private bool _busy;

        private QuestBoardDefinition CurrentBoard =>
            _data != null && SelectedTab < _data.QuestBoards.Count ? _data.QuestBoards[SelectedTab] : null;

        public void BindUi(IUiRegistry registry)
        {
            _rewardPopup = registry.Get<RewardPopupView>();
            _chestInfoPopup = registry.Get<MissionRewardInfoPopupView>();
        }

        [Inject]
        public void Construct(PlayerProfile profile, GameDataStore data, IQuestApi api)
        {
            _profile = profile;
            _data = data;
            _api = api;
            _profile.Changed += OnProfileChanged;
        }

        protected override void InitializeView()
        {
            base.InitializeView();
            _claimAllButton.onClick.AddListener(() => ClaimAllAsync().Forget(Debug.LogException));
            for (int i = 0; i < _chestNodes.Length; i++)
            {
                int index = i;
                _chestNodes[i].Button.onClick.AddListener(() => OnChestClicked(index));
            }
        }

        protected override void DisposeView()
        {
            if (_profile != null) { _profile.Changed -= OnProfileChanged; }
        }

        protected override void OnTabSelected(int index) => Refresh();

        private void OnProfileChanged(PlayerProfile profile)
        {
            if (IsOpen) { Refresh(); }
        }

        private void Refresh()
        {
            ShowTabBadges();
            var board = CurrentBoard;
            bool hasBoard = board != null;
            _gauge.SetActive(hasBoard);
            _claimAllButton.gameObject.SetActive(hasBoard);
            EmptyLabel.gameObject.SetActive(!hasBoard);
            if (!hasBoard)
            {
                foreach (var row in _rows) { row.gameObject.SetActive(false); }
                return;
            }

            ShowRows(board);
            ShowGauge(board);
        }

        // 탭마다 그 보드에 받을 수 있는 것이 있으면 빨간 점을 켠다 (탭 순서가 보드 순서다)
        private void ShowTabBadges()
        {
            for (int i = 0; i < _tabBadges.Length; i++)
            {
                bool hasBoard = _data != null && i < _data.QuestBoards.Count;
                _tabBadges[i].SetActive(hasBoard && QuestRules.HasClaimable(_profile, new[] { _data.QuestBoards[i] }));
            }
        }

        private void ShowRows(QuestBoardDefinition board)
        {
            while (_rows.Count < board.Quests.Count)
            {
                var row = Instantiate(_rowPrefab, _rowParent);
                int index = _rows.Count;
                row.Button.onClick.AddListener(() => ClaimQuestAsync(index).Forget(Debug.LogException));
                _rows.Add(row);
            }

            // 받을 수 있는 퀘스트를 맨 위, 받은 퀘스트를 맨 아래에 둔다. 그 안에서는 표의 순서를 따른다
            var order = Enumerable.Range(0, board.Quests.Count)
                .OrderBy(i => State(board.Quests[i]))
                .ThenBy(i => i)
                .ToList();
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].gameObject.SetActive(i < board.Quests.Count);
            }

            for (int position = 0; position < order.Count; position++)
            {
                int index = order[position];
                var quest = board.Quests[index];
                var row = _rows[index];
                row.Show(QuestText.Title(quest), _profile.QuestCount(quest.Id), quest.Target, quest.Points, _profile.IsQuestClaimed(quest.Id));
                row.Button.interactable &= !_busy;
                row.transform.SetSiblingIndex(position);
            }

            _claimAllButton.interactable = !_busy && board.Quests.Any(CanClaim);
        }

        // 0 받을 수 있음, 1 진행 중, 2 받음
        private int State(QuestDefinition quest) => _profile.IsQuestClaimed(quest.Id) ? 2 : CanClaim(quest) ? 0 : 1;

        private bool CanClaim(QuestDefinition quest) =>
            !_profile.IsQuestClaimed(quest.Id) && _profile.QuestCount(quest.Id) >= quest.Target;

        private void ShowGauge(QuestBoardDefinition board)
        {
            int points = _profile.QuestPoints(board);
            int max = Math.Max(1, board.MaxPoints);
            // 채움 막대는 스프라이트가 없어 fillAmount가 먹지 않으므로 오른쪽 앵커로 길이를 정한다
            _gaugeFill.rectTransform.anchorMax = new Vector2(GaugeRatio(board, points), 1f);
            _pointsText.text = $"{Math.Min(points, max)} / {max}";
            for (int i = 0; i < _chestNodes.Length; i++)
            {
                bool exists = i < board.Chests.Count;
                _chestNodes[i].gameObject.SetActive(exists);
                if (!exists) { continue; }

                var chest = board.Chests[i];
                _chestLabels[i].text = chest.Points.ToString();
                _chestNodes[i].Show(points >= chest.Points, _profile.IsQuestChestClaimed(chest.Id), _busy);
            }
        }

        /// <summary>막대에서 채운 비율(0~1). 상자는 막대를 같은 간격으로 나눈 칸의 가운데에 놓이므로, 점수가 상자의 점수에 닿는 순간 채움이 그 상자의 가운데에 온다</summary>
        private static float GaugeRatio(QuestBoardDefinition board, int points)
        {
            int count = board.Chests.Count;
            float previousPoints = 0f, previousPosition = 0f;
            for (int i = 0; i < count; i++)
            {
                float position = (i + 0.5f) / count;
                int needed = board.Chests[i].Points;
                if (points <= needed)
                {
                    return Mathf.Lerp(previousPosition, position, (points - previousPoints) / (needed - previousPoints));
                }

                previousPoints = needed;
                previousPosition = position;
            }

            return previousPosition;
        }

        private async UniTask ClaimQuestAsync(int index)
        {
            var board = CurrentBoard;
            if (_busy || board == null || index >= board.Quests.Count) { return; }

            var quest = board.Quests[index];
            if (!CanClaim(quest)) { return; }

            if (await RunAsync(() => _api.ClaimQuest(quest.Id)))
            {
                _rewardPopup.Show(new List<ResultRewardItem> { new(_pointsIcon, quest.Points, PointsId) },
                    "퀘스트 보상을 받았어요.", "퀘스트 완료");
            }
        }

        private async UniTask ClaimAllAsync()
        {
            var board = CurrentBoard;
            if (_busy || board == null) { return; }

            int points = 0;
            foreach (var quest in board.Quests.Where(CanClaim).ToList())
            {
                if (!await RunAsync(() => _api.ClaimQuest(quest.Id))) { break; }
                points += quest.Points;
            }

            if (points > 0)
            {
                _rewardPopup.Show(new List<ResultRewardItem> { new(_pointsIcon, points, PointsId) },
                    "퀘스트 보상을 받았어요.", "퀘스트 완료");
            }
        }

        // 열 수 있는 상자를 누르면 열 수 있는 상자를 한 번에 열고, 아직 못 열거나 이미 연 상자를 누르면 안에 든 보상을 보여준다
        private void OnChestClicked(int index)
        {
            var board = CurrentBoard;
            if (_busy || board == null || index >= board.Chests.Count) { return; }

            if (QuestRules.IsChestOpenable(_profile, board, board.Chests[index]))
            {
                ClaimChestAsync().Forget(Debug.LogException);
                return;
            }

            var chest = board.Chests[index];
            string name = board.Period == QuestPeriod.Daily ? "일일" : "주간";
            string description = _profile.IsQuestChestClaimed(chest.Id)
                ? $"{name} 퀘스트 · 이미 수령한 보상이에요."
                : $"{name} 퀘스트 · 점수 {chest.Points}점을 모으면 받을 수 있어요.";
            _chestInfoPopup?.Show($"보물상자 {chest.Points}점", description, ChestRewards(chest.Gold, chest.Energy, chest.Gems));
        }

        private List<ResultRewardItem> ChestRewards(int gold, int energy, int gems)
        {
            var rewards = new List<ResultRewardItem>();
            if (gold > 0) { rewards.Add(new ResultRewardItem(_itemIcons != null ? _itemIcons.Coin : null, gold, RewardRows.CoinId)); }
            if (energy > 0) { rewards.Add(new ResultRewardItem(_energyIcon, energy, EnergyId)); }
            if (gems > 0) { rewards.Add(new ResultRewardItem(_gemIcon, gems, GemId)); }
            return rewards;
        }

        private async UniTask ClaimChestAsync()
        {
            var board = CurrentBoard;
            if (_busy || board == null) { return; }

            // 어느 상자를 눌러도 지금 열 수 있는 상자를 한 번에 열고, 보상은 합쳐서 한 번만 보여준다
            int gold = 0, energy = 0, gems = 0;
            foreach (var chest in board.Chests.Where(c => QuestRules.IsChestOpenable(_profile, board, c)).ToList())
            {
                if (!await RunAsync(() => _api.ClaimQuestChest(chest.Id))) { break; }
                gold += chest.Gold;
                energy += chest.Energy;
                gems += chest.Gems;
            }

            var rewards = ChestRewards(gold, energy, gems);
            if (rewards.Count > 0) { _rewardPopup.Show(rewards, "보물상자 보상을 받았어요.", "보물상자 보상"); }
        }

        /// <summary>수령 요청 하나를 보내고 응답을 프로필에 반영한다. 거절되면 서버 기준 상태로 다시 그린다. 성공하면 true</summary>
        private async UniTask<bool> RunAsync(Func<UniTask<PlayerSnapshot>> request)
        {
            _busy = true;
            Refresh();
            try
            {
                _profile.Apply(await request());
                return true;
            }
            catch (ApiException)
            {
                return false;
            }
            finally
            {
                _busy = false;
                if (this != null) { Refresh(); }
            }
        }
    }
}
