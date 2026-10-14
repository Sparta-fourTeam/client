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
            if (root.GetComponent<CardSelectView>() is not { } view) { return; }
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
            for (int i = 0; i < slots.arraySize; i++)
            {
                if (slots.GetArrayElementAtIndex(i).objectReferenceValue is not CardSlotView slot) { continue; }
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
