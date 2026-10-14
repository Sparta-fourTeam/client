using Game.Core;
using Game.View;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Catalog-backed roster and maximum reward review on the isolated Workspace clone.</summary>
    internal static class UiPausePreviewSample
    {
        internal static void Apply(GameObject root)
        {
            if (root.GetComponent<PauseMenuView>() is not { } view) { return; }
            var data = new GameDataStore();
            var stage = data.StageOrFirst(3);
            var so = new SerializedObject(view);
            if (so.FindProperty("_titleText").objectReferenceValue is TMP_Text title)
            {
                title.text = $"제 {stage.Id} 관문";
            }
            var display = (MonsterDisplayTable)so.FindProperty("_monsterDisplay").objectReferenceValue;
            ((StageMonsterListView)so.FindProperty("_monsterList").objectReferenceValue)
                .Show(MonsterRows.Build(display, data, stage.Id));
            var maximum = StageRewardRules.Maximum(data.StageRewards.GetOrThrow(stage.Id));
            var icons = (ItemIconTable)so.FindProperty("_itemIcons").objectReferenceValue;
            ((ResultRewardListView)so.FindProperty("_obtainableList").objectReferenceValue)
                .Show(RewardRows.Build(icons, data, maximum.Coin, maximum.Exp, maximum.Items));
        }
    }
}
