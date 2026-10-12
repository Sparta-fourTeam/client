using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>로비에 돌아왔을 때 레벨업 소식을 한 팝업에 줄로 보여준다: 레벨업, 이번에 해금된 것(ILevelUnlockSource), 함께 받은 아이템(튜토리얼 보상 등).
    /// 확인을 누를 때까지 ShowAsync가 기다린다</summary>
    public sealed class LevelUpPopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private LevelUpLineView _lineTemplate;        // 첫 줄. 모자라면 복제한다
        [SerializeField] private Sprite _levelIcon;
        [SerializeField] private SkillAssetTable _skillAssets;
        [SerializeField] private ItemIconTable _itemIcons;

        private readonly List<LevelUpLineView> _lines = new();
        private GameDataStore _data;
        private UniTaskCompletionSource _closed;

        public bool IsOpen => _panel.activeSelf;

        [Inject]
        public void Construct(GameDataStore data) => _data = data;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _lines.Add(_lineTemplate);
            _confirmButton.onClick.AddListener(Close);
        }

        public UniTask ShowAsync(LevelUpNews news)
        {
            var rows = new List<(Sprite icon, string text)>();
            if (news.LeveledUp)
            {
                rows.Add((_levelIcon, $"레벨 업! Lv.{news.FromLevel} → Lv.{news.ToLevel}"));
            }

            foreach (var unlock in news.Unlocks)
            {
                rows.Add((IconOf(unlock), TextOf(unlock)));
            }

            foreach (var item in news.RewardItems)
            {
                var data = _data.Items.GetOrThrow(item.itemId);
                rows.Add((_itemIcons != null ? _itemIcons.Get(data.IconKey) : null, $"{data.Name} ×{item.quantity} 획득!"));
            }

            while (_lines.Count < rows.Count)
            {
                _lines.Add(Instantiate(_lineTemplate, _lineTemplate.transform.parent));
            }

            for (int i = 0; i < _lines.Count; i++)
            {
                if (i < rows.Count)
                {
                    _lines[i].Bind(rows[i].icon, rows[i].text);
                }
                else
                {
                    _lines[i].Hide();
                }
            }

            _title.text = news.LeveledUp ? "레벨 업!" : "보상 획득";
            _closed = new UniTaskCompletionSource();
            PopupPanel.Set(_panel, _transition, true);
            return _closed.Task;
        }

        // 해금 종류마다 아이콘 표가 다르다. 표가 없는 종류는 아이콘 없이 글만 보여준다
        private Sprite IconOf(UnlockNotice unlock) => unlock.Kind switch
        {
            UnlockKind.Weapon => _skillAssets != null ? _skillAssets.GetHudIcon(unlock.IconKey) : null,
            _ => null,
        };

        private static string TextOf(UnlockNotice unlock) => unlock.Kind switch
        {
            UnlockKind.Weapon => $"새 무기 [{unlock.Name}] 획득!",
            UnlockKind.Equipment => $"새 장비 [{unlock.Name}] 해금!",
            _ => $"[{unlock.Name}] 해금!",
        };

        private void Close()
        {
            PopupPanel.Set(_panel, _transition, false);
            _closed?.TrySetResult();
        }

        protected override void DisposeView() => _closed?.TrySetCanceled();
    }
}
