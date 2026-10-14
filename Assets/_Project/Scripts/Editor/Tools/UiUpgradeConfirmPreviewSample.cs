using System.Collections.Generic;
using Game.Core;
using Game.View;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Opens only the isolated Workspace clone; no purchase or saved profile is used.</summary>
    internal static class UiUpgradeConfirmPreviewSample
    {
        internal static void Apply(GameObject root)
        {
            var view = root.GetComponent<UpgradeConfirmPopupView>();
            if (view == null) { return; }
            var data = new GameDataStore();
            var definition = data.Upgrades.GetOrThrow("equipment.top");
            var profile = new PlayerProfile(data);
            int exp = 0;
            for (int level = 1; level < data.UnlockLevelOf(definition); level++)
            {
                exp += data.PlayerLevels.RequiredToNext(level);
            }
            profile.Apply(new PlayerSnapshot
            {
                gold = 1215,
                exp = exp,
                upgrades = new List<UpgradeRow>(),
                items = new List<ItemAmount>
                {
                    new() { itemId = definition.MaterialItemId, quantity = definition.MaterialAt(1) }
                }
            });
            var so = new SerializedObject(view);
            so.FindProperty("_transition").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            view.Initialize();
            view.Construct(profile, data, null);
            view.Show("로브", "equipment.top", true);
        }
    }
}
