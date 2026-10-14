using Game.Core;
using Game.View;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Displays catalog recovery amounts on the isolated Workspace clone without calling recovery services.</summary>
    internal static class UiEnergyPreviewSample
    {
        internal static void Apply(GameObject root)
        {
            var view = root.GetComponent<EnergyRecoverPopupView>();
            if (view == null) { return; }
            var config = new GameDataStore().Energy;
            var serialized = new SerializedObject(view);
            if (serialized.FindProperty("_adTitle")?.objectReferenceValue is TMP_Text ad)
            { ad.text = $"에너지 x{config.AdRecoverAmount}"; }
            if (serialized.FindProperty("_purchaseTitle")?.objectReferenceValue is TMP_Text purchase)
            { purchase.text = $"에너지 x{config.PurchaseRecoverAmount}"; }
        }
    }
}
