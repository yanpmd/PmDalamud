using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace Umbra.QoLBar;

/// <summary>
/// A single toolbar bar. In Umbra, every bar is bound to one or more
/// "QoL Bar" toolbar widget instances (see <see cref="Widgets.QoLBarWidget"/>).
/// </summary>
/// <remarks>
/// The JSON property names are kept identical to the original QoLBarCN
/// configuration format so that exported bars can be imported as-is.
/// </remarks>
public class BarCfg
{
    public enum BarDock
    {
        Top,
        Right,
        Bottom,
        Left,
        Undocked
    }

    public enum BarAlign
    {
        LeftOrTop,
        Center,
        RightOrBottom
    }

    public enum BarVisibility
    {
        Slide,
        Immediate,
        Always
    }

    [JsonProperty("n")]  [DefaultValue("")]                   public string Name = string.Empty;
    [JsonProperty("k")]  [DefaultValue(0)]                    public int Hotkey = 0;
    [JsonProperty("sL")] [DefaultValue(null)]                 public List<ShCfg> ShortcutList = new();
    [JsonProperty("h")]  [DefaultValue(false)]                public bool Hidden = false;
    [JsonProperty("d")]  [DefaultValue(BarDock.Bottom)]       public BarDock DockSide = BarDock.Bottom;
    [JsonProperty("a")]  [DefaultValue(BarAlign.Center)]      public BarAlign Alignment = BarAlign.Center;
    [JsonProperty("v")]  [DefaultValue(BarVisibility.Always)] public BarVisibility Visibility = BarVisibility.Always;
    [JsonProperty("ht")] [DefaultValue(false)]                public bool Hint = false;
    [JsonProperty("bW")] [DefaultValue(100)]                  public int ButtonWidth = 100;
    [JsonProperty("e")]  [DefaultValue(false)]                public bool Editing = false;
    [JsonProperty("cT")] [DefaultValue(false)]                public bool ClickThrough = false;
    [JsonProperty("p")]  [DefaultValue(new[] { 0f, 0f })]     public float[] Position = new float[2];
    [JsonProperty("l")]  [DefaultValue(false)]                public bool LockedPosition = false;
    [JsonProperty("co")] [DefaultValue(0)]                    public int Columns = 0;
    [JsonProperty("s")]  [DefaultValue(1.0f)]                 public float Scale = 1.0f;
    [JsonProperty("rA")] [DefaultValue(1.0f)]                 public float RevealAreaScale = 1.0f;
    [JsonProperty("fS")] [DefaultValue(1.0f)]                 public float FontScale = 1.0f;
    [JsonProperty("sp")] [DefaultValue(new[] { 8, 4 })]       public int[] Spacing = { 8, 4 };
    [JsonProperty("nB")] [DefaultValue(false)]                public bool NoBackground = false;
    [JsonProperty("c")]  [DefaultValue(-1)]                   public int ConditionSet = -1;
}

/// <summary>
/// A single shortcut (button) of a bar.
/// </summary>
public class ShCfg
{
    public enum ShortcutType
    {
        Command,
        Category,
        Spacer
    }

    public enum ShortcutMode
    {
        Default,
        Incremental,
        Random
    }

    [JsonProperty("n")]   [DefaultValue("")]                   public string Name = string.Empty;
    [JsonProperty("t")]   [DefaultValue(ShortcutType.Command)] public ShortcutType Type = ShortcutType.Command;
    [JsonProperty("c")]   [DefaultValue("")]                   public string Command = string.Empty;
    [JsonProperty("k")]   [DefaultValue(0)]                    public int Hotkey = 0;
    [JsonProperty("kP")]  [DefaultValue(false)]                public bool KeyPassthrough = false;
    [JsonProperty("sL")]  [DefaultValue(null)]                 public List<ShCfg> SubList;
    [JsonProperty("m")]   [DefaultValue(ShortcutMode.Default)] public ShortcutMode Mode = ShortcutMode.Default;
    [JsonProperty("cl")]  [DefaultValue(0xFFFFFFFF)]           public uint Color = 0xFFFFFFFF;
    [JsonProperty("clA")] [DefaultValue(0)]                    public int ColorAnimation = 0;
    [JsonProperty("iZ")]  [DefaultValue(1.0f)]                 public float IconZoom = 1.0f;
    [JsonProperty("iO")]  [DefaultValue(new[] { 0f, 0f })]     public float[] IconOffset = new float[2];
    [JsonProperty("iR")]  [DefaultValue(0)]                    public float IconRotation = 0.0f;
    [JsonProperty("cdA")] [DefaultValue(0u)]                   public uint CooldownAction = 0;
    [JsonProperty("cdS")] [DefaultValue(0)]                    public int CooldownStyle = 0;
    [JsonProperty("cW")]  [DefaultValue(140)]                  public int CategoryWidth = 140;
    [JsonProperty("cSO")] [DefaultValue(false)]                public bool CategoryStaysOpen = false;
    [JsonProperty("cC")]  [DefaultValue(1)]                    public int CategoryColumns = 1;
    [JsonProperty("cSp")] [DefaultValue(new[] { 8, 4 })]       public int[] CategorySpacing = { 8, 4 };
    [JsonProperty("cS")]  [DefaultValue(1.0f)]                 public float CategoryScale = 1.0f;
    [JsonProperty("cF")]  [DefaultValue(1.0f)]                 public float CategoryFontScale = 1.0f;
    [JsonProperty("cNB")] [DefaultValue(false)]                public bool CategoryNoBackground = false;
    [JsonProperty("cH")]  [DefaultValue(false)]                public bool CategoryOnHover = false;
    [JsonProperty("cHC")] [DefaultValue(false)]                public bool CategoryHoverClose = false;

    [JsonProperty("_i")]  [DefaultValue(0)]                    public int _i = 0;
}

/// <summary>
/// A named set of conditions that can be attached to a bar to control
/// whether its widgets are visible.
/// </summary>
public class CndSetCfg
{
    [JsonProperty("n")] [DefaultValue("")]   public string Name = "";
    [JsonProperty("c")] [DefaultValue(null)] public List<CndCfg> Conditions = new();
}

/// <summary>
/// A single condition of a <see cref="CndSetCfg"/>.
/// </summary>
public class CndCfg
{
    [JsonProperty("i")] [DefaultValue(Conditions.ConditionFlagCondition.ConstId)] public string ID = Conditions.ConditionFlagCondition.ConstId;
    [JsonProperty("a")] [DefaultValue(0)]                                         public dynamic Arg = 0;
    [JsonProperty("n")] [DefaultValue(false)]                                     public bool Negate = false;
    [JsonProperty("o")] [DefaultValue(ConditionManager.BinaryOperator.AND)]       public ConditionManager.BinaryOperator Operator = ConditionManager.BinaryOperator.AND;
}

/// <summary>
/// Global plugin settings that are not tied to a specific bar.
/// </summary>
public class QoLBarSettings
{
    [JsonProperty("language")]          public string Language = "Auto";
    [JsonProperty("useHRIcons")]        public bool   UseHRIcons = false;
    [JsonProperty("noConditionCache")]  public bool   NoConditionCache = false;
    [JsonProperty("exportOnDelete")]    public bool   ExportOnDelete = true;
    [JsonProperty("showWidgetHint")]    public bool   ShowWidgetHint = true;
    [JsonProperty("alwaysDisplayBars")] public bool   AlwaysDisplayBars = false;
}

/// <summary>
/// The root configuration object, persisted as JSON in Umbra's config directory.
/// </summary>
public class QoLBarConfig
{
    [JsonProperty("version")]      public int Version = 1;
    [JsonProperty("bars")]         public Dictionary<string, BarCfg> Bars = new();
    [JsonProperty("conditionSets")] public List<CndSetCfg> ConditionSets = new();
    [JsonProperty("settings")]     public QoLBarSettings Settings = new();
}
