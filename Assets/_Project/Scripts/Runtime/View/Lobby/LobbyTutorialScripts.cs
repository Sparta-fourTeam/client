using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.View
{
    /// <summary>가이드 튜토리얼의 한 단계: 강조할 버튼과, 지금 이 단계에 와 있는지(화면 상태).
    /// 대사는 같은 순번의 TutorialDefinition.Lines에서 온다</summary>
    public sealed class TutorialStep
    {
        public readonly Func<RectTransform> Target;
        public readonly Func<bool> Reached;

        public TutorialStep(Func<RectTransform> target, Func<bool> reached)
        {
            Target = target;
            Reached = reached;
        }
    }

    /// <summary>Tutorials.json의 Script 이름 → 로비 가이드 단계 템플릿. 단계는 순서대로이며 가이드는 Reached가 참인 마지막 단계를 강조한다
    /// (팝업을 닫아 앞 화면으로 돌아가면 앞 단계로 돌아간다). 첫 단계의 Reached는 항상 참이어야 한다.
    /// 같은 흐름의 가이드(다른 무기 강화 등)는 Tutorials.json에 행만 추가하고, 새 흐름이면 여기에 템플릿을 하나 추가한다</summary>
    public static class LobbyTutorialScripts
    {
        /// <summary>스킬 탭 → 스킬 칸 → 강화 버튼 → 강화 확인. 대상 스킬은 완료 조건의 UpgradeId다</summary>
        public const string SkillUpgrade = "skillUpgrade";

        /// <summary>템플릿이 없으면 null</summary>
        public static IReadOnlyList<TutorialStep> Create(TutorialDefinition tutorial, IUiRegistry ui) => tutorial.Script switch
        {
            SkillUpgrade => SkillUpgradeSteps(ui, tutorial.Complete.UpgradeId),
            _ => null,
        };

        private static IReadOnlyList<TutorialStep> SkillUpgradeSteps(IUiRegistry ui, string upgradeId)
        {
            var nav = ui.Get<LobbyNavView>();
            var screen = ui.Get<SkillScreenView>();
            var popup = ui.Get<SkillUpgradePopupView>();
            var confirm = ui.Get<UpgradeConfirmPopupView>();
            return new[]
            {
                new TutorialStep(() => nav.SkillTab, () => true),
                new TutorialStep(() => screen.SlotOf(upgradeId), () => screen.IsVisible),
                new TutorialStep(() => popup.UpgradeButton, () => popup.OpenUpgradeId == upgradeId),
                new TutorialStep(() => confirm.ConfirmButton, () => confirm.IsOpen),
            };
        }
    }
}
