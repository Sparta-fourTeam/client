using Game.View;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Existing submit failure presentation on the isolated Workspace clone.</summary>
    internal static class UiSubmitPreviewSample
    {
        internal static void Apply(GameObject root, string sample)
        {
            if (root.GetComponent<SubmitResultPopupView>() is not { } view) { return; }
            var so = new SerializedObject(view);
            bool retryable = sample != "제출 거절" && sample != "재시도 불가";
            if (so.FindProperty("_messageText").objectReferenceValue is TMP_Text message)
            {
                message.text = sample == "제출 거절"
                    ? SubmitResultMessages.ForRejected("INVALID_ID")
                    : SubmitResultMessages.ForFailed(retryable);
            }
            if (so.FindProperty("_retryButton").objectReferenceValue is UnityEngine.UI.Button retry)
            {
                retry.gameObject.SetActive(retryable);
            }
            if (so.FindProperty("_panel").objectReferenceValue is GameObject panel)
            {
                panel.SetActive(true);
            }
        }
    }
}
