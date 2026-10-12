using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>확률 정보 팝업 한 줄: 후보의 아이콘, 이름, 확률 문자열</summary>
    public readonly struct RewardOddsRow
    {
        public Sprite Icon { get; }
        public string Name { get; }
        public string Percent { get; }

        public RewardOddsRow(Sprite icon, string name, string percent)
        {
            Icon = icon;
            Name = name;
            Percent = percent;
        }
    }

    /// <summary>랜덤 재료 보상에서 무엇이 얼마의 확률로 나오는지 보여주는 팝업. 상세 창의 "i" 아이콘이 연다.
    /// 상세 창 위에 겹쳐 뜨고, 닫으면 상세 창으로 돌아간다</summary>
    public sealed class RewardOddsPopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private TMP_Text _title, _description;
        [SerializeField] private Button _closeButton, _confirmButton;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private RectTransform _rowParent;
        [SerializeField] private RewardOddsRowView _rowTemplate;

        private readonly List<RewardOddsRowView> _rows = new();

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _rowTemplate.gameObject.SetActive(false);
            _closeButton.onClick.AddListener(Close);
            _confirmButton.onClick.AddListener(Close);
        }

        public void Show(string title, string description, IReadOnlyList<RewardOddsRow> rows)
        {
            _title.text = title;
            _description.text = description;
            for (int i = 0; i < rows.Count; i++)
            {
                if (i == _rows.Count)
                {
                    _rows.Add(Instantiate(_rowTemplate, _rowParent, false));
                }

                _rows[i].gameObject.SetActive(true);
                _rows[i].Set(rows[i]);
            }

            for (int i = rows.Count; i < _rows.Count; i++)
            {
                _rows[i].gameObject.SetActive(false);
            }

            PopupPanel.Set(_panel, _transition, true);
            PopupPanel.FitContent(_scroll, 560f, 1120f);
            _scroll.verticalNormalizedPosition = 1f;
        }

        public void Close() => PopupPanel.Set(_panel, _transition, false);
    }
}
