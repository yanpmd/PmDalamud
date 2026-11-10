using System.Numerics;
using KamiToolKit.Nodes;
using AetherBags.Compat;

namespace AetherBags.Nodes.Configuration.Currency;

public sealed class CurrencyScrollingAreaNode : ScrollingListNode
{
    public CurrencyScrollingAreaNode()
    {
        AddNode(new CurrencyGeneralConfigurationNode
        {
            Width = 600,
            OnLayoutChanged = RecalculateLayout
        });
    }
}