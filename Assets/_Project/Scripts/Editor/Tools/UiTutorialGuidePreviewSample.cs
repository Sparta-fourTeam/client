using Game.Core;
using Game.View;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Displays the real guide line and target on the isolated Workspace clone.</summary>
    internal static class UiTutorialGuidePreviewSample
    {
        private const string MaterialName = "Workspace Tutorial Spotlight";

        internal static void Apply(GameObject container)
        {
            var guide = container.GetComponentInChildren<TutorialGuideView>(true);
            var nav = container.GetComponentInChildren<LobbyNavView>(true);
            if (guide == null || nav == null) { return; }
            var so = new SerializedObject(guide);
            var root = (GameObject)so.FindProperty("_root").objectReferenceValue;
            var dialog = (RectTransform)so.FindProperty("_dialog").objectReferenceValue;
            var text = (TMP_Text)so.FindProperty("_dialogText").objectReferenceValue;
            var spotlight = (UnityEngine.UI.Image)so.FindProperty("_spotlight").objectReferenceValue;
            root.SetActive(true);
            nav.SetSkillLocked(false);
            text.text = new GameDataStore().TutorialPresentations.For("log_upgrade").Lines[0];
            var area = (RectTransform)root.transform;
            var corners = new Vector3[4];
            nav.SkillTab.GetWorldCorners(corners);
            Vector2 min = area.InverseTransformPoint(corners[0]);
            Vector2 max = area.InverseTransformPoint(corners[2]);
            Vector2 center = (min + max) * .5f;
            Vector2 half = (max - min) * .5f + Vector2.one * so.FindProperty("_holePadding").floatValue;
            var material = new Material(spotlight.material) { name = MaterialName, hideFlags = HideFlags.HideAndDontSave };
            spotlight.material = material;
            material.SetVector("_Hole", new Vector4(center.x, center.y, half.x, half.y));
            material.SetVector("_Params", new Vector4(so.FindProperty("_holeRadius").floatValue, so.FindProperty("_holeSoftness").floatValue, 0, 0));
            material.SetColor("_RingColor", so.FindProperty("_ringColor").colorValue);
            dialog.localPosition = new Vector3(0, center.y < 0 ? so.FindProperty("_dialogOffset").floatValue : -so.FindProperty("_dialogOffset").floatValue, 0);
            ((TutorialRaycastHole)so.FindProperty("_blocker").objectReferenceValue).Target = nav.SkillTab;
            Advance(container, 0);
            text.ForceMeshUpdate(true);
        }

        internal static bool HasPulse(GameObject container) => container.GetComponentInChildren<TutorialGuideView>(true) != null;

        internal static void Advance(GameObject container, double seconds)
        {
            foreach (var guide in container.GetComponentsInChildren<TutorialGuideView>(true))
            {
                var so = new SerializedObject(guide);
                var image = (UnityEngine.UI.Image)so.FindProperty("_spotlight").objectReferenceValue;
                if (image.material == null || image.material.name != MaterialName) { continue; }
                float phase = Mathf.Repeat((float)seconds / Mathf.Max(.1f, so.FindProperty("_pulseSeconds").floatValue), 1);
                image.material.SetVector("_Ring", new Vector4(phase, so.FindProperty("_ringSpread").floatValue, so.FindProperty("_ringThickness").floatValue, 0));
            }
        }

        internal static void Dispose(GameObject container)
        {
            foreach (var guide in container.GetComponentsInChildren<TutorialGuideView>(true))
            {
                var image = (UnityEngine.UI.Image)new SerializedObject(guide).FindProperty("_spotlight").objectReferenceValue;
                if (image.material == null || image.material.name != MaterialName) { continue; }
                var material = image.material;
                image.material = null;
                Object.DestroyImmediate(material);
            }
        }
    }
}
