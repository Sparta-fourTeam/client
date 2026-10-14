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
                    // Disabled Selectables stay outside the global navigation registry.
                    if (toggle.graphic != null) { toggle.graphic.enabled = on; }
                }
            }
            bool standard = sample == "표준 프레임";
            ToggleState("_damageToggle", !standard);
            ToggleState("_debuffToggle", !standard);
            ToggleState("_autoEnterToggle", standard);
            ToggleState("_frameHighToggle", !standard);
            ToggleState("_frameStandardToggle", standard);
            if (sample == "일시 비활성")
            {
                if (serialized.FindProperty("_languageLabel").objectReferenceValue is TMP_Text label)
                { label.text = "일시 비활성"; }
            }
            var sliders = serialized.FindProperty("_sliders");
            var texts = serialized.FindProperty("_valueTexts");
            for (int i = 0; i < sliders.arraySize && i < texts.arraySize; i++)
            {
                var slider = sliders.GetArrayElementAtIndex(i).objectReferenceValue as Slider;
                var text = texts.GetArrayElementAtIndex(i).objectReferenceValue as TMP_Text;
                if (slider != null && text != null)
                {
                    float value = sample == "음소거" ? 0f : sample == "중간 음량" ? 0.5f : 1f;
                    slider.SetValueWithoutNotify(Mathf.Lerp(slider.minValue, slider.maxValue, value));
                    // Slider.OnEnable does not run on an isolated preview clone.
                    if (slider.fillRect != null)
                    {
                        slider.fillRect.anchorMin = Vector2.zero;
                        slider.fillRect.anchorMax = new Vector2(value, 1f);
                    }
                    if (slider.handleRect != null)
                    {
                        slider.handleRect.anchorMin = new Vector2(value, 0f);
                        slider.handleRect.anchorMax = new Vector2(value, 1f);
                    }
                    text.text = $"{Mathf.RoundToInt(value * 100f)}%";
                }
            }
        }
    }
}
