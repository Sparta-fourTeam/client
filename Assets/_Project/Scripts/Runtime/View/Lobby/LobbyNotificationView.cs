using System.Linq;
using Game.Core;
using UnityEngine;
using VContainer;

namespace Game.View
{
    public enum LobbyNotificationKind { Equipment, Skill, Reward }

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

        [Inject]
        public void Construct(IGrowthCatalog catalog, PlayerProfile profile)
        {
            _catalog = catalog;
            _profile = profile;
            _profile.Changed += Refresh;
            Refresh(profile);
        }

        protected override void InitializeView() => _badge.Initialize();

        private void Refresh(PlayerProfile profile)
        {
            bool available = false;
            if (_itemIndex != -2)
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
                }
            }
            _badge.SetVisible(available);
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
