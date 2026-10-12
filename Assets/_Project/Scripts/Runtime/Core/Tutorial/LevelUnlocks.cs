using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public enum UnlockKind
    {
        Weapon,
        Equipment,
        Feature,
    }

    /// <summary>레벨업 팝업의 해금 한 줄. IconKey는 Kind마다 다른 아이콘 표에서 찾는다 (무기: SkillAssetTable의 assetKey)</summary>
    public sealed class UnlockNotice
    {
        public UnlockKind Kind;
        public string Name;
        public string IconKey;
    }

    /// <summary>플레이어 레벨이 오를 때 새로 열리는 것을 알려주는 출처. 레벨업 팝업은 등록된 출처를 모두 모아 줄을 만든다.
    /// 새 해금 종류(장비·기능 등)는 출처를 하나 만들어 LobbyLifetimeScope에 등록하면 된다</summary>
    public interface ILevelUnlockSource
    {
        /// <summary>레벨이 fromLevel에서 toLevel로 오르며 열린 것 (fromLevel 초과, toLevel 이하)</summary>
        IEnumerable<UnlockNotice> Unlocked(int fromLevel, int toLevel);
    }

    /// <summary>해금 레벨(SkillData.unlockLevel)을 채워 열린 무기</summary>
    public sealed class WeaponUnlockSource : ILevelUnlockSource
    {
        private readonly GameDataStore _data;

        public WeaponUnlockSource(GameDataStore data)
        {
            _data = data;
        }

        public IEnumerable<UnlockNotice> Unlocked(int fromLevel, int toLevel) =>
            SkillUnlockRule.NewlyUnlocked(_data.LoadSkills(), fromLevel, toLevel)
                .Select(skill => new UnlockNotice { Kind = UnlockKind.Weapon, Name = skill.name, IconKey = skill.assetKey });
    }
}
