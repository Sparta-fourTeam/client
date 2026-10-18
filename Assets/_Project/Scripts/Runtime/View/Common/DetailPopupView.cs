using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>아이템·몬스터 등을 눌렀을 때 뜨는 공용 상세 창. 아이콘·제목·부제·설명을 보여주고,
    /// 이동할 곳이 있으면 아래 버튼(예: 강화하러 가기)을 띄운다. 어두운 배경이나 닫기를 누르면 닫힌다.
    /// 인벤토리와 미션 상세가 같이 쓴다. 몬스터를 보여줄 때 도감 등록 보상을 아직 안 받았으면 우측 상단에 선물 아이콘이 뜨고,
    /// 누르면 그 자리에서 보상을 받는다 (도감 화면이 아니어도 받을 수 있다)</summary>
    public sealed class DetailPopupView : HudView, IUiBindable
    {
        public const string DefaultGoLabel = "강화하러 가기";

        [SerializeField] private GameObject _panel;
        [SerializeField] private PointerClickRelay _background;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _titleText, _subtitleText, _descText;
        [SerializeField] private Button _goButton, _closeButton;
        [SerializeField] private TMP_Text _goLabel;

        [Header("도감 등록 보상 (몬스터 상세에서만 뜬다. 비어 있으면 수령 기능을 건너뛴다)")]
        [SerializeField] private Button _claimButton;
        [SerializeField] private GameObject _claimBadge;
        [SerializeField] private ItemIconTable _itemIcons;

        [Header("확률 정보 (랜덤 재료 보상에서만 뜬다. 비어 있으면 건너뛴다)")]
        [SerializeField] private Button _oddsButton;

        private Action _onGo;
        private PlayerProfile _profile;
        private ICodexApi _codexApi;
        private GameDataStore _data;
        private RewardPopupView _rewardPopup;
        private RewardOddsPopupView _oddsPopup;
        private string _oddsItemId;
        private int _claimMonsterId;
        private bool _claiming;

        public void BindUi(IUiRegistry registry)
        {
            _rewardPopup = registry.Get<RewardPopupView>();
            _oddsPopup = registry.Get<RewardOddsPopupView>();
        }

        [Inject]
        public void Construct(PlayerProfile profile, ICodexApi codexApi, GameDataStore data)
        {
            _profile = profile;
            _codexApi = codexApi;
            _data = data;
        }

        public bool IsShown => _panel.activeSelf;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _background.Clicked += Hide;
            _closeButton.onClick.AddListener(Hide);
            if (_claimButton != null)
            {
                _claimButton.onClick.AddListener(() => ClaimAsync().Forget(Debug.LogException));
            }

            if (_oddsButton != null)
            {
                _oddsButton.onClick.AddListener(ShowOdds);
            }
            _goButton.onClick.AddListener(() =>
            {
                var go = _onGo;
                Hide();
                go?.Invoke();
            });
        }

        /// <param name="onGo">아래 버튼을 누르면 부른다. null이면 버튼을 숨긴다</param>
        public void Show(Sprite icon, string title, string subtitle, string description, Action onGo = null, string goLabel = DefaultGoLabel)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _titleText.text = title;
            _subtitleText.text = subtitle;
            _descText.text = description;
            _onGo = onGo;
            _goButton.gameObject.SetActive(onGo != null);
            _goLabel.text = goLabel;
            SetClaimVisible(false);
            SetOddsVisible(false);
            _panel.SetActive(true);
        }

        /// <summary>Show로 몬스터를 띄운 직후에 부른다. 도감에 등록됐고 보상을 아직 안 받았으면 수령 아이콘을 띄운다</summary>
        public void OfferMonsterReward(int monsterId)
        {
            _claimMonsterId = monsterId;
            SetClaimVisible(_claimButton != null && _profile != null && _profile.CanClaimMonsterReward(monsterId));
        }

        /// <summary>Show로 보상을 띄운 직후에 부른다. 랜덤 재료(랜덤 마법북·랜덤 인챈트 주문서)면 확률 정보 "i" 아이콘을 띄운다</summary>
        public void OfferOdds(string itemId)
        {
            _oddsItemId = itemId;
            SetOddsVisible(_oddsButton != null && _oddsPopup != null && _profile != null && RandomRewardOdds.IsRandom(itemId, _data));
        }

        private void SetOddsVisible(bool visible)
        {
            if (_oddsButton != null)
            {
                _oddsButton.gameObject.SetActive(visible);
            }
        }

        private void ShowOdds()
        {
            string itemId = _oddsItemId;
            if (!RandomRewardOdds.IsRandom(itemId, _data)) { return; }

            var rows = new System.Collections.Generic.List<RewardOddsRow>();
            foreach (var odd in RandomRewardOdds.ForExp(itemId, _data, _profile.Exp))
            {
                rows.Add(new RewardOddsRow(RewardRows.IconOf(_itemIcons, _data, odd.ItemId),
                    ItemInfo.NameOf(odd.ItemId, _data), RandomRewardOdds.Percent(odd.Probability)));
            }

            string what = _data.RewardRules.Items[itemId].Kind == "SkillMaterial" ? "열린 스킬의 마법북" : "열린 장비의 인챈트 주문서";
            _oddsPopup.Show($"{ItemInfo.NameOf(itemId, _data)} 확률", $"현재 레벨에서 {what}이 같은 확률로 나와요.", rows);
        }

        private void SetClaimVisible(bool visible)
        {
            if (_claimButton != null)
            {
                _claimButton.gameObject.SetActive(visible);
            }

            if (_claimBadge != null)
            {
                _claimBadge.SetActive(visible);
            }
        }

        private async UniTask ClaimAsync()
        {
            int monsterId = _claimMonsterId;
            if (_claiming || !_profile.CanClaimMonsterReward(monsterId)) { return; }
            _claiming = true;
            _claimButton.interactable = false;
            var before = _profile.Snapshot();
            try
            {
                var snapshot = await _codexApi.ClaimMonsterReward(monsterId);
                var rows = RewardRows.Gained(_itemIcons, _data, before, snapshot);
                _profile.Apply(snapshot);
                if (this != null)
                {
                    SetClaimVisible(false);
                    _rewardPopup.Show(rows, "도감 등록 보상을 받았어요.");
                }
            }
            catch (ApiException error)
            {
                // 이미 받은 보상이면 아이콘만 치운다. 그 밖의 실패는 아이콘을 남겨 다시 누를 수 있게 한다
                if (this != null && error.Code == "ALREADY_CLAIMED") { SetClaimVisible(false); }
            }
            finally
            {
                _claiming = false;
                if (this != null) { _claimButton.interactable = true; }
            }
        }

        public void Hide()
        {
            _onGo = null;
            _panel.SetActive(false);
        }
    }
}
