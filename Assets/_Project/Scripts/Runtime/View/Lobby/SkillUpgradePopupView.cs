using System;
using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>스킬 강화 팝업. IGrowthCatalog의 스킬 정보로 능력치·레벨별 보상·비용을 채운다.
    /// 레벨업은 아직 재료 체크·차감 없이 더미 레벨만 올린다</summary>
    public sealed class SkillUpgradePopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private GameObject _maxLevel;
        [SerializeField] private TMP_Text _nameText, _levelText, _descText, _coinText, _bookText;
        [SerializeField] private StatCellView[] _statCells;             // Stats/StatCell1~4
        [SerializeField] private LevelRewardRowView _rewardRowPrefab;   // 보상 행이 모자랄 때 복제할 원본 (LevelRewardRow1)
        [SerializeField] private Transform _rewardContent;              // LevelRewards/Viewport/Content
        [SerializeField] private GameObject _cost;                      // 인법서·엽전 비용 묶음

        private readonly List<LevelRewardRowView> _rewardRows = new();

        private IGrowthCatalog _catalog;
        private PlayerProfile _profile;
        private int _index;

        /// <summary>레벨업이 끝나면 알린다. 스킬 화면이 칸의 레벨을 다시 그린다</summary>
        // TODO(data): 실제 구매가 붙으면 UpgradeChanged 메시지 구독으로 바꾼다
        public event Action Upgraded;

        [Inject]
        public void Construct(IGrowthCatalog catalog, PlayerProfile profile)
        {
            _catalog = catalog;
            _profile = profile;
        }

        private void Awake()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(() => _panel.SetActive(false));
            _upgradeButton.onClick.AddListener(OnUpgradeClicked);

            // 프리팹에 미리 만들어 둔 행을 재사용하고, 모자라면 BindRewards에서 복제한다
            _rewardRows.AddRange(_rewardContent.GetComponentsInChildren<LevelRewardRowView>(true));
        }

        public void Open(int index)
        {
            _index = index;
            _panel.SetActive(true);
            SkillInfo info = _catalog.Skills[index];
            bool isMax = info.IsMaxLevel;

            _nameText.text = info.Name;
            _levelText.text = isMax ? "MAX" : $"Lv. {info.Level}";
            _descText.text = info.Description;

            // 최대 레벨이면 다음 레벨이 없으므로 증가량을 숨긴다
            for (int i = 0; i < _statCells.Length; i++)
            {
                if (i < info.Stats.Count)
                {
                    _statCells[i].Bind(info.Stats[i], showIncrease: !isMax);
                }
                else
                {
                    _statCells[i].Hide();
                }
            }

            BindRewards(info);

            // 최대 레벨에는 다음 비용이 없으므로 비용 칸과 강화 버튼을 숨기고 "최대 레벨"을 띄운다
            _cost.SetActive(!isMax);
            if (!isMax)
            {
                _bookText.text = CostFormat.HaveNeed(_catalog.SkillBooks, info.BookCost);
                _coinText.text = CostFormat.HaveNeed(_profile.Gold, info.CoinCost);
            }

            _upgradeButton.gameObject.SetActive(!isMax);
            _maxLevel.SetActive(isMax);
        }

        // TODO(data): IUpgradeApi.Purchase(스킬 id)를 감싸는 구매 로직으로 교체한다 (#143)
        private void OnUpgradeClicked()
        {
            SkillInfo info = _catalog.Skills[_index];
            if (info.IsMaxLevel)
            {
                return;
            }

            // TODO: 재료 체크 — UpgradeRule.Check로 엽전·인법서가 충분한지 확인하고, 부족하면 막는다.
            // 지금은 항상 충분하다고 보고 넘긴다
            Debug.Log("소비 됨!");

            // 임시: 더미 데이터의 레벨만 올린다 (재시작하면 원래대로). 능력치·비용은 더미 값이라 그대로다
            info.Level++;
            Open(_index);
            Upgraded?.Invoke();
        }

        // 보상 수만큼 행을 채우고 남는 행은 끈다. 달성 여부는 현재 레벨과 비교한다
        private void BindRewards(SkillInfo info)
        {
            int count = info.LevelRewards?.Count ?? 0;
            while (_rewardRows.Count < count)
            {
                _rewardRows.Add(Instantiate(_rewardRowPrefab, _rewardContent));
            }

            for (int i = 0; i < _rewardRows.Count; i++)
            {
                if (i < count)
                {
                    LevelReward reward = info.LevelRewards[i];
                    _rewardRows[i].Bind(reward, reached: reward.Level <= info.Level);
                }
                else
                {
                    _rewardRows[i].Hide();
                }
            }
        }
    }
}
