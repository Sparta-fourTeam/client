using Game.Core;
using Game.View;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Display-only reference sample applied to isolated UI Workspace clones.</summary>
    public static class UiSkillPreviewSample
    {
        public static void Apply(GameObject root)
        {
            var view = root.GetComponent<SkillScreenView>();
            if (view == null) { return; }
            var serialized = new SerializedObject(view);
            var slots = serialized.FindProperty("_slots");
            var assets = AssetDatabase.LoadAssetAtPath<SkillAssetTable>("Assets/_Project/Data/SkillAssetTable.asset");
            string[] ids = { "1", "2", "3", "4", "5", "6", "7", "18", "16", "14" };
            var elements = new System.Collections.Generic.Dictionary<string, Element>
            {
                ["1"] = Element.Neutral,
                ["2"] = Element.Fire,
                ["3"] = Element.Lightning,
                ["4"] = Element.Ice,
                ["5"] = Element.Earth,
                ["6"] = Element.Ice,
                ["7"] = Element.Lightning,
                ["18"] = Element.Lightning,
            };
            for (int i = 0; i < slots.arraySize; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i).objectReferenceValue as SkillListSlotView;
                if (slot == null) { continue; }
                if (i >= ids.Length) { slot.Hide(); continue; }
                slot.Bind(new SkillInfo
                {
                    Id = ids[i],
                    Element = elements.TryGetValue(ids[i], out var element) ? element : Element.Neutral,
                    IsUnlocked = i < 8,
                    UnlockLevel = i == 8 ? 6 : 7,
                    Level = i == 0 ? 2 : i == 4 ? 1 : 0
                }, null, assets != null ? assets.GetHudIcon(ids[i]) : null);
                foreach (var badge in slot.GetComponentsInChildren<NotificationBadgeView>(true))
                {
                    badge.SetVisible(false);
                }
            }
        }
    }
}
