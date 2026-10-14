using Game.Core;
using Game.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>Uses table content on an isolated review clone; the action samples never call an API or write a save.</summary>
    internal static class UiDetailPreviewSample
    {
        internal static void Apply(GameObject root, string sample)
        {
            var view = root.GetComponent<DetailPopupView>();
            if (view == null) { return; }
            var data = new GameDataStore();
            var icons = AssetDatabase.LoadAssetAtPath<ItemIconTable>("Assets/_Project/Data/ItemIconTable.asset");
            if (sample == "도감 보상 수령")
            {
                var display = AssetDatabase.LoadAssetAtPath<MonsterDisplayTable>("Assets/_Project/Data/MonsterDisplayTable.asset");
                if (!display.TryGetMonster(1, out var entry)) { return; }
                var monster = data.Monsters.GetOrThrow(entry.monsterId);
                view.Show(entry.icon, entry.displayName, MonsterTraits.Subtitle(monster),
                    MonsterTraits.Description(monster, id => display.TryGetMonster(id, out var child) ? child.displayName : $"#{id}"));
                var so = new SerializedObject(view);
                ((Button)so.FindProperty("_claimButton").objectReferenceValue).gameObject.SetActive(true);
                ((GameObject)so.FindProperty("_claimBadge").objectReferenceValue)?.SetActive(true);
            }
            else if (sample == "확률 정보")
            {
                view.Show(icons.GetRandom(ItemIds.RandomSkillMaterial), ItemInfo.NameOf(ItemIds.RandomSkillMaterial, data),
                    "랜덤 스킬 재료", ItemInfo.DescriptionOf(ItemIds.RandomSkillMaterial));
                var so = new SerializedObject(view);
                ((Button)so.FindProperty("_oddsButton").objectReferenceValue).gameObject.SetActive(true);
            }
            else
            {
                view.Show(icons.Get(ItemIds.ArrowBook), ItemInfo.NameOf(ItemIds.ArrowBook, data),
                    $"보유 {CurrencyFormat.Format(32)}개", ItemInfo.DescriptionOf(ItemIds.ArrowBook), () => { });
            }
        }
    }
}
