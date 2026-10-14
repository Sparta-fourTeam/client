using System.Collections.Generic;
using Game.Core;
using Game.Core.Messages;
using Game.View;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Display-only stage result inputs on the isolated Workspace clone; no battle, API or save calls.</summary>
    internal static class UiResultPreviewSample
    {
        private sealed class OwnedSkill : ISkillStatus
        {
            public int Id { get; }
            public int Level { get; }
            public SkillForm Form => default;
            public float CooldownRatio => 0;
            internal OwnedSkill(int id, int level) { Id = id; Level = level; }
        }

        internal static void Apply(GameObject root, string sample)
        {
            if (root.GetComponent<ResultPopupView>() is not { } view) { return; }
            bool cleared = sample != "패배";
            var data = new GameDataStore();
            var profile = new PlayerProfile(data);
            profile.Apply(new PlayerSnapshot { stageProgress = new List<StageProgressRow>() });
            var stage = new StageContext();
            stage.Set(1);
            var result = new StageResult(cleared, 653, 20, 259, 150,
                new[]
                {
                    new ItemAmount { itemId = "book.arrow", quantity = 45 },
                    new ItemAmount { itemId = "book.weapon", quantity = 1 },
                    new ItemAmount { itemId = ItemIds.GemChest, quantity = 1 }
                }, 75, cleared ? 3 : 0,
                new[] { new SkillDamage(3, 5047), new SkillDamage(4, 4865), new SkillDamage(1, 2868), new SkillDamage(2, 1200) });
            var owned = new ISkillStatus[] { new OwnedSkill(3, 6), new OwnedSkill(4, 5), new OwnedSkill(1, 7), new OwnedSkill(2, 2) };
            var so = new SerializedObject(view);
            SetText(so, "_titleText", cleared ? "Clear !" : "Fail !");
            SetText(so, "_stageText", $"Stage {stage.StageId}");
            SetText(so, "_killsText", $"처치\n{result.Kills}");
            SetText(so, "_waveText", $"도달 웨이브\n{result.ReachedWave}");
            SetText(so, "_timeText", "플레이 시간\n04:19");
            SetText(so, "_bubbleText", cleared
                ? (!profile.IsStageCleared(1) ? "첫 클리어! 로비에서 새 무기를 확인해 봐요!" : "어때요? 이 정도쯤이야!")
                : "으으... 다음엔 꼭...!");
            var ribbon = (UnityEngine.UI.Image)so.FindProperty("_titleRibbon").objectReferenceValue;
            var sprite = (Sprite)so.FindProperty(cleared ? "_clearRibbon" : "_failRibbon").objectReferenceValue;
            if (sprite != null) { ribbon.sprite = sprite; }
            ribbon.color = so.FindProperty(cleared ? "_clearRibbonColor" : "_failRibbonColor").colorValue;
            var icons = (ItemIconTable)so.FindProperty("_itemIcons").objectReferenceValue;
            ((ResultRewardListView)so.FindProperty("_rewardList").objectReferenceValue)
                .Show(RewardRows.Build(icons, data, result.RewardGold, result.RewardExp, result.RewardItems));
            var skills = (SkillAssetTable)so.FindProperty("_skillAssets").objectReferenceValue;
            ((ResultDamageListView)so.FindProperty("_damageList").objectReferenceValue)
                .Show(SkillDamageRows.Build(result.SkillDamages, owned, status => skills.GetHudIcon(status.Id)));
            ((ResultStarsView)so.FindProperty("_starsView").objectReferenceValue).Show(cleared, result.ClearRating);
            var tip = (ResultTipView)so.FindProperty("_tip").objectReferenceValue;
            if (cleared) { tip.Hide(); } else { tip.Show(); }
        }

        private static void SetText(SerializedObject view, string field, string text)
        {
            if (view.FindProperty(field)?.objectReferenceValue is TMP_Text label) { label.text = text; }
        }
    }
}
