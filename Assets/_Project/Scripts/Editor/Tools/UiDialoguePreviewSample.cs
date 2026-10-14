using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.View;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>실제 도입 대사를 Workspace 복제본에만 표시한다. 게임 진행이나 저장 상태는 바꾸지 않는다.</summary>
    internal static class UiDialoguePreviewSample
    {
        private static DialogueLine FirstLine() => new GameDataStore().TutorialPresentations.For("intro_battle").Dialogue[0];

        internal static void Apply(GameObject root)
        {
            if (root.GetComponent<DialogueView>() is not { } view) { return; }
            var line = FirstLine();
            var so = new SerializedObject(view);
            ((GameObject)so.FindProperty("_root").objectReferenceValue).SetActive(true);
            var left = (UnityEngine.UI.Image)so.FindProperty("_leftPortrait").objectReferenceValue;
            var right = (UnityEngine.UI.Image)so.FindProperty("_rightPortrait").objectReferenceValue;
            left.gameObject.SetActive(!line.IsRight);
            right.gameObject.SetActive(line.IsRight);
            var portrait = line.IsRight ? right : left;
            if (so.FindProperty(line.IsRight ? "_rightPortraitGlyph" : "_leftPortraitGlyph").objectReferenceValue is UnityEngine.UI.Image glyph)
            {
                portrait = glyph;
            }
            var portraits = so.FindProperty("_portraits");
            for (int i = 0; i < portraits.arraySize; i++)
            {
                var entry = portraits.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("Speaker").stringValue != line.Speaker) { continue; }
                portrait.sprite = (Sprite)entry.FindPropertyRelative("Sprite").objectReferenceValue;
                portrait.color = entry.FindPropertyRelative("Tint").colorValue;
                break;
            }
            var tag = (RectTransform)so.FindProperty("_nameTag").objectReferenceValue;
            float inset = so.FindProperty("_nameTagInset").floatValue;
            tag.anchorMin = tag.anchorMax = new Vector2(line.IsRight ? 1f : 0f, tag.anchorMin.y);
            tag.pivot = new Vector2(line.IsRight ? 1f : 0f, tag.pivot.y);
            tag.anchoredPosition = new Vector2(line.IsRight ? -inset : inset, tag.anchoredPosition.y);
            ((TMP_Text)so.FindProperty("_nameText").objectReferenceValue).text = line.Speaker;
            var body = (TMP_Text)so.FindProperty("_bodyText").objectReferenceValue;
            body.text = line.Text;
            body.maxVisibleCharacters = int.MaxValue;
            var auto = (UnityEngine.UI.Image)so.FindProperty("_autoBackground").objectReferenceValue;
            var off = (Sprite)so.FindProperty("_autoOffSprite").objectReferenceValue;
            if (off != null) { auto.sprite = off; auto.color = Color.white; }
            else { auto.color = so.FindProperty("_autoOffColor").colorValue; }
            ((TMP_Text)so.FindProperty("_autoLabel").objectReferenceValue).text = "AUTO OFF";
        }

        internal static async UniTask PlayAsync(DialogueView view)
        {
            view.Initialize();
            try { await view.PlayAsync(new[] { FirstLine() }); }
            catch (OperationCanceledException) { /* Workspace 복제본을 닫으면 public Dispose가 대기를 취소한다. */ }
        }
    }
}
