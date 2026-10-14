using Game.View;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>Display-only settings values for an isolated UI Workspace prefab clone.</summary>
    internal static class UiSettingsPreviewSample
    {
        internal static void Apply(GameObject root, string sample)
        {
            var view = root.GetComponent<SettingsPopupView>();
            if (view == null) { return; }
            var serialized = new SerializedObject(view);
            void ToggleState(string field, bool on)
            {
                if (serialized.FindProperty(field).objectReferenceValue is Toggle toggle)
                {
                    toggle.SetIsOnWithoutNotify(on);
                }
            }
            bool standard = sample == "표준 프레임";
            ToggleState("_damageToggle", !standard);
            ToggleState("_debuffToggle", !standard);
            ToggleState("_autoEnterToggle", standard);
            ToggleState("_frameHighToggle", !standard);
            ToggleState("_frameStandardToggle", standard);
            var sliders = serialized.FindProperty("_sliders");
            var texts = serialized.FindProperty("_valueTexts");
            for (int i = 0; i < sliders.arraySize && i < texts.arraySize; i++)
            {
                var slider = sliders.GetArrayElementAtIndex(i).objectReferenceValue as Slider;
                var text = texts.GetArrayElementAtIndex(i).objectReferenceValue as TMP_Text;
                if (slider != null && text != null)
                {
                    text.text = $"{Mathf.RoundToInt(slider.normalizedValue * 100f)}%";
                }
            }
        }
    }
}
