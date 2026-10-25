using System.Collections.Generic;
using AetherBags.Inventory.Items;

namespace AetherBags.Inventory.Categories;

public readonly record struct CategorizedInventory(uint Key, CategoryInfo Category, List<ItemInfo> Items);