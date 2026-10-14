using System.Linq;
using Game.Core;
using Game.View;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    internal static class UiEliteRoulettePreviewSample
    {
        internal static void Apply(GameObject root, string sample)
        {
            if (root.GetComponent<EliteRouletteView>() is not { } view) { return; }
            var serialized = new SerializedObject(view);
            var assets = (SkillAssetTable)serialized.FindProperty("_assets").objectReferenceValue;
            var background = (Sprite)serialized.FindProperty("_cardBackground").objectReferenceValue;
            bool longDescriptions = sample == "긴 설명 4장" || sample == "긴 설명 5장";
            var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Single(s => s.id == (longDescriptions ? 7 : 1));
            var options = data.upgrades.Where(o => string.IsNullOrEmpty(o.sharedId) && (!longDescriptions || o.enabled))
                .OrderByDescending(o => longDescriptions ? (o.desc?.Length ?? 0) : 0).ToArray();
            int visibleCount = sample == "보상 5장" || sample == "긴 설명 5장" ? 5 : longDescriptions ? 4 : 2;
            using var owned = SkillFactory.Create(data, assets.GetPrefab(data.id), null, null);
            var cards = serialized.FindProperty("_rewardCards");
            for (int i = 0; i < cards.arraySize; i++)
            {
                if (cards.GetArrayElementAtIndex(i).objectReferenceValue is not CardSlotView card) { continue; }
                var option = options[i % options.Length];
                if (!SkillUpgradeResolver.TryResolve(option, 0, out var upgrade)) { continue; }
                var choice = new UpgradeChoice
                {
                    skill = owned,
                    Option = option,
                    DisplayName = upgrade.Name,
                    DisplayDescription = upgrade.Description
                };
                card.Bind(i, choice, assets.GetCardIcon(data.id, SkillCardIcon.Upgrade), background, null,
                    hint => assets.GetFormIcon(hint.Form, hint.Skill.id));
                card.gameObject.SetActive(i < visibleCount);
                card.GetComponentInChildren<UnityEngine.UI.Button>(true).interactable = false;
                if (i == 0) { owned.LevelUp(option); }
            }
            var icons = serialized.FindProperty("_weaponIcons");
            for (int i = 0; i < icons.arraySize; i++)
            {
                if (icons.GetArrayElementAtIndex(i).objectReferenceValue is not UnityEngine.UI.Image icon) { continue; }
                icon.sprite = assets.GetHudIcon(data.id);
                icon.enabled = icon.sprite != null;
                icon.color = Color.white;
            }
            ((RectTransform)serialized.FindProperty("_spinner").objectReferenceValue).localRotation = Quaternion.Euler(0f, 0f, 22.5f);
            if (serialized.FindProperty("_heading").objectReferenceValue is TMP_Text heading)
            {
                heading.text = "엘리트 보상  3 / 5";
            }
        }
    }
}
