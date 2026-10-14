using Game.Core.Combat;

namespace Game.Core
{
    /// <summary>데미지 숫자의 종류. 모양과 움직임이 다르다</summary>
    public enum DamagePopupKind
    {
        /// <summary>스킬이 준 일반 피해</summary>
        Normal,

        /// <summary>치명타 피해</summary>
        Critical,

        /// <summary>점화·동상 같은 상태이상 지속 피해(스킬 밖의 피해)</summary>
        Status,
    }

    public static class DamagePopupKinds
    {
        /// <summary>피해 정보로 숫자 종류를 정한다. 치명타는 스킬 피해에만 있으므로 겹치지 않지만, 겹치면 치명타를 먼저 본다</summary>
        public static DamagePopupKind Of(DamageInfo info)
        {
            if (info.IsCritical) { return DamagePopupKind.Critical; }
            return info.IsFromSkill ? DamagePopupKind.Normal : DamagePopupKind.Status;
        }
    }
}
