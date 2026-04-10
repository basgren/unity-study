using System;
using System.Collections.Generic;

namespace Game.Core.Models.Inventory {
    public delegate void SelectionChangedHandler(InventoryItem currentItem, InventoryItem prevItem);

    /// <summary>
    /// Common interface for panel models that track a list of inventory items
    /// with a single selected entry (backpack items, perks, etc.).
    /// </summary>
    public interface ISelectablePanelModel {
        event Action<IReadOnlyList<InventoryItem>> ItemsUpdated;
        event SelectionChangedHandler SelectionUpdated;

        IReadOnlyList<InventoryItem> Items { get; }
        InventoryItem SelectedItem { get; }
        int SelectedItemUid { get; }
        bool IsAnyItemSelected { get; }

        void NextItem();
        void PrevItem();
    }
}
