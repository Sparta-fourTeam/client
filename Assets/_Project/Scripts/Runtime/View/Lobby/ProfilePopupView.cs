using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>프로필 팝업. 로비 상단의 프로필 칸을 누르면 열린다.
    /// 플레이어 ID·닉네임, 레벨·경험치, 장비 레벨, 진행 관문을 보여주고, 열린 장비 칸을 누르면 장비 강화 팝업을 연다.
    /// 보유 캐릭터 목록은 아직 데이터가 없어 넣지 않았다</summary>
    public sealed class ProfilePopupView : HudView, IUiBindable
    {
        // TODO(server): PlayerSnapshot에 플레이어 ID·닉네임이 생기면 그 값으로 바꾼다 (지금은 임시값)
        private const string TempPlayerId = "LOCAL-0001";
        private const string TempNickname = "닌자";

        public void BindUi(IUiRegistry registry)
        {
            _equipPopup = registry.Get<EquipUpgradePopupView>();
        }

        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _copyIdButton;
        [SerializeField] private TMP_Text _idText, _nameText, _levelText, _expText, _stageText;
        [SerializeField] private Image _expFill;
        [SerializeField] private EquipSlotView[] _equipSlots;   // IGrowthCatalog.Equips와 같은 순서
        [SerializeField] private TMP_Text[] _equipNames;        // 장비 칸 아래 이름 (_equipSlots와 같은 순서)
        private EquipUpgradePopupView _equipPopup;

        private IGrowthCatalog _catalog;
        private PlayerProfile _profile;

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

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(() => PopupPanel.Set(_panel, _transition, false));
            _copyIdButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = TempPlayerId);
        }

        public void Open()
        {
            PopupPanel.Set(_panel, _transition, true);
            Refresh();
        }

        // 열려 있는 동안 프로필이 바뀌면(장비 강화, 경험치 반영 등) 다시 그린다
        private void OnProfileChanged(PlayerProfile _)
        {
            if (_panel.activeSelf)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            _idText.text = $"ID: {TempPlayerId}";
            _nameText.text = TempNickname;
            _levelText.text = $"Lv.{_profile.Level}";
            _expFill.fillAmount = _profile.LevelProgress;
            _expText.text = _profile.IsMaxLevel
                ? "MAX"
                : $"({CurrencyFormat.Format(_profile.ExpIntoLevel)}/{CurrencyFormat.Format(_profile.ExpRequiredForNext)})";
            _stageText.text = $"제 {_profile.HighestUnlockedStage}관문";

            var equips = _catalog.Equips;
            for (int i = 0; i < _equipSlots.Length; i++)
            {
                bool has = i < equips.Count;
                if (i < _equipNames.Length)
                {
                    _equipNames[i].gameObject.SetActive(has);
                    if (has)
                    {
                        _equipNames[i].text = equips[i].Name;
                    }
                }

                if (!has)
                {
                    _equipSlots[i].Hide();
                    continue;
                }

                int slot = i;
                _equipSlots[i].Bind(equips[i], () => _equipPopup.Open(slot));
            }
        }
    }
}
