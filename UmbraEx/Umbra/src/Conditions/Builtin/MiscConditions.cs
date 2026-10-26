using Dalamud.Game.ClientState.Keys;

namespace Umbra.Conditions.Builtin;

/// <summary>Is another Dalamud plugin installed and currently loaded?</summary>
[ConditionCategory("Misc", 40)]
internal sealed class PluginEnabledCondition : IDisplayCondition
{
    public string Id   => "plugin";
    public string Name => "插件已启用";

    public bool Check(ConditionArg arg)
    {
        if (string.IsNullOrWhiteSpace(arg.Text)) return false;

        try {
            return Framework.DalamudPlugin.InstalledPlugins
                            .Any(plugin => plugin.InternalName == arg.Text && plugin.IsLoaded);
        } catch {
            return false;
        }
    }

    public string Describe(ConditionArg arg)
    {
        return string.IsNullOrWhiteSpace(arg.Text) ? "（未选择插件）" : arg.Text;
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        var options = new List<(string Id, string Label)>();

        try {
            options.AddRange(
                Framework.DalamudPlugin.InstalledPlugins
                         .OrderBy(plugin => plugin.Name)
                         .Select(plugin => (plugin.InternalName, $"{plugin.Name} ({plugin.InternalName})"))
            );
        } catch {
            // Plugin list is unavailable while Dalamud is still starting up.
        }

        if (options.Count == 0) {
            container.AppendChild(new Node { NodeValue = "尚无可选插件。" });

            return;
        }

        container.AppendChild(
            SelectHelper.Create(
                $"plugin{idSuffix}",
                options,
                arg.Text,
                value => {
                    arg.Text = value;
                    onChanged();
                }
            )
        );
    }
}

[ConditionCategory("Misc", 40)]
internal sealed class LoggedInCondition : IDisplayCondition
{
    public string Id   => "login";
    public string Name => "已登录游戏";

    public bool Check(ConditionArg arg)
    {
        return ConditionServices.ClientState?.IsLoggedIn ?? false;
    }

    public string Describe(ConditionArg arg)
    {
        return Check(arg) ? "已登录" : "未登录";
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        container.AppendChild(new Node { NodeValue = "角色登录游戏时成立。" });
    }
}

/// <summary>Only matches one specific character, identified by its content id.</summary>
[ConditionCategory("Misc", 40)]
internal sealed class CharacterCondition : IDisplayCondition
{
    public string Id   => "character";
    public string Name => "角色 ID";

    public bool Check(ConditionArg arg)
    {
        if (!ulong.TryParse(arg.Text, out ulong contentId) || contentId == 0) return false;

        return ConditionServices.PlayerState?.ContentId == contentId;
    }

    public string Describe(ConditionArg arg)
    {
        if (!ulong.TryParse(arg.Text, out ulong contentId) || contentId == 0) return "（未指定角色）";

        return contentId == ConditionServices.PlayerState?.ContentId
            ? $"当前角色 ({contentId})"
            : contentId.ToString();
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        var useCurrent = new ButtonNode($"character-current{idSuffix}", "使用当前角色", null, true, true);

        useCurrent.OnClick += _ => {
            if (ConditionServices.PlayerState is not { } state) return;

            arg.Text = state.ContentId.ToString();
            onChanged();
        };

        var clear = new ButtonNode($"character-clear{idSuffix}", "清除", FontAwesomeIcon.Times, true, true);

        clear.OnClick += _ => {
            arg.Text = string.Empty;
            onChanged();
        };

        container.AppendChild(useCurrent);
        container.AppendChild(clear);

        if (!string.IsNullOrWhiteSpace(arg.Text)) {
            container.AppendChild(new Node { NodeValue = $"已指定：{arg.Text}" });
        }
    }
}

[ConditionCategory("Misc", 40)]
internal sealed class TargetExistsCondition : IDisplayCondition
{
    public string Id   => "target";
    public string Name => "目标存在";

    public bool Check(ConditionArg arg)
    {
        if (ConditionServices.TargetManager is not { } targets) return false;

        return arg.Int1 switch {
            1 => targets.FocusTarget is not null,
            2 => targets.SoftTarget is not null,
            _ => targets.Target is not null,
        };
    }

    public string Describe(ConditionArg arg)
    {
        return arg.Int1 switch {
            1 => "存在焦点目标",
            2 => "存在软目标",
            _ => "存在目标",
        };
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        List<(string Id, string Label)> options = [
            ("0", "目标"),
            ("1", "焦点目标"),
            ("2", "软目标"),
        ];

        container.AppendChild(
            SelectHelper.Create(
                $"target{idSuffix}",
                options,
                arg.Int1.ToString(),
                value => {
                    if (int.TryParse(value, out int index)) arg.Int1 = index;
                    onChanged();
                }
            )
        );
    }
}

/// <summary>Is a specific key currently being held down?</summary>
[ConditionCategory("Misc", 40)]
internal sealed class KeyHeldCondition : IDisplayCondition
{
    public string Id   => "keyheld";
    public string Name => "按键按住";

    private static List<(string Id, string Label)>? _options;

    public bool Check(ConditionArg arg)
    {
        if (arg.Int1 <= 0) return false;

        try {
            return ConditionServices.KeyState?[arg.Int1] ?? false;
        } catch {
            return false;
        }
    }

    public string Describe(ConditionArg arg)
    {
        string label = GetOptions().FirstOrDefault(entry => entry.Id == arg.Int1.ToString()).Label;

        return string.IsNullOrEmpty(label) ? "（未选择按键）" : $"按住 {label}";
    }

    public void BuildEditor(Node container, ConditionArg arg, string idSuffix, Action onChanged)
    {
        container.AppendChild(
            SelectHelper.Create(
                $"keyheld{idSuffix}",
                GetOptions(),
                arg.Int1.ToString(),
                value => {
                    if (int.TryParse(value, out int key)) arg.Int1 = key;
                    onChanged();
                }
            )
        );
    }

    private static List<(string Id, string Label)> GetOptions()
    {
        if (_options is not null) return _options;

        var options = new List<(string Id, string Label)> { ("0", "（未选择）") };

        foreach (VirtualKey key in Enum.GetValues<VirtualKey>().Distinct()) {
            string name = key.ToString();

            bool isModifier = key is VirtualKey.SHIFT or VirtualKey.CONTROL or VirtualKey.MENU
                                      or VirtualKey.SPACE or VirtualKey.TAB;
            bool isLetter = name.Length == 1 && char.IsLetter(name[0]);
            bool isDigit  = name.StartsWith("KEY_", StringComparison.Ordinal) && int.TryParse(name[4..], out _);
            bool isFN     = name.Length is 2 or 3 && name[0] == 'F' && int.TryParse(name[1..], out int fn)
                                                                && fn is >= 1 and <= 12;

            if (!isModifier && !isLetter && !isDigit && !isFN) continue;

            options.Add((((int)key).ToString(), isDigit ? name[4..] : name));
        }

        _options = options;

        return options;
    }
}
