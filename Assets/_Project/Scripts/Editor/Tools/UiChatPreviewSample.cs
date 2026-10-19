using System;
using Game.Core;
using Game.View;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>실제 계정과 연결하지 않는 UI Workspace용 표시 데이터.</summary>
    internal static class UiChatPreviewSample
    {
        internal static void Apply(GameObject root, string sample = "기본")
        {
            if (root.TryGetComponent<ChatOpenButton>(out var opener))
            {
                ((GameObject)new SerializedObject(opener).FindProperty("_badge").objectReferenceValue).SetActive(true);
                ((TMP_Text)new SerializedObject(opener).FindProperty("_count").objectReferenceValue).text = "3";
            }
            if (!root.TryGetComponent<ChatView>(out var view)) { return; }
            view.Initialize();
            view.Open();
            var so = new SerializedObject(view);
            ((TMP_Text)so.FindProperty("_status").objectReferenceValue).gameObject.SetActive(false);
            ((UnityEngine.UI.Button)so.FindProperty("_retryButton").objectReferenceValue).gameObject.SetActive(false);
            ((TMP_Text)so.FindProperty("_presence").objectReferenceValue).text = "접속자 128명";
            var template = (ChatMessageRow)so.FindProperty("_rowTemplate").objectReferenceValue;
            var content = (RectTransform)so.FindProperty("_content").objectReferenceValue;
            var messages = new[]
            {
                new ChatMessage(4, "안녕하세요!", DateTimeOffset.Parse("2026-10-19T05:23:00Z")),
                new ChatMessage(12, "3스테이지 어렵네요 ㅠㅠ", DateTimeOffset.Parse("2026-10-19T05:24:00Z")),
                new ChatMessage(7, "저는 불꽃 스킬 추천해요!", DateTimeOffset.Parse("2026-10-19T05:24:00Z")),
                new ChatMessage(8, sample == "이모지" ? ChatEmojiPalette.Values[0] : "ㅎㅎㅎ", DateTimeOffset.Parse("2026-10-19T05:25:00Z")),
                new ChatMessage(9, sample == "이모지" ? "다들 화이팅! 👍🎉" : "다들 화이팅!", DateTimeOffset.Parse("2026-10-19T05:25:00Z"))
            };
            int index = 0;
            var names = new[] { "용감한 낙서냥", "낙서닌자", "펜-마스터", "꼬적이", "방어왕" };
            foreach (var message in messages)
            {
                var row = UnityEngine.Object.Instantiate(template, content);
                row.Bind(message);
                var fields = new SerializedObject(row);
                ((TMP_Text)fields.FindProperty("_sender").objectReferenceValue).text = names[index];
                row.SetPortrait(index);
                index++;
                row.gameObject.SetActive(true);
            }
            Canvas.ForceUpdateCanvases();
            ((UnityEngine.UI.ScrollRect)so.FindProperty("_scroll").objectReferenceValue).verticalNormalizedPosition = 0;
            if (sample == "이모지") { view.ToggleEmoji(true); }
        }
    }
}
