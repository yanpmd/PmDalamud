using System;
using System.Linq;
using System.Numerics;
using AetherBags.Addons;
using AetherBags.Configuration;
using AetherBags.Nodes.Color;
using AetherBags.Nodes.Configuration.Category;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;
using Lumina.Excel.Sheets;

namespace AetherBags.Nodes.Configuration.Currency;

public sealed class CurrencyGeneralConfigurationNode : TabbedVerticalListNode
{
    private readonly UintListEditorNode? _currencyListEditor;
    public global::System.Action? OnLayoutChanged { get; set; }

    public CurrencyGeneralConfigurationNode()
    {
        CurrencySettings config = System.Config.Currency;

        Width = 600;
        ItemVerticalSpacing = 2;

        LabelTextNode titleNode = new LabelTextNode
        {
            Size = new Vector2(Width, 18),
            String = Localization.Get("Currency.Title"),
            TextColor = ColorHelper.GetColor(2),
            TextOutlineColor = ColorHelper.GetColor(0),
        };
        AddNode(titleNode);

        AddTab(1);

        CheckboxNode currencyEnabledCheckbox = new CheckboxNode
        {
            Size = new Vector2(Width, 18),
            IsVisible = true,
            String = Localization.Get("Currency.ShowCurrency"),
            IsChecked = config.Enabled,
            OnClick = isChecked =>
            {
                config.Enabled = isChecked;
                RefreshCurrency();
            }
        };
        AddNode(currencyEnabledCheckbox);

        AddTab(1);

        var defaultColorHandler = CreateColorHandler(color => config.DefaultColor = color);
        ColorInputRow defaultCurrencyColorNode = new ColorInputRow
        {
            Label = Localization.Get("Currency.DefaultColor"),
            Size = new Vector2(300, 24),
            CurrentColor = config.DefaultColor,
            DefaultColor = new CurrencySettings().DefaultColor,
            OnColorConfirmed = defaultColorHandler,
            OnColorChange = defaultColorHandler,
            OnColorCanceled = defaultColorHandler,
            OnColorPreviewed = defaultColorHandler,
        };
        AddNode(defaultCurrencyColorNode);

        CheckboxNode cappedEnabledCheckbox = new CheckboxNode
        {
            Size = new Vector2(Width, 18),
            IsVisible = true,
            String = Localization.Get("Currency.ColorWeeklyCap"),
            IsChecked = config.ColorWhenCapped,
            TextTooltip = Localization.Get("Currency.ColorWeeklyCapTooltip"),
            OnClick = isChecked =>
            {
                config.ColorWhenCapped = isChecked;
                RefreshCurrency();
            }
        };
        AddNode(cappedEnabledCheckbox);

        AddTab(1);

        var cappedColorHandler = CreateColorHandler(color => config.CappedColor = color);
        ColorInputRow cappedCurrencyColorNode = new ColorInputRow
        {
            Label = Localization.Get("Currency.CappedColor"),
            Size = new Vector2(300, 24),
            CurrentColor = config.CappedColor,
            DefaultColor = new CurrencySettings().CappedColor,
            OnColorConfirmed = cappedColorHandler,
            OnColorChange = cappedColorHandler,
            OnColorCanceled = cappedColorHandler,
            OnColorPreviewed = cappedColorHandler,
        };
        AddNode(cappedCurrencyColorNode);

        SubtractTab(1);

        CheckboxNode limitedEnabledCheckbox = new CheckboxNode
        {
            Size = new Vector2(Width, 18),
            IsVisible = true,
            String = Localization.Get("Currency.ColorMaxCapacity"),
            IsChecked = config.ColorWhenLimited,
            TextTooltip = Localization.Get("Currency.ColorMaxCapacityTooltip"),
            OnClick = isChecked =>
            {
                config.ColorWhenLimited = isChecked;
                RefreshCurrency();
            }
        };
        AddNode(limitedEnabledCheckbox);

        AddTab(1);

        var limitColorHandler = CreateColorHandler(color => config.LimitColor = color);
        ColorInputRow limitCurrencyColorNode = new ColorInputRow
        {
            Label = Localization.Get("Currency.LimitColor"),
            Size = new Vector2(300, 24),
            CurrentColor = config.LimitColor,
            DefaultColor = new CurrencySettings().LimitColor,
            OnColorConfirmed = limitColorHandler,
            OnColorChange = limitColorHandler,
            OnColorCanceled = limitColorHandler,
            OnColorPreviewed = limitColorHandler,
        };
        AddNode(limitCurrencyColorNode);

        AddNode(new ResNode { Size = new Vector2(15) });

        SubtractTab(1);

        AddNode(new ResNode { Size = new Vector2(15) });

        _currencyListEditor = new UintListEditorNode
        {
            Label = Localization.Get("Currency.DisplayedCurrencies"),
            LabelWidth = 360f,
            MaxValue = Services.DataManager.GetExcelSheet<Item>()?.LastOrDefault().RowId ?? uint.MaxValue,
            LabelResolver = id =>
            {
                return id switch
                {
                    CurrencySettings.LimitedTomestoneId => Localization.Get("Currency.CurrentLimitedTomestone"),
                    CurrencySettings.NonLimitedTomestoneId => Localization.Get("Currency.CurrentNonLimitedTomestone"),
                    _ => Services.DataManager.GetExcelSheet<Item>().GetRow(id).Name.ToString()
                };
            },
            OnSearchButtonClicked = OpenCurrencyPicker,
            OnChanged = () => {
                System.Config.Currency.DisplayedCurrencies = _currencyListEditor!.GetList();
                RefreshCurrency();
                RecalculateLayout();
                OnLayoutChanged?.Invoke();
            }
        };
        _currencyListEditor.SetList(System.Config.Currency.DisplayedCurrencies);
        AddNode(_currencyListEditor);

        AddTab(1);

        var quickAddRow = new HorizontalListNode { Size = new Vector2(600, 30), ItemSpacing = 8.0f };

        quickAddRow.AddNode(new TextButtonNode {
            String = Localization.Get("Currency.QuickAddGil"), Size = new Vector2(70, 24),
            OnClick = () => AddCurrencyToList(1)
        });

        quickAddRow.AddNode(new TextButtonNode {
            String = Localization.Get("Currency.QuickAddLimited"), Size = new Vector2(150, 24),
            OnClick = () => AddCurrencyToList(CurrencySettings.LimitedTomestoneId)
        });

        quickAddRow.AddNode(new TextButtonNode {
            String = Localization.Get("Currency.QuickAddNonLimited"), Size = new Vector2(110, 24),
            OnClick = () => AddCurrencyToList(CurrencySettings.NonLimitedTomestoneId)
        });
        AddNode(quickAddRow);
        RecalculateLayout();
        OnLayoutChanged?.Invoke();
    }

    private Action<Vector4> CreateColorHandler(Action<Vector4> setter) => newColor =>
    {
        setter(newColor);
        RefreshCurrency();
    };

    private void RefreshCurrency() => System.AddonInventoryWindow.ManualCurrencyRefresh();

    private void AddCurrencyToList(uint id)
    {
        if (_currencyListEditor != null)
        {
            _currencyListEditor.AddValue(id);
            RecalculateLayout();
            OnLayoutChanged?.Invoke();
        }
    }

    private void OpenCurrencyPicker() {
        var picker = new AddonCurrencyPicker
        {
            Title = Localization.Get("ItemPicker.SelectCurrencyToAdd"),
            InternalName = "AetherBagsCN_CurrencyPicker",
        };
        picker.SelectionResult = item =>
        {
            AddCurrencyToList(item.RowId);
        };
        ItemListItemWithAddNode.OnAddClicked = item => AddCurrencyToList(item.RowId);
        picker.Open();
    }
}