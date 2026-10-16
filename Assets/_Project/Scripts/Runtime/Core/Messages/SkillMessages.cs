using System.Collections.Generic;

namespace Game.Core.Messages
{
    /// <summary>보유한 주 스킬이 대상을 향해 시전됐다. 자식 공격은 별도로 발행하지 않는다.</summary>
    public readonly struct SkillFired
    {
        public int SkillId { get; }
        /// <summary>시전 당시 강화가 반영된 광선 지속시간(게임 시간). 즉발·투사체는 0이다.</summary>
        public float Duration { get; }
        public SkillFired(int skillId, float duration = 0f) { SkillId = skillId; Duration = duration; }
    }

    /// <summary>보유 스킬이 바뀌었다(획득, 강화). 지금 보유한 스킬 전체의 스냅샷이다.
    /// 상태를 나타내는 메시지라 Buffered로 발행한다: 늦게 구독한 HUD도 현재 보유 목록을 바로 받는다.
    /// 쿨타임은 이 메시지에 담기지 않는다. 목록 안의 ISkillStatus를 뷰가 매 프레임 읽는다</summary>
    public readonly struct SkillChanged
    {
        public IReadOnlyList<ISkillStatus> Skills { get; }

        public SkillChanged(IReadOnlyList<ISkillStatus> skills)
        {
            Skills = skills;
        }
    }
}
