using Cysharp.Threading.Tasks;
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
        public void BindUi(IUiRegistry registry)
        {
            _equipPopup = registry.Get<EquipUpgradePopupView>();
            _toast = registry.Get<ComingSoonToastView>();
            _confirm = registry.Get<ConfirmPopupView>();
        }

        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _copyIdButton, _renameButton;
        [SerializeField] private TMP_Text _idText, _nameText, _levelText, _expText, _stageText;
        [SerializeField] private Image _expFill;
        [SerializeField] private EquipSlotView[] _equipSlots;   // IGrowthCatalog.Equips와 같은 순서
        [SerializeField] private TMP_Text[] _equipNames;        // 장비 칸 아래 이름 (_equipSlots와 같은 순서)
        private EquipUpgradePopupView _equipPopup;
        private ComingSoonToastView _toast;

        private ConfirmPopupView _confirm;
        private AccountService _account;
        private bool _renaming;
        private IGrowthCatalog _catalog;
        private PlayerProfile _profile;

        [Inject]
        public void Construct(IGrowthCatalog catalog, PlayerProfile profile, AccountService account)
        {
            _account = account;
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
            _copyIdButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = _profile.PlayerId);
            _renameButton.onClick.AddListener(() => RenameAsync().Forget(Debug.LogException));
        }

        private async UniTask RenameAsync()
        {
            if (_renaming || _account == null || !_profile.IsLoaded) { return; }
            _renaming = true;
            _renameButton.interactable = false;
            try
            {
                string draft = _profile.Nickname;
                string warning = null;
                while (this != null)
                {
                    var request = new ConfirmRequest
                    {
                        Title = L10n.Get("profile.renameTitle"),
                        Message = L10n.Get("profile.renameRule"),
                        Warning = warning,
                        ConfirmLabel = L10n.Get("common.change"),
                        CancelLabel = L10n.Get("common.cancel"),
                        InputPlaceholder = L10n.Get("profile.renamePlaceholder"),
                        InitialInput = draft,
                        InputValidator = value => NicknameRules.IsValid(value?.Trim()),
                    };
                    if (!await _confirm.ShowAsync(request)) { return; }
                    draft = request.InputValue.Trim();
                    try
                    {
                        await _account.ChangeNicknameAsync(draft);
                        _toast.Show(L10n.Get("profile.renamed"));
                        return;
                    }
                    catch (ApiException error) when (error.Code == "NICKNAME_DUPLICATED" || error.Code == "HTTP_409")
                    { warning = L10n.Get("profile.nameDuplicated"); }
                    catch (ApiException)
                    { warning = L10n.Get("profile.saveFailedRetry"); }
                }
            }
            catch (System.OperationCanceledException) { throw; }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                _toast.Show(L10n.Get("profile.renameFailed"));
            }
            finally
            {
                _renaming = false;
                if (_renameButton != null) { _renameButton.interactable = true; }
            }
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
            _idText.SetL10n("profile.playerId", _profile.PlayerId);
            _copyIdButton.interactable = !string.IsNullOrEmpty(_profile.PlayerId);
            _renameButton.interactable = !_renaming && _account != null && _profile.IsLoaded;
            _nameText.richText = false;
            _nameText.text = _profile.Nickname;
            _levelText.text = $"Lv.{_profile.Level}";
            _expFill.fillAmount = _profile.LevelProgress;
            bool isMaxLevel = _profile.IsMaxLevel;
            string expInto = CurrencyFormat.Format(_profile.ExpIntoLevel), expNeeded = CurrencyFormat.Format(_profile.ExpRequiredForNext);
            _expText.SetL10n(() => isMaxLevel ? L10n.Get("label.max") : $"({expInto}/{expNeeded})");
            _stageText.SetL10n("profile.stageValue", _profile.HighestUnlockedStage.ToString());

            var equips = _catalog.Equips;
            for (int i = 0; i < _equipSlots.Length; i++)
            {
                bool has = i < equips.Count;
                if (i < _equipNames.Length)
                {
                    _equipNames[i].gameObject.SetActive(has);
                    if (has)
                    {
                        _equipNames[i].SetL10n(equips[i].NameKey);
                    }
                }

                if (!has)
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
