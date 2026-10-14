using System.Collections.Generic;
using Game.Core;
using Game.View;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Populates only the isolated workspace preview clone through the public inventory API.</summary>
    internal static class UiInventoryPreviewSample
    {
        internal static void Apply(GameObject root)
        {
            var inventory = root.GetComponent<InventoryScreenView>();
            if (inventory == null)
            {
                return;
            }

            var data = new GameDataStore();
            var profile = new PlayerProfile(data);
            profile.Apply(new PlayerSnapshot
            {
                items = new List<ItemAmount>
                {
                    new() { itemId = ItemIds.ArrowBook, quantity = 32 },
                    new() { itemId = ItemIds.LightningBook, quantity = 2 },
                    new() { itemId = ItemIds.LogBook, quantity = 3 },
                    new() { itemId = ItemIds.HatBook, quantity = 1 },
                    new() { itemId = ItemIds.GemChest, quantity = 1 },
                },
            });
            inventory.Initialize();
            inventory.Construct(profile, data);
            inventory.SetVisible(true);
        }
    }
}
