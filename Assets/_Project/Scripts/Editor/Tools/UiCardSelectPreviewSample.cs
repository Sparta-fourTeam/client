using System.Linq;
using Game.Core;
using Game.View;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    internal static class UiCardSelectPreviewSample
    {
        internal static void Apply(GameObject root)
        {
            var view = root.GetComponent<CardSelectView>();
            var standalone = root.GetComponent<CardSlotView>();
            if (view == null && standalone == null) { return; }
            if (view == null)
            {
                // In a screen the parent's layout supplies these bounds; standalone review uses the same card dimensions.
                var rect = (RectTransform)standalone.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(328f, 600f);
                view = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Stage/CardSelect.prefab")
                    .GetComponent<CardSelectView>();
            }
            var serialized = new SerializedObject(view);
            var assets = (SkillAssetTable)serialized.FindProperty("_assets").objectReferenceValue;
            var slots = serialized.FindProperty("_slots");
            var catalog = new DefaultSkillDataProvider(new GameDataStore()).LoadAll();
            var frost = catalog.Single(s => s.id == 4);
            var arrow = catalog.Single(s => s.id == 1);
            var lightning = catalog.Single(s => s.id == 3);
            var option = arrow.upgrades.Single(o => o.id == "arrow_multishot");
            SkillUpgradeResolver.TryResolve(option, 0, out var upgrade);
            using var owned = SkillFactory.Create(arrow, assets.GetPrefab(arrow.id), null, null);
            var choices = new[]
            {
                new UpgradeChoice { IsNewWeapon = true, newSkillData = frost },
                new UpgradeChoice { skill = owned, Option = option, DisplayName = upgrade.Name, DisplayDescription = upgrade.Description },
                new UpgradeChoice { IsNewWeapon = true, newSkillData = lightning }
            };
            int count = standalone != null ? 1 : slots.arraySize;
            for (int i = 0; i < count; i++)
            {
                var slot = standalone != null ? standalone : slots.GetArrayElementAtIndex(i).objectReferenceValue as CardSlotView;
                if (slot == null) { continue; }
                slot.gameObject.SetActive(i < choices.Length);
                if (i >= choices.Length) { continue; }
                var choice = choices[i];
                var data = choice.IsNewWeapon ? choice.newSkillData : choice.skill.Data;
                var kind = choice.IsNewWeapon ? SkillCardIcon.New : SkillCardIcon.Upgrade;
                var background = (Sprite)serialized.FindProperty(choice.IsNewWeapon ? "_newWeaponBg" : "_upgradeBg").objectReferenceValue;
                slot.Bind(i, choice, assets.GetCardIcon(data.id, kind), background, null,
                    hint => assets.GetFormIcon(hint.Form, hint.Skill.id));
            }
        }
    }
}
