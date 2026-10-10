using System;
using System.Collections.Generic;
using System.Linq;
using Game.View;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    internal static class UiAssetAudit
    {
        internal sealed class Issue
        {
            public string AssetPath, ObjectPath, Message;
            public bool IsError;
        }

        internal static List<Issue> InspectAll()
        {
            var issues = new List<Issue>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/UI" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try { InspectPrefab(root, path, issues); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string scene in new[] { "Lobby", "Stage" })
            {
                string path = $"Assets/_Project/Settings/UI/{scene}Ui.asset";
                var composition = AssetDatabase.LoadAssetAtPath<UiComposition>(path);
                if (composition == null) { Add(issues, path, "", "화면 구성이 없습니다.", true); continue; }
                try { composition.Validate(); }
                catch (Exception error) { Add(issues, path, "", error.Message, true); }
                string hostPath = $"Assets/_Project/Prefabs/UI/Hosts/{scene}UiHost.prefab";
                var host = AssetDatabase.LoadAssetAtPath<GameObject>(hostPath);
                if (host == null) { Add(issues, hostPath, "", "UI 호스트가 없습니다.", true); continue; }
                if (host.GetComponent<UiHost>()?.Composition != composition)
                {
                    Add(issues, hostPath, "", "호스트가 해당 씬의 화면 구성을 참조하지 않습니다.", true);
                }

                foreach (var entry in composition.Entries)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.ParentPath) && host.transform.Find(entry.ParentPath) == null)
                    {
                        Add(issues, hostPath, entry.ParentPath, entry.Id + ": 배치 영역이 없습니다.", true);
                    }
                }
            }
            return issues;
        }

        internal static void InspectPrefab(GameObject root, string path, List<Issue> issues)
        {
            var references = new HashSet<Object>();
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null)
                {
                    continue;
                }

                var property = new SerializedObject(component).GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference && !IsInternal(property.propertyPath)
                        && property.objectReferenceValue != null && property.objectReferenceValue != component
                        && property.objectReferenceValue != component.gameObject && property.objectReferenceValue != component.transform)
                    {
                        references.Add(property.objectReferenceValue);
                    }
                }
            }
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController != null)
                {
                    foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    {
                        foreach (var binding in AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)))
                        {
                            var target = animator.transform.Find(binding.path);
                            if (target != null)
                            {
                                references.Add(target.gameObject); foreach (var component in target.GetComponents<Component>())
                                {
                                    references.Add(component);
                                }
                            }
                        }
                    }
                }
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var location = AnimationUtility.CalculateTransformPath(t, root.transform);
                var names = new HashSet<string>();
                foreach (Transform child in t)
                {
                    if (!names.Add(child.name))
                    {
                        Add(issues, path, location, "같은 부모에 이름이 중복됩니다: " + child.name, false);
                    }
                }

                var image = t.GetComponent<Image>();
                if (image != null && image.raycastTarget && t.name.StartsWith("Art_", StringComparison.Ordinal))
                {
                    Add(issues, path, location, "장식 이미지가 터치를 차단합니다. Raycast Target을 확인하세요.", false);
                }

                if (t.GetComponent<TMP_Text>() != null && t.childCount == 0 && t.parent != null
                    && !references.Contains(t.gameObject) && !t.GetComponents<Component>().Any(references.Contains))
                {
                    bool portrait = t.name == "Placeholder" && t.parent.GetComponent<Image>()?.sprite != null;
                    bool replaced = !t.gameObject.activeSelf && Enumerable.Range(0, t.parent.childCount).Select(t.parent.GetChild)
                        .Any(s => s != t && s.gameObject.activeSelf && s.name.StartsWith("Art_", StringComparison.Ordinal));
                    if (portrait || replaced)
                    {
                        Add(issues, path, location, "이미지로 대체된 문구인지 확인하세요. 코드·이벤트·애니메이션 참조가 없습니다.", false);
                    }
                }
                foreach (var component in t.GetComponents<Component>())
                {
                    if (component == null) { Add(issues, path, location, "Missing Script", true); continue; }
                    var property = new SerializedObject(component).GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference || IsInternal(property.propertyPath))
                        {
                            continue;
                        }

                        var value = property.objectReferenceValue;
                        if (value == null)
                        {
                            if (property.objectReferenceEntityIdValue != default)
                            {
                                Add(issues, path, location, "Missing Reference: " + property.propertyPath, true);
                            }

                            continue;
                        }
                        var target = value is Component c ? c.transform : value is GameObject go ? go.transform : null;
                        if (target != null && !target.IsChildOf(root.transform) && !EditorUtility.IsPersistent(value))
                        {
                            Add(issues, path, location, "프리팹 외부 객체 참조: " + property.propertyPath, true);
                        }
                    }
                }
            }
        }

        private static bool IsInternal(string path) => path is "m_GameObject" or "m_Script" or "m_CorrespondingSourceObject" or "m_PrefabInstance" or "m_PrefabAsset";
        private static void Add(List<Issue> issues, string asset, string location, string message, bool error) =>
            issues.Add(new Issue { AssetPath = asset, ObjectPath = location, Message = message, IsError = error });

        internal static void Open(Issue issue)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(issue.AssetPath);
            if (asset == null)
            {
                return;
            }

            AssetDatabase.OpenAsset(asset);
            if (!(asset is GameObject)) { Selection.activeObject = asset; return; }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.assetPath != issue.AssetPath)
            {
                return;
            }

            var target = string.IsNullOrEmpty(issue.ObjectPath) ? stage.prefabContentsRoot.transform
                : stage.prefabContentsRoot.transform.Find(issue.ObjectPath);
            if (target != null) { Selection.activeGameObject = target.gameObject; EditorGUIUtility.PingObject(target.gameObject); }
        }
    }
}
