using Game.Core;
using TMPro;
using UnityEngine;
using VContainer;
using CharacterInfo = Game.Core.CharacterInfo;

namespace Game.View
{
    /// <summary>캐릭터 화면. 열 때 IGrowthCatalog에서 캐릭터·장비 정보를 읽어 채우고, 열린 장비 칸을 누르면 장비 강화 팝업을 열고, 잠긴 칸을 누르면 열리는 레벨을 안내한다.
    /// 아래 캐릭터 목록(카드)은 아직 데이터가 없어 프리팹 그대로 둔다</summary>
    public sealed class CharacterScreenView : HudView, IUiBindable
    {
        public void BindUi(IUiRegistry registry)
        {
            _equipPopup = registry.Get<EquipUpgradePopupView>();
            _toast = registry.Get<ComingSoonToastView>();
        }

        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _nameText, _rankText, _powerText;
        [SerializeField] private EquipSlotView[] _equipSlots;   // IGrowthCatalog.Equips와 같은 순서
        private EquipUpgradePopupView _equipPopup;
        private ComingSoonToastView _toast;

        private IGrowthCatalog _catalog;
        private PlayerProfile _profile;

        // TODO(data): 장비 강화·캐릭터 변경 후 다시 그리려면 관련 메시지(추가 예정) 구독
        [Inject]
        public void Construct(IGrowthCatalog catalog, PlayerProfile profile)
        {
            _catalog = catalog;
            _profile = profile;
            _profile.Changed += OnProfileChanged;
        }

        protected override void DisposeView()
        {
            if (_profile != null)
            {
                _profile.Changed -= OnProfileChanged;
            }

            base.DisposeView();
        }

        // 화면이 열려 있는 동안 프로필이 바뀌면(장비 강화 등) 장비 칸의 레벨·잠금을 다시 그린다
        private void OnProfileChanged(PlayerProfile _)
        {
            if (_panel.activeSelf)
            {
                Refresh();
            }
        }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
        }

        public bool IsVisible => _panel.activeSelf;

        public RectTransform SlotOf(string upgradeId)
        {
            var equips = _catalog.Equips;
            for (int i = 0; i < equips.Count && i < _equipSlots.Length; i++)
            {
                if (equips[i].UpgradeId == upgradeId) { return (RectTransform)_equipSlots[i].transform; }
            }
            return null;
        }

        public void SetVisible(bool visible)
        {
            _panel.SetActive(visible);
            if (visible)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            CharacterInfo character = _catalog.Character;
            _nameText.SetL10n(() => _catalog.Character.Name);
            _rankText.SetL10n(() => _catalog.Character.Rank);
            _powerText.text = CurrencyFormat.Format(character.Power);

            var equips = _catalog.Equips;
            for (int i = 0; i < _equipSlots.Length; i++)
            {
                if (i >= equips.Count)
                {
                    _equipSlots[i].Hide();
                    continue;
                }

                int slot = i;
                _equipSlots[i].Bind(equips[i], () => _equipPopup.Open(slot), _toast);
            }
        }
    }
}
