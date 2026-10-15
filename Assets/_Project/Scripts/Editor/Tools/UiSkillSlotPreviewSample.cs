using DG.Tweening;
using Game.Core;
using Game.View;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>스킬 슬롯의 강화(레벨 숫자 펀치)와 형태 변환 연출을 편집용 예시 상태로 재생한다. 게임 데이터는 건드리지 않는다.</summary>
    public static class UiSkillSlotPreviewSample
    {
        private const int SampleSkillId = 2;
        private const SkillForm SampleForm = SkillForm.FlameArrow;
        // 전투 HUD 등장 연출(슬롯이 미끄러져 들어오는 시간)이 끝난 뒤에 보이도록 늦춘다
        private const float UpgradeDelaySeconds = 1.3f;

        private sealed class Status : ISkillStatus
        {
            public int Id => SampleSkillId;
            public int Level { get; set; }
            public SkillForm Form { get; set; }
            public float CooldownRatio => 0.35f;
        }

        public static bool Has(GameObject root) => root.GetComponentInChildren<SkillSlotView>(true) != null;

        /// <summary>첫 슬롯을 레벨 1로 채우고, 잠시 뒤 강화와 형태 변환을 한꺼번에 일으킨다. 숫자 펀치와 아이콘 변환이 같이 보인다.</summary>
        public static void Play(GameObject root)
        {
            var slot = root.GetComponentInChildren<SkillSlotView>(true);
            if (slot == null) { return; }
            var assets = AssetDatabase.LoadAssetAtPath<SkillAssetTable>("Assets/_Project/Data/SkillAssetTable.asset");
            Sprite Icon(SkillForm form) => assets != null ? assets.GetFormIcon(form, SampleSkillId) : null;

            var status = new Status { Level = 1, Form = SkillForm.Default };
            slot.Bind(status, Icon(SkillForm.Default));
            DOVirtual.DelayedCall(UpgradeDelaySeconds, () =>
            {
                status.Level = 2;
                slot.Bind(status, Icon(SkillForm.Default));
                status.Form = SampleForm;
                slot.Bind(status, Icon(SampleForm));
            }, true).SetLink(slot.gameObject);
        }
    }
}
