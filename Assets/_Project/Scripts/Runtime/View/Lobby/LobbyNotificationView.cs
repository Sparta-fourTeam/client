using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using VContainer;

namespace Game.View
{
    public enum LobbyNotificationKind { Equipment, Skill, Reward, Quest, Shop }

    public static class LobbyAvailability
    {
        public static bool CanUpgrade(PlayerProfile profile, EquipInfo info) =>
            info.IsUnlocked && !info.IsMaxLevel && !string.IsNullOrEmpty(info.UpgradeId)
            && CanPay(profile, info.CoinCost, info.MaterialItemId, info.MaterialCost);

        public static bool CanUpgrade(PlayerProfile profile, SkillInfo info) =>
            info.IsUnlocked && !info.IsMaxLevel && !string.IsNullOrEmpty(info.UpgradeId)
            && CanPay(profile, info.CoinCost, info.MaterialItemId, info.MaterialCost);

        private static bool CanPay(PlayerProfile profile, int coins, string material, int quantity) =>
            profile.Gold >= coins && (material == null ? 0 : profile.ItemQuantity(material)) >= quantity;

        public static bool HasAnyReward(PlayerProfile profile) =>
            profile.Snapshot().stageProgress.Any(row => row.HasUnclaimedReward);

        /// <summary>일일상점에 아직 받지 않은 무료 물품이 있는가</summary>
        public static bool HasFreeShopOffer(PlayerProfile profile, GameDataStore data)
        {
            var state = profile.Shop;
            var offers = ShopRules.Roll(data.Shop, ShopRules.DayKey(System.DateTime.Now), state.adUsed + state.goldUsed);
            for (int i = 0; i < offers.Count; i++)
            {
                if (offers[i].Price <= 0 && !profile.IsShopSlotSold(i)) { return true; }
            }

            return false;
        }
    }

    // Aggregate tabs read the data, so their badge stays current even while the child screen is closed.
    public sealed class LobbyNotificationView : HudView
    {
        [SerializeField] private NotificationBadgeView _badge;
        [SerializeField] private LobbyNotificationKind _kind;
        [Tooltip("-1: all items; -2: unbound template; 0+: catalog index")]
        [SerializeField] private int _itemIndex = -1;
        private IGrowthCatalog _catalog;
        private PlayerProfile _profile;
        private GameDataStore _data;
        private string _shopDayKey;
        private IPlayerApi _player;
        private bool _refreshingShopDay;
        private float _nextShopDayCheck;

        [Inject]
        public void Construct(IGrowthCatalog catalog, PlayerProfile profile, GameDataStore data, IPlayerApi player)
        {
            _catalog = catalog;
            _profile = profile;
            _data = data;
            _player = player;
            _shopDayKey = ShopRules.DayKey(System.DateTime.Now);
            _profile.Changed += Refresh;
            Refresh(profile);
        }

        protected override void InitializeView() => _badge.Initialize();

        private void Update()
        {
            if (_kind != LobbyNotificationKind.Shop || _profile == null || !_profile.IsLoaded
                || _refreshingShopDay || Time.unscaledTime < _nextShopDayCheck) { return; }
            _nextShopDayCheck = Time.unscaledTime + 1f;
            string dayKey = ShopRules.DayKey(System.DateTime.Now);
            if (dayKey != _shopDayKey) { RefreshShopDayAsync(dayKey).Forget(Debug.LogException); }
        }

        // 하단 알림은 상점 화면이 꺼져 있어도 날짜 변경 후 구매·다시 뽑기 기록을 갱신한다
        private async UniTask RefreshShopDayAsync(string dayKey)
        {
            _refreshingShopDay = true;
            try
            {
                _profile.Apply(await _player.GetMe());
                _shopDayKey = dayKey;
            }
            catch (ApiException) { }
            finally
            {
                _nextShopDayCheck = Time.unscaledTime + 1f;
                _refreshingShopDay = false;
            }
        }

        private void Refresh(PlayerProfile profile)
        {
            bool available = false;
            if (_itemIndex != -2 && !IsLocked(profile))
            {
                switch (_kind)
                {
                    case LobbyNotificationKind.Equipment:
                        var equips = _catalog.Equips;
                        available = _itemIndex < 0 ? equips.Any(info => LobbyAvailability.CanUpgrade(profile, info))
                            : _itemIndex < equips.Count && LobbyAvailability.CanUpgrade(profile, equips[_itemIndex]);
                        break;
                    case LobbyNotificationKind.Skill:
                        var skills = _catalog.Skills;
                        available = _itemIndex < 0 ? skills.Any(info => LobbyAvailability.CanUpgrade(profile, info))
                            : _itemIndex < skills.Count && LobbyAvailability.CanUpgrade(profile, skills[_itemIndex]);
                        break;
                    case LobbyNotificationKind.Reward:
                        available = LobbyAvailability.HasAnyReward(profile);
                        break;
                    case LobbyNotificationKind.Quest:
                        available = QuestRules.HasClaimable(profile, _data.QuestBoards);
                        break;
                    case LobbyNotificationKind.Shop:
                        available = LobbyAvailability.HasFreeShopOffer(profile, _data);
                        break;
                }
            }
            _badge.SetVisible(available);
        }

        // 잠긴 컨텐츠(FeatureUnlocks)의 알림은 열릴 때까지 숨긴다
        private bool IsLocked(PlayerProfile profile)
        {
            string feature = _kind switch
            {
                LobbyNotificationKind.Equipment => "character",
                LobbyNotificationKind.Skill => "skill",
                LobbyNotificationKind.Quest => "quest",
                LobbyNotificationKind.Shop => "shop",
                _ => null,
            };
            return feature != null && !profile.IsFeatureUnlocked(feature);
        }

        protected override void DisposeView()
        {
            if (_profile != null)
            {
                _profile.Changed -= Refresh;
            }
        }
    }
}
