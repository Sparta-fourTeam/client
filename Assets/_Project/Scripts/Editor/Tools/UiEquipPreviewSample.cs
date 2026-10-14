using Game.Core;
using Game.View;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Editor
{
    /// <summary>Editor-only values on the isolated UI Workspace clone; no backend requests.</summary>
    internal static class UiEquipPreviewSample
    {
        internal static void Apply(GameObject root)
        {
            var view = root.GetComponent<EquipUpgradePopupView>();
            if (view == null) { return; }
            var so = new SerializedObject(view);
            var data = new GameDataStore();
            var def = data.Upgrades.GetOrThrow("equipment.top");
            var icons = (ItemIconTable)so.FindProperty("_icons").objectReferenceValue;
            void Text(string field, string value) => ((TMP_Text)so.FindProperty(field).objectReferenceValue).text = value;
            Text("_nameText", "로브");
            Text("_currentLevelText", $"Lv.0/{def.MaxLevel}");
            Text("_nextLevelText", $"Lv.1/{def.MaxLevel}");
            Text("_coinText", CostFormat.HaveNeed(1215, def.CostAt(1)));
            Text("_materialText", CostFormat.HaveNeed(1, def.MaterialAt(1)));
            Text("_materialNameText", data.Items.GetOrThrow(def.MaterialItemId).Name);
            foreach (var field in new[] { "_equipmentIcon", "_nextEquipmentIcon" })
            { var image = (Image)so.FindProperty(field).objectReferenceValue; image.sprite = icons.Get("equipment.top"); image.color = Color.white; image.enabled = true; }
            var material = (Image)so.FindProperty("_materialIcon").objectReferenceValue;
            material.sprite = icons.Get(def.MaterialItemId); material.color = Color.white; material.enabled = true;
            foreach (var field in new[] { "_currentRows", "_nextRows" })
            {
                bool next = field == "_nextRows";
                var array = so.FindProperty(field);
                for (int i = 0; i < array.arraySize; i++)
                {
                    var row = (StatRowView)array.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (i >= def.Effects.Count + 1) { row.Hide(); continue; }
                    var label = i < def.Effects.Count ? EquipEffectText.Name(def.Effects[i]) : "장비 레벨";
                    var value = i < def.Effects.Count ? EquipEffectText.Value(def.Effects[i], next ? 1 : 0) : next ? "1" : "0";
                    row.Bind(label, next ? $"<color=#3DEBB4>{value}</color>" : value);
                }
            }
        }
    }
}
