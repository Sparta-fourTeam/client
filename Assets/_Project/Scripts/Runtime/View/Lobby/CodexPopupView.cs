using System.Collections.Generic;
using System.Linq;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>일반·엘리트·보스 도감. 기존 조우 등록과 상세 창의 1회 보상 수령을 재사용한다.</summary>
    public sealed class CodexPopupView : LobbyListPopupView, IUiBindable
    {
        [SerializeField] private MonsterDisplayTable _display;
        [SerializeField] private Transform _grid;
        [SerializeField] private CodexSlotView _slotTemplate;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private ScrollRect _scroll;

        private static readonly EnemyType[] Grades = { EnemyType.Normal, EnemyType.Elite, EnemyType.Boss };
        private readonly List<CodexSlotView> _slots = new();
        private GameDataStore _data;
        private PlayerProfile _profile;
        private DetailPopupView _detail;

        public EnemyType Grade => Grades[SelectedTab];
        public IReadOnlyList<CodexSlotView> VisibleSlots => _slots.FindAll(s => s.gameObject.activeSelf);

        public void BindUi(IUiRegistry registry) => _detail = registry.Get<DetailPopupView>();

        [Inject]
        public void Construct(GameDataStore data, PlayerProfile profile)
        {
            _data = data;
            _profile = profile;
            _profile.Changed += OnProfileChanged;
        }

        protected override void InitializeView()
        {
            base.InitializeView();
            _slotTemplate.gameObject.SetActive(false);
        }

        protected override void DisposeView()
        {
            if (_profile != null) { _profile.Changed -= OnProfileChanged; }
            base.DisposeView();
        }

        protected override void OnTabSelected(int index)
        {
            Refresh();
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1f;
        }

        private void OnProfileChanged(PlayerProfile profile)
        {
            if (IsOpen) { Refresh(); }
        }

        private void Refresh()
        {
            if (_data == null || _profile == null) { return; }
            var monsters = _data.Monsters.Values.Where(m => m.GetEnemyType() == Grade)
                .GroupBy(m => CodexFamilyId(m.Id))
                .OrderBy(group => group.FirstOrDefault(m => m.Id == group.Key)?.Hp ?? group.Min(m => m.Hp))
                .ThenBy(group => group.Key)
                .SelectMany(group => group.OrderBy(m => m.Id == group.Key ? 0 : 1)
                    .ThenBy(m => m.Hp).ThenBy(m => m.Damage).ThenBy(m => m.Id)).ToList();
            int discovered = monsters.Count(m => _profile.IsMonsterRegistered(m.Id));
            _progressText.text = $"발견 {discovered} / {monsters.Count}";
            EmptyLabel.gameObject.SetActive(monsters.Count == 0);
            while (_slots.Count < monsters.Count) { _slots.Add(Instantiate(_slotTemplate, _grid, false)); }
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i >= monsters.Count) { _slots[i].gameObject.SetActive(false); continue; }
                int id = monsters[i].Id;
                bool known = _profile.IsMonsterRegistered(id);
                MonsterDisplayTable.MonsterEntry entry = null;
                if (_display != null) { _display.TryGetMonster(id, out entry); }
                var item = new StageMonsterItem(entry?.icon, entry?.displayName ?? $"#{id}", monsters[i].IsBoss, id);
                _slots[i].Bind(id, item.Icon, known,
                    () => DetailContent.ShowMonster(_detail, _data, _display, item),
                    CodexFamilyId(id) != id ? 0.65f : 1f);
            }
        }

        private int CodexFamilyId(int id)
        {
            if (_display != null && _display.TryGetMonster(id, out var entry) && entry.codexFamilyId != 0)
            { return entry.codexFamilyId; }
            return id;
        }
    }
}
