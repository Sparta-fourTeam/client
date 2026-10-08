using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>최종 PNG의 실제 Sprite 임포트와 표시 테이블/프리팹 연결을 검사한다.</summary>
    [InitializeOnLoad]
    public static class GeneratedAssetValidator
    {
        private const string Root = "Assets/_Project/Sprites/Generated";
        private const string Marker = "docs/asset-previews/.run-unity-audit";

        [Serializable]
        private sealed class Report
        {
            public string utc;
            public int sprites;
            public int textures;
            public int sheetTextures;
            public int layerSprites;
            public string[] errors;
        }

        static GeneratedAssetValidator()
        {
            EditorApplication.delayCall += RunRequested;
        }

        private static void RunRequested()
        {
            if (!File.Exists(Marker)) { return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunRequested;
                return;
            }
            Validate();
            File.Delete(Marker);
        }

        [MenuItem("Tools/Assets/Validate Generated Artwork")]
        public static void Validate()
        {
            var errors = new List<string>();
            int count = 0;
            int textures = 0;
            int sheetTextures = 0;
            int layerSprites = 0;
            foreach (string path in Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories))
            {
                string assetPath = path.Replace('\\', '/');
                if (assetPath.Contains("/LayeredSheets/"))
                {
                    sheetTextures++;
                    var parts = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();
                    layerSprites += parts.Length;
                    if (parts.Length == 0 || parts.Any(part => part.texture == null))
                    { errors.Add("Missing sheet layers: " + assetPath); }
                    continue;
                }
                if (assetPath.Contains("/EffectTextures/"))
                {
                    if (AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath) == null)
                    { errors.Add("Missing particle texture: " + assetPath); }
                    else { textures++; }
                    continue;
                }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null) { errors.Add("Not imported as Sprite: " + assetPath); continue; }
                count++;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long fileId);
                if (fileId != 21300000) { errors.Add("Unexpected single sprite fileID: " + assetPath + " " + fileId); }
            }

            CheckTable("ItemIconTable", "_items", "icon", 24, errors);
            CheckTable("MonsterDisplayTable", "_monsters", "icon", 14, errors);
            var items = Table("ItemIconTable");
            foreach (string key in new[] { "_coin", "_exp", "_randomSkillMaterial", "_randomEquipmentMaterial" })
            {
                RequireReference(items.FindProperty(key), "ItemIconTable." + key, errors);
            }
            var skills = Table("SkillAssetTable").FindProperty("entries");
            for (int i = 0; i < skills.arraySize; i++)
            {
                var entry = skills.GetArrayElementAtIndex(i);
                string key = entry.FindPropertyRelative("key").stringValue;
                if (key == "wall_repair")
                {
                    RequireReference(entry.FindPropertyRelative("upgradeCardIcon"), key, errors);
                    continue;
                }
                if (new[] { "weapon_ice_shard", "weapon_frost_crystal_aux", "weapon_arrow_shard",
                    "weapon_fireball_shard", "weapon_lightning_orb", "weapon_energy_beam_aux" }.Contains(key)) { continue; }
                foreach (string field in new[] { "hudIcon", "newCardIcon", "upgradeCardIcon" })
                {
                    RequireReference(entry.FindPropertyRelative(field), key + "." + field, errors);
                }
            }

            var screen = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/CharacterScreen.prefab");
            var screenView = screen.GetComponents<MonoBehaviour>().First(c => c != null && c.GetType().Name == "CharacterScreenView");
            var slots = new SerializedObject(screenView).FindProperty("_equipSlots");
            string[] expected = { "Hood", "Robe", "Boots", "Staff", "BoneRing", "SkullNecklace", "Seal" };
            if (slots.arraySize != expected.Length) { errors.Add("Equipment slot count"); }
            for (int i = 0; i < Math.Min(slots.arraySize, expected.Length); i++)
            {
                var slot = (Component)slots.GetArrayElementAtIndex(i).objectReferenceValue;
                var icon = slot.transform.Find("Icon").GetComponent<Image>();
                if (icon.sprite == null || AssetDatabase.GetAssetPath(icon.sprite) != Root + "/Equipment/" + expected[i] + ".png")
                { errors.Add("Equipment slot icon: " + expected[i]); }
            }
            var popup = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/EquipUpgradePopup.prefab");
            var popupView = popup.GetComponents<MonoBehaviour>().First(c => c != null && c.GetType().Name == "EquipUpgradePopupView");
            var popupData = new SerializedObject(popupView);
            foreach (string key in new[] { "_icons", "_equipmentIcon", "_materialIcon" })
            { RequireReference(popupData.FindProperty(key), "EquipUpgradePopup." + key, errors); }

            foreach (string path in new[] { "Skills/FrostPrison", "Skills/LightningCloud", "Skills/SunBeam", "Stage/Slime_Projectile" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/" + path + ".prefab");
                var renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer.sprite == null || !AssetDatabase.GetAssetPath(renderer.sprite).StartsWith(Root + "/Effects/"))
                { errors.Add("Effect sprite: " + path); }
            }

            var report = new Report { utc = DateTime.UtcNow.ToString("O"), sprites = count, textures = textures, sheetTextures = sheetTextures, layerSprites = layerSprites, errors = errors.ToArray() };
            File.WriteAllText("docs/asset-previews/unity-validation.json", JsonUtility.ToJson(report, true));
            if (errors.Count == 0) { Debug.Log("[GeneratedAssetValidator] Passed: " + count + " sprites"); }
            else { Debug.LogError("[GeneratedAssetValidator] " + string.Join("\n", errors)); }
        }

        private static SerializedObject Table(string name) =>
            new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Project/Data/" + name + ".asset"));

        private static void CheckTable(string name, string list, string icon, int expected, List<string> errors)
        {
            var array = Table(name).FindProperty(list);
            if (array.arraySize != expected) { errors.Add(name + " count: " + array.arraySize); }
            for (int i = 0; i < array.arraySize; i++)
            { RequireReference(array.GetArrayElementAtIndex(i).FindPropertyRelative(icon), name + "[" + i + "]", errors); }
        }

        private static void RequireReference(SerializedProperty property, string context, List<string> errors)
        {
            if (property == null || property.objectReferenceValue == null) { errors.Add("Missing reference: " + context); }
        }
    }
}
