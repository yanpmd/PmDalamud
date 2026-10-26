using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using DeepDungeon.Fsd.Dalamud;
using PilgrimsTraverse.YAxis;

namespace PilgrimsTraverse.UI;

internal sealed class FsdWindow : Window
{
    private readonly FsdApplication _application;
    private readonly YAxisAdjustController _yAxis;

    public FsdWindow(FsdApplication application, YAxisAdjustController yAxis)
        : base("你是坏孩子###PilgrimsTraverse")
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _yAxis = yAxis ?? throw new ArgumentNullException(nameof(yAxis));
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##FsdHostTabs"))
            return;

        if (ImGui.BeginTabItem("说明"))
        {
            _application.DrawCompanionSettings();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextWrapped("它也许可以帮助你挖到一些宝藏");
            ImGui.TextWrapped("推荐设定: 启用宝藏+金银, 如果陶片不足启用铜.\n启用 Pilgrim's Traverse 自动选中 + 你所用输出外挂的主动攻击\n死灵术士模式需要较高输出的职业, 如黑魔.");
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("FSD"))
        {
            _application.DrawDeepDungeonFormalPanel();
            ImGui.Separator();
            ImGui.Text("通用辅助");
            _application.DrawGeneralAssistantSettings();
            _yAxis.DrawSettings();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

}
