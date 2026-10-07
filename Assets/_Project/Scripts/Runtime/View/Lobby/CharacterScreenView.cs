using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using CharacterInfo = Game.Core.CharacterInfo;

namespace Game.View
{
    /// <summary>캐릭터 화면. 열 때 IGrowthCatalog에서 캐릭터·장비 정보를 읽어 채우고, 열린 장비 칸을 누르면 장비 강화 팝업을 연다.
    /// 아래 캐릭터 목록(카드)은 아직 데이터가 없어 프리팹 그대로 둔다</summary>
    public sealed class CharacterScreenView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _nameText, _rankText, _powerText;
        [SerializeField] private EquipSlotView[] _equipSlots;   // IGrowthCatalog.Equips와 같은 순서
        [SerializeField] private EquipUpgradePopupView _equipPopup;

        [Header("플레이어 레벨 (레벨 글자, 경험치 게이지 채움, 현재/필요 경험치 글자)")]
        [SerializeField] private TMP_Text _levelText, _expText;
        [SerializeField] private Image _expFill;

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

        protected override void OnDestroy()
        {
            if (_profile != null)
            {
                _profile.Changed -= OnProfileChanged;
            }

            base.OnDestroy();
        }

        // 화면이 열려 있는 동안 프로필이 갱신되면(전투 보상 EXP 등) 레벨 표시를 다시 그린다
        private void OnProfileChanged(PlayerProfile _)
        {
            if (_panel.activeSelf)
            {
                RefreshLevel();
            }
        }

        private void Awake()
        {
            _panel.SetActive(false);
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
            _nameText.text = character.Name;
            _rankText.text = character.Rank;
            _powerText.text = CurrencyFormat.Format(character.Power);
            RefreshLevel();

            var equips = _catalog.Equips;
            for (int i = 0; i < _equipSlots.Length; i++)
            {
                if (i >= equips.Count)
                {
                    _equipSlots[i].Hide();
                    continue;
                }

                int slot = i;
                _equipSlots[i].Bind(equips[i], () => _equipPopup.Open(slot));
            }
        }

        // 만렙에서는 요구 경험치가 0이라 게이지를 가득 채우고 "MAX"를 보여준다
        private void RefreshLevel()
        {
            _levelText.text = $"Lv.{_profile.Level}";
            _expFill.fillAmount = _profile.LevelProgress;
            _expText.text = _profile.IsMaxLevel
                ? "MAX"
                : $"{CurrencyFormat.Format(_profile.ExpIntoLevel)} / {CurrencyFormat.Format(_profile.ExpRequiredForNext)}";
        }
    }
}
