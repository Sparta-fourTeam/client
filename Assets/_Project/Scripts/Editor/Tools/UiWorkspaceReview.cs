using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.View;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>Exports the same isolated composition used by UI Workspace, without entering Play mode.</summary>
    public static class UiWorkspaceReview
    {
        [Serializable]
        public sealed class Entry
        {
            public string group, id, prefab, reference;
            public bool approved, standalone;
        }

        [Serializable]
        public sealed class GraphicInfo
        {
            public string path, type, text, color, sprite, spriteName;
            public bool active, raycastTarget, textWithinRect;
            public Rect screenRect;
            public Vector4 margin, border;
            public float fontSize, pixelsPerUnitMultiplier;
        }

        [Serializable]
        public sealed class ButtonInfo
        {
            public string path, transition, normal, highlighted, pressed, selected;
            public bool interactable;
        }

        [Serializable]
        public sealed class LayoutInfo
        {
            public string path, component;
        }

        [Serializable]
        public sealed class Report
        {
            public Entry entry;
            public string image, referenceImage, error;
            public double motionSeconds;
            public List<GraphicInfo> graphics = new();
            public List<ButtonInfo> buttons = new();
            public List<LayoutInfo> layouts = new();
            public List<string> issues = new();
        }

        [Serializable]
        private sealed class CatalogData
        {
            public List<Entry> entries;
        }

        private static readonly Dictionary<string, string> References = new()
        {
            ["LoadingScreen"] = "final2/rr_title_tap.png",
            ["TapToStart"] = "final2/rr_title_tap.png",
            ["LoginView"] = "misc/ok_login.png",
            ["LobbyBackground"] = "final2/rr_lobby_home.png",
            ["LobbyTopBar"] = "final2/rr_lobby_home.png",
            ["LobbyMissionButton"] = "final2/rr_lobby_home.png",
            ["LobbyBottomNav"] = "final2/rr_lobby_home.png",
            ["LobbySideMenu"] = "final2/rr_lobby_home.png",
            ["CharacterScreen"] = "final2/rr_character.png",
            ["InventoryScreen"] = "final2/rr_inventory.png",
            ["SkillScreen"] = "final2/rr_skill.png",
            ["ShopScreen"] = "final2/rr_shop.png",
            ["MissionScreen"] = "final2/rr_mission.png",
            ["ProfilePopup"] = "final/fin_profile_popup.png",
            ["EnergyRecoverPopup"] = "final/fin_energy_popup.png",
            ["FriendPopup"] = "final/fin_friend_popup.png",
            ["MailPopup"] = "final/fin_mail_popup.png",
            ["QuestPopup"] = "final/fin_quest_popup.png",
            ["LobbyMenuPopup"] = "final/fin_menu_popup.png",
            ["SettingsPopup"] = "final2/rr_settings_popup.png",
            ["LanguagePopup"] = "final/fin_language_popup.png",
            ["DialogueScene"] = "final2/rr_dialogue.png",
            ["TutorialGuide"] = "final2/rr_tutorial_guide.png",
            ["LevelUpPopup"] = "final2/rr_level_up.png",
            ["LevelUpBurst"] = "final2/rr_level_up.png",
            ["RewardPopup"] = "final2/rr_reward_popup.png",
            ["MissionRewardInfoPopup"] = "final2/rr_reward_popup.png",
            ["DetailPopup"] = "final/fin_item_detail.png",
            ["MissionDetailsPopup"] = "final2/rr_mission.png",
            ["RewardOddsPopup"] = "final/fin_item_detail.png",
            ["SkillUpgradePopup"] = "final/fin_detail_c.png",
            ["EquipUpgradePopup"] = "final/fin_detail_b.png",
            ["UpgradeConfirmPopup"] = "final/fin_detail_a.png",
            ["ConfirmPopup"] = "final/fin_skip_confirm.png",
            ["HudCanvas"] = "final/fin_battle_hud.png",
            ["SkillHud"] = "final/fin_battle_hud.png",
            ["StageControls"] = "final/fin_battle_hud.png",
            ["CardSelect"] = "final2/cmp_v31_card.png",
            ["ResultPopup"] = "final2/rr_clear_result.png",
            ["PauseMenu"] = "final/fin_pause_popup.png",
            ["EliteRoulette"] = "final/fin_elite_roulette.png",
            ["SubmittingOverlay"] = "final/fin_submitting.png",
            ["SubmitRejectedPopup"] = "final/fin_submit_fail.png"
        };

        private static readonly HashSet<string> Approved = new()
        {
            "LobbyBackground", "LobbyTopBar", "LobbyMissionButton", "LobbyBottomNav", "LobbySideMenu", "LobbyEventBanner",
            "HudCanvas", "SkillHud", "StageControls", "SkillSlot", "SkillRangeIndicator", "HpSoulFx", "StarSparksFx", "ConfirmPopup"
        };

        public static string ReferenceRoot => EditorPrefs.GetString("ProjectNova.UiReferenceRoot",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "UiConcept", "restored"));

        public static List<Entry> Catalog()
        {
            var entries = new List<Entry>();
            foreach (string group in new[] { "Loading", "Lobby", "Stage", "Common" })
            {
                var composition = group == "Common" ? null : AssetDatabase.LoadAssetAtPath<UiComposition>(
                    $"Assets/_Project/Settings/UI/{group}Ui.asset");
                if (composition != null)
                {
                    entries.AddRange(composition.Entries.Where(e => e?.Prefab != null)
                        .Select(e => MakeEntry(group, e.Id, AssetDatabase.GetAssetPath(e.Prefab), false)));
                }
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { $"Assets/_Project/Prefabs/UI/{group}" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (entries.Any(e => e.group == group && e.prefab == path)) { continue; }
                    entries.Add(MakeEntry(group, Path.GetFileNameWithoutExtension(path), path, true));
                }
            }
            // Shared sources used by approved screens are also protected.
            var protectedDependencies = new HashSet<string>(AssetDatabase.GetDependencies(
                entries.Where(e => e.approved).Select(e => e.prefab).ToArray(), true));
            foreach (var entry in entries)
            {
                if (protectedDependencies.Contains(entry.prefab)) { entry.approved = true; }
            }
            return entries.OrderBy(e => e.group).ThenBy(e => e.id).ToList();
        }

        private static Entry MakeEntry(string group, string id, string prefab, bool standalone) => new()
        {
            group = group,
            id = id,
            prefab = prefab,
            standalone = standalone,
            approved = group == "Loading" || Approved.Contains(id),
            reference = References.TryGetValue(id, out string reference) ? reference : ""
        };

        public static string ExportCatalog(string directory)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "catalog.json");
            File.WriteAllText(path, JsonUtility.ToJson(new CatalogData { entries = Catalog() }, true));
            return path;
        }

        public static string Export(string group, string id, string directory, double motionSeconds = 0,
            int deviceIndex = 0, string sample = "기본")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("편집 미리보기 검토는 재생 모드 밖에서 실행하세요.");
            }
            var entry = Catalog().Single(e => e.group == group && e.id == id);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefab);
            var composition = entry.standalone || group == "Common" ? null : AssetDatabase.LoadAssetAtPath<UiComposition>(
                $"Assets/_Project/Settings/UI/{group}Ui.asset");
            var device = UiPreviewRenderer.Devices[Mathf.Clamp(deviceIndex, 0, UiPreviewRenderer.Devices.Length - 1)];
            using var session = UiPreviewRenderer.CreateSession(prefab, composition, device, true, sample, entry.id);
            if (motionSeconds > 0) { session.Advance((float)motionSeconds); }
            Directory.CreateDirectory(directory);
            string stem = Path.Combine(directory, group + "_" + id);
            var report = new Report
            {
                entry = entry,
                image = stem + ".png",
                motionSeconds = motionSeconds,
                referenceImage = string.IsNullOrEmpty(entry.reference) ? "" : Path.Combine(ReferenceRoot, entry.reference)
            };
            Texture2D shot = session.Capture();
            try { File.WriteAllBytes(report.image, shot.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(shot); }
            Collect(session.ReviewRoot, session.ReviewCamera, device, report);
            string json = stem + ".json";
            File.WriteAllText(json, JsonUtility.ToJson(report, true));
            return json;
        }

        private static void Collect(GameObject root, Camera camera, UiPreviewRenderer.Device device, Report report)
        {
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                var record = new GraphicInfo
                {
                    path = AnimationUtility.CalculateTransformPath(graphic.transform, root.transform),
                    type = graphic.GetType().Name,
                    active = graphic.isActiveAndEnabled,
                    color = ColorUtility.ToHtmlStringRGBA(graphic.color),
                    raycastTarget = graphic.raycastTarget,
                    screenRect = ScreenRect(graphic.rectTransform, camera, device),
                    textWithinRect = true
                };
                if (graphic is UnityEngine.UI.Image image)
                {
                    record.sprite = image.sprite == null ? "" : AssetDatabase.GetAssetPath(image.sprite);
                    record.spriteName = image.sprite == null ? "" : image.sprite.name;
                    record.border = image.sprite == null ? Vector4.zero : image.sprite.border;
                    record.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
                }
                if (graphic is TMP_Text text)
                {
                    record.text = text.text; record.fontSize = text.fontSize; record.margin = text.margin;
                    text.ForceMeshUpdate(true);
                    foreach (var mesh in text.textInfo.meshInfo)
                    {
                        for (int i = 0; i < mesh.vertexCount; i++)
                        {
                            var point = mesh.vertices[i];
                            var rect = text.rectTransform.rect;
                            if (point.x < rect.xMin - .5f || point.x > rect.xMax + .5f
                                || point.y < rect.yMin - .5f || point.y > rect.yMax + .5f)
                            {
                                record.textWithinRect = false;
                            }
                        }
                    }
                    if (record.active && !record.textWithinRect) { report.issues.Add(record.path + ": 글자 메시가 영역 밖입니다."); }
                }
                report.graphics.Add(record);
            }
            foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                var colors = button.colors;
                report.buttons.Add(new ButtonInfo
                {
                    path = AnimationUtility.CalculateTransformPath(button.transform, root.transform),
                    interactable = button.interactable,
                    transition = button.transition.ToString(),
                    normal = ColorUtility.ToHtmlStringRGBA(colors.normalColor),
                    highlighted = ColorUtility.ToHtmlStringRGBA(colors.highlightedColor),
                    pressed = ColorUtility.ToHtmlStringRGBA(colors.pressedColor),
                    selected = ColorUtility.ToHtmlStringRGBA(colors.selectedColor)
                });
            }
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component is LayoutGroup or ContentSizeFitter or LayoutElement or AspectRatioFitter)
                {
                    report.layouts.Add(new LayoutInfo
                    {
                        path = AnimationUtility.CalculateTransformPath(component.transform, root.transform),
                        component = component.GetType().Name
                    });
                }
            }
            var issues = new List<UiAssetAudit.Issue>();
            UiAssetAudit.InspectPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(report.entry.prefab), report.entry.prefab, issues);
            report.issues.AddRange(issues.Select(i => i.ObjectPath + ": " + i.Message));
        }

        private static Rect ScreenRect(RectTransform rect, Camera camera, UiPreviewRenderer.Device device)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var points = corners.Select(camera.WorldToViewportPoint).Select(p => new Vector2(
                (camera.rect.x + p.x * camera.rect.width) * device.Width,
                (1f - camera.rect.y - p.y * camera.rect.height) * device.Height)).ToArray();
            float left = points.Min(p => p.x), top = points.Min(p => p.y);
            return new Rect(left, top, points.Max(p => p.x) - left, points.Max(p => p.y) - top);
        }
    }
}
