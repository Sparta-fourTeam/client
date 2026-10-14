using System.Linq;
using Game.Core;
using Game.View;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    internal static class UiCardSelectPreviewSample
    {
        internal static void Apply(GameObject root, string sample = "기본")
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
            var option = arrow.upgrades.Single(o => o.id == "1"); // 다중 사격(옛 arrow_multishot)
            SkillUpgradeResolver.TryResolve(option, 0, out var upgrade);
            using var owned = SkillFactory.Create(arrow, assets.GetPrefab(arrow.id), null, null);
            var choices = new[]
            {
                new UpgradeChoice { IsNewWeapon = true, newSkillData = frost },
                new UpgradeChoice { skill = owned, Option = option, DisplayName = upgrade.Name, DisplayDescription = upgrade.Description },
                new UpgradeChoice { IsNewWeapon = true, newSkillData = lightning }
            };
            if (sample == "형태변환")
            {
                var transform = arrow.upgrades.Single(o => o.id == "16"); // 번개 화살 형태 변환(옛 arrow_thunder)
                SkillUpgradeResolver.TryResolve(transform, 0, out var resolved);
                FormHintFinder.TryGetForm(transform, 0, out var form);
                choices[standalone != null ? 0 : 2] = new UpgradeChoice
                {
                    skill = owned,
                    Option = transform,
                    TargetForm = form,
                    DisplayName = resolved.Name,
                    DisplayDescription = resolved.Description
                };
            }
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
                var icon = choice.TargetForm != SkillForm.Default
                    ? assets.GetFormIcon(choice.TargetForm, data.id) : assets.GetCardIcon(data.id, kind);
                slot.Bind(i, choice, icon, background, null,
                    hint => assets.GetFormIcon(hint.Form, hint.Skill.id));
            }
        }
    }
}
