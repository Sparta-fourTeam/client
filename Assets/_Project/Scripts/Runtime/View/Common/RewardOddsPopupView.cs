using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>확률형 보상(랜덤 마법북·랜덤 인챈트 주문서)의 후보별 획득 확률을 보여주는 팝업.
    /// 확률은 RandomRewardOdds가 테이블과 현재 플레이어 레벨로 계산한다. 화면에 박지 않는다</summary>
    public sealed class RewardOddsPopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private TMP_Text _title, _description;
        [SerializeField] private Button _closeButton, _confirmButton;
        [SerializeField] private ScrollRect _scroll;

        [Header("목록 (Content 아래에 꺼둔 템플릿을 복제한다)")]
        [SerializeField] private Transform _content;
        [SerializeField] private TMP_Text _sectionTemplate;
        [SerializeField] private RewardOddsRowView _rowTemplate;
        [SerializeField] private TMP_Text _emptyTemplate;
        [SerializeField] private ItemIconTable _itemIcons;

        [Header("표시")]
        [SerializeField, Range(0, 4)] private int _decimals = 2;

        private readonly List<GameObject> _spawned = new();
        private GameDataStore _data;
        private PlayerProfile _profile;

        [Inject]
        public void Construct(GameDataStore data, PlayerProfile profile)
        {
            _data = data;
            _profile = profile;
        }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _sectionTemplate.gameObject.SetActive(false);
            _rowTemplate.gameObject.SetActive(false);
            if (_emptyTemplate != null) { _emptyTemplate.gameObject.SetActive(false); }
            _closeButton.onClick.AddListener(Close);
            if (_confirmButton != null) { _confirmButton.onClick.AddListener(Close); }
        }

        /// <summary>보상 목록에서 확률형 보상만 골라 보여준다. 확률형 보상이 없으면 열지 않고 false를 돌려준다</summary>
        public bool ShowFor(IEnumerable<ItemAmount> rewards) => Show(RandomRewardOdds.RandomItemsOf(rewards));

        /// <summary>주어진 확률형 보상 ID마다 한 구역씩 보여준다. 확률형 보상이 없으면 열지 않고 false를 돌려준다</summary>
        public bool Show(IReadOnlyList<string> randomItemIds)
        {
            if (randomItemIds == null || randomItemIds.Count == 0) { return false; }
            Clear();
            _title.text = "확률 정보";
            _description.text = $"현재 레벨(Lv.{_profile.Level})에서 열린 항목 중 1개씩 아래 확률로 정해집니다. "
                + "레벨이 오르면 후보와 확률이 바뀝니다. 표시 확률은 반올림 값이라 합계가 100%와 조금 다를 수 있습니다.";

            foreach (var randomItemId in randomItemIds)
            {
                Spawn(_sectionTemplate).text = SectionName(randomItemId);
                var odds = RandomRewardOdds.For(randomItemId, _data, _profile.Exp);
                if (odds.Count == 0)
                {
                    if (_emptyTemplate != null) { Spawn(_emptyTemplate).text = "아직 열린 항목이 없어 지급되지 않습니다."; }
                    continue;
                }

                foreach (var entry in odds)
                {
                    var item = _data.Items.GetOrThrow(entry.ItemId);
                    Spawn(_rowTemplate).Set(_itemIcons != null ? _itemIcons.Get(item.IconKey) : null, item.Name,
                        RandomRewardOdds.FormatPercent(entry.Probability, _decimals));
                }
            }

            PopupPanel.Set(_panel, _transition, true);
            PopupPanel.FitContent(_scroll, 560f, 1120f);
            _scroll.verticalNormalizedPosition = 1f;
            return true;
        }

        public void Close() => PopupPanel.Set(_panel, _transition, false);

        private static string SectionName(string randomItemId) =>
            randomItemId == ItemIds.RandomSkillMaterial ? "랜덤 마법북" : "랜덤 인챈트 주문서";

        private T Spawn<T>(T template) where T : Component
        {
            var copy = Instantiate(template, _content);
            copy.gameObject.SetActive(true);
            _spawned.Add(copy.gameObject);
            return copy;
        }

        private void Clear()
        {
            foreach (var spawned in _spawned)
            {
                if (spawned == null) { continue; }
                spawned.SetActive(false); // Destroy는 프레임 끝에 처리되므로 같은 프레임의 높이 계산에서 빼 둔다
                Destroy(spawned);
            }

            _spawned.Clear();
        }
    }
}
