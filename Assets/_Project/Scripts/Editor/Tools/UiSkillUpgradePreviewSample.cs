using System.Collections.Generic;
using Game.Core;
using Game.View;
using UnityEditor;
using UnityEngine;
namespace Game.Editor
{
    /// <summary>Opens an isolated Workspace clone with catalog data; never touches player saves or APIs.</summary>
    internal static class UiSkillUpgradePreviewSample
    {
        internal static void Apply(GameObject root, string sample)
        {
            var view = root.GetComponent<SkillUpgradePopupView>();
            if (view == null) { return; }
            var data = new GameDataStore();
            var profile = new PlayerProfile(data);
            int level = sample == "최대 레벨" ? data.Upgrades.GetOrThrow("shuriken").MaxLevel : 2;
            var definition = data.Upgrades.GetOrThrow("shuriken");
            bool enough = sample == "강화 가능";
            profile.Apply(new PlayerSnapshot
            {
                gold = enough ? definition.CostAt(level + 1) : 1215,
                upgrades = new List<UpgradeRow> { new() { upgradeId = "shuriken", level = level } },
                items = new List<ItemAmount> { new() { itemId = "book.arrow", quantity = enough ? definition.MaterialAt(level + 1) : 32 } }
            });
            using var catalog = new DummyGrowthCatalog(profile, data);
            var so = new SerializedObject(view);
            so.FindProperty("_transition").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            view.Initialize();
            view.Construct(catalog, profile, data);
            int index = 0;
            for (int i = 0; i < catalog.Skills.Count; i++) { if (catalog.Skills[i].Id == "1") { index = i; break; } }
            view.Open(index);
        }
    }
}
