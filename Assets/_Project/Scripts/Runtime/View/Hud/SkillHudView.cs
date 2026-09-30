using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>보유 스킬을 슬롯에 채운다. 스킬이 바뀔 때만 SkillChanged를 받고, 남는 칸은 빈 틀로 둔다</summary>
    public sealed class SkillHudView : HudView
    {
        [SerializeField] private SkillSlotView[] _slots;
        [SerializeField] private SkillIconTable _iconTable;

        [Inject]
        public void Construct(IBufferedSubscriber<SkillChanged> skillChanged)
        {
            Track(skillChanged.Subscribe(OnSkillChanged));
        }

        private void OnSkillChanged(SkillChanged message)
        {
            // Buffered struct는 구독 즉시 기본값이 오고, 그때 Skills는 null이다
            int count = message.Skills?.Count ?? 0;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (i < count)
                {
                    var status = message.Skills[i];
                    _slots[i].Bind(status, _iconTable != null ? _iconTable.Find(status.IconKey) : null);
                }
                else
                {
                    _slots[i].Clear();
                }
            }

            if (count > _slots.Length)
            {
                Debug.LogWarning($"[SkillHudView] 스킬이 {count}개인데 슬롯은 {_slots.Length}칸입니다. 넘치는 스킬은 표시하지 않습니다");
            }
        }
    }
}
