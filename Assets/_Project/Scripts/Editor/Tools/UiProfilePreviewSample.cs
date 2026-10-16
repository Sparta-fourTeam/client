using System.Collections.Generic;
using Game.Core;
using Game.View;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Populates the isolated profile preview through its public APIs; never touches saved player data.</summary>
    internal static class UiProfilePreviewSample
    {
        internal static void Apply(GameObject root)
        {
            var view = root.GetComponent<ProfilePopupView>();
            if (view == null) { return; }
            var data = new GameDataStore();
            var profile = new PlayerProfile(data);
            profile.Apply(new PlayerSnapshot
            {
                nickname = "Nameless_285709",
                playerId = "7ff0d5b53cb347b29318c1acb03d4635",
                exp = 420,
                stageProgress = new List<StageProgressRow> { new() { stageId = 3 } }
            });
            using var catalog = new DummyGrowthCatalog(profile, data);
            // Preview opens immediately; runtime's serialized transition remains unchanged.
            var so = new SerializedObject(view);
            so.FindProperty("_transition").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            view.Initialize();
            view.Construct(catalog, profile, null);
            view.Open();
        }
    }
}
