using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Game.View
{
    /// <summary>보유 스킬을 슬롯에 채운다. 스킬이 바뀔 때만 SkillChanged를 받고, 남는 칸은 빈 틀로 둔다.
    /// 슬롯을 누르고 있는 동안은 그 스킬의 사정거리를 점선으로 보여 준다</summary>
    public sealed class SkillHudView : HudView
    {
        [SerializeField] private SkillSlotView[] _slots;
        [FormerlySerializedAs("_iconTable")]
        [SerializeField] private SkillAssetTable _assets;
        [Tooltip("슬롯을 누르고 있는 동안 사정거리를 그리는 점선 표시. 월드에 한 번 만들어 쓴다")]
        [SerializeField] private SkillRangeIndicator _rangeIndicatorPrefab;

        private SkillController _skills;
        private SkillRangeIndicator _rangeIndicator;
        private int _heldSkillId;

        [Inject]
        public void Construct(IBufferedSubscriber<SkillChanged> skillChanged, SkillController skills)
        {
            _skills = skills;
            Track(skillChanged.Subscribe(OnSkillChanged));
            foreach (var slot in _slots)
            {
                slot.HoldStarted += OnHoldStarted;
                slot.HoldEnded += OnHoldEnded;
            }
        }

        private void OnHoldStarted(ISkillStatus status)
        {
            _heldSkillId = status.Id;
            ShowRange();
        }

        private void OnHoldEnded()
        {
            _heldSkillId = 0;
            if (_rangeIndicator != null) { _rangeIndicator.Hide(); }
        }

        // 누르는 동안 매 프레임 다시 그려 플레이어 위치나 사정거리가 바뀌어도 따라간다
        private void Update()
        {
            if (_heldSkillId != 0) { ShowRange(); }
        }

        private void ShowRange()
        {
            if (_skills == null || _rangeIndicatorPrefab == null || !_skills.TryGetRange(_heldSkillId, out var range)) { return; }
            if (_rangeIndicator == null) { _rangeIndicator = Instantiate(_rangeIndicatorPrefab); }
            _rangeIndicator.Show(range);
        }

        protected override void OnDestroy()
        {
            foreach (var slot in _slots)
            {
                if (slot == null) { continue; }
                slot.HoldStarted -= OnHoldStarted;
                slot.HoldEnded -= OnHoldEnded;
            }
            if (_rangeIndicator != null) { Destroy(_rangeIndicator.gameObject); }
            base.OnDestroy();
        }

        private void Awake()
        {
            // 슬롯 수의 출처는 SkillSlotLimit 하나다. 프리팹의 슬롯이 다르면 스킬이 보이지 않거나 빈 칸이 남는다
            if (_slots != null && _slots.Length != SkillSlotLimit.Max)
            {
                Debug.LogWarning($"[SkillHudView] 슬롯이 {_slots.Length}칸인데 스킬 슬롯 수(SkillSlotLimit.Max)는 {SkillSlotLimit.Max}개입니다. 프리팹의 슬롯 수를 맞추세요");
            }
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
                    _slots[i].Bind(status, _assets != null ? _assets.GetHudIcon(status.AssetKey) : null);
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
