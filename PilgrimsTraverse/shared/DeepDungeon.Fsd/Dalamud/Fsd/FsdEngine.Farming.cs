using global::Dalamud.Bindings.ImGui;
using DeepDungeon.Fsd.Core;
using DeepDungeon.Fsd.Dalamud.GameState;
using DeepDungeon.Fsd.Dalamud.Runtime;
using DeepDungeon.Fsd.Dalamud.Runtime.Scenarios;

namespace DeepDungeon.Fsd.Dalamud;

internal partial class FsdEngine
{
    private string? _farmingError;
    private static readonly string[] FarmingModeLabels =
        ["挖宝藏", "强化／陶片", "宝藏发现成就", "死灵术士(beta)"];
    private static readonly string[] FarmingModeWithSurveyLabels =
        ["挖宝藏", "强化／陶片", "宝藏发现成就", "死灵术士(beta)", "受控采集（研究）"];
    private static readonly string[] HoardFloorsetLabels = ["21–30 层", "31–40 层"];
    private static readonly string[] FarmingDungeonLabels = ["朝圣交错路"];

    private bool IsFixedFloorsetHoard => _fsfScenarioIndex != 2 &&
        _configuration.Farming.Mode == FarmingMode.Hoard &&
        _configuration.Farming.GetPreferences(_configuration).BandedEnabled == true;

    private string? GetSelectedDetailedMapScenarioKey() => IsFixedFloorsetHoard || _fsfScenarioIndex == 2
        ? DetailedMapCatalogManager.GetScenarioKey(_fsfScenarioIndex) : null;

    private void DrawFarmingSelection(bool running)
    {
        ImGui.BeginDisabled(running);
        int dungeon = 0;
        ImGui.SetNextItemWidth(260);
        ImGui.Combo("地宫##fsdDungeon", ref dungeon, FarmingDungeonLabels, FarmingDungeonLabels.Length);
        var settings = _configuration.Farming;
        int mode = _fsfScenarioIndex == 2 ? 4 : (int)settings.Mode;
        var labels = _detailedMapHostOptions.SupportsControlledPtSurvey ? FarmingModeWithSurveyLabels : FarmingModeLabels;
        ImGui.SetNextItemWidth(260);
        if (ImGui.Combo("模式##fsdMode", ref mode, labels, labels.Length))
        {
            if (mode == 4) _fsfScenarioIndex = 2;
            else
            {
                settings.Mode = (FarmingMode)mode;
                if (_fsfScenarioIndex == 2) _fsfScenarioIndex = 1;
            }
            _configuration.NecromancerFsdScenarioIndex = _fsfScenarioIndex;
            _configuration.Save();
            _farmingError = null;
        }
        if (_fsfScenarioIndex != 2)
        {
            if (settings.Mode == FarmingMode.Hoard)
            {
                int floor = _fsfScenarioIndex;
                ImGui.SetNextItemWidth(180);
                if (ImGui.Combo("起始层段##fsdFloorset", ref floor, HoardFloorsetLabels, 2))
                {
                    _fsfScenarioIndex = floor;
                    _configuration.NecromancerFsdScenarioIndex = floor;
                    _configuration.Save();
                }
            }
            else if (settings.Mode == FarmingMode.Aetherpool)
                ImGui.TextWrapped("准备1份较多跳层松杜香的任意起点层存档");
            else if (settings.Mode == FarmingMode.HoardDiscovery)
                ImGui.TextWrapped("准备1份有至少1个感知宝藏的任意起点层存档");
        }
        ImGui.EndDisabled();
    }

    private void DrawFarmingOptions()
    {
        ImGui.Separator();
        ImGui.Text("FSD 选项");
        ImGui.Indent();
        bool running = _ddHost?.FsdActive == true;
        bool research = _fsfScenarioIndex == 2;
        var mode = _configuration.Farming.Mode;
        var preferences = research ? null : _configuration.Farming.GetPreferences(_configuration);
        var options = running ? _ddHost?.RunOptionsProvider?.Current : null;
        bool hoard = options?.BandedEnabled ?? preferences?.BandedEnabled == true;
        bool gold = options?.OpenGold ?? preferences?.OpenGold ?? false;
        bool silver = options?.OpenSilver ?? preferences?.OpenSilver ?? false;
        bool bronze = options?.OpenBronze ?? preferences?.OpenBronze ?? false;
        bool changed;
        ImGui.BeginDisabled(research || FarmingTargetPolicy.LocksHoard(mode));
        changed = ImGui.Checkbox("探索埋藏的宝藏", ref hoard);
        ImGui.EndDisabled();
        ImGui.BeginDisabled(research || FarmingTargetPolicy.LocksGold(mode));
        changed |= ImGui.Checkbox("开启金宝箱", ref gold);
        ImGui.EndDisabled();
        ImGui.BeginDisabled(research || FarmingTargetPolicy.LocksSilverBronze(mode) ||
            mode == FarmingMode.Aetherpool && silver && !bronze);
        changed |= ImGui.Checkbox("开启银宝箱", ref silver);
        ImGui.EndDisabled();
        ImGui.BeginDisabled(research || FarmingTargetPolicy.LocksSilverBronze(mode) ||
            mode == FarmingMode.Aetherpool && bronze && !silver);
        changed |= ImGui.Checkbox("开启铜宝箱", ref bronze);
        ImGui.EndDisabled();
        if (changed && preferences != null)
        {
            preferences.BandedEnabled = hoard;
            preferences.OpenGold = gold; preferences.OpenSilver = silver; preferences.OpenBronze = bronze;
            _ddHost?.SetFarmingTargets(hoard, gold, silver, bronze);
            _configuration.Save();
            _farmingError = null;
        }
        ImGui.BeginDisabled(research || mode == FarmingMode.HoardDiscovery);
        bool aggressiveChestInteraction = _configuration.AggressiveChestInteraction;
        if (ImGui.Checkbox("我开箱会卡住很久##aggressiveChestInteraction", ref aggressiveChestInteraction))
        {
            _configuration.AggressiveChestInteraction = aggressiveChestInteraction;
            _configuration.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 32f);
            ImGui.TextUnformatted("启用该项目会增加开箱的力度, 如帧数较低或战斗时很难成功开箱, 请启用以增加对输出或其他外挂抢占成功率. 若没有遇到这个问题, 请不要启用");
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
        ImGui.EndDisabled();
        ImGui.Spacing();
        DrawCycleOptions(preferences);
        if (running)
        {
            ImGui.TextDisabled($"完成循环：{_ddHost!.CompletedLoops}/{(_ddHost.Infinite ? "∞" : _ddHost.TargetLoops)}");
            if (mode == FarmingMode.DeepProgression && !research)
                ImGui.TextDisabled($"失败重试：{_ddHost.FarmingFailures}");
            if (mode == FarmingMode.HoardDiscovery && !research)
                ImGui.TextDisabled($"宝藏发现：{_ddHost.HoardDiscoveries}");
            if (_ddHost.Context?.FarmingPlan?.SaveUse == SaveUse.Prepared)
                ImGui.TextDisabled(_ddHost.PreparedSaveDescription);
        }
        ImGui.Unindent();
        string? error = _farmingError ?? (_ddHost is { FsdActive: false, LastStatusIsError: true }
            ? _ddHost.LastStatus : null);
        if (!string.IsNullOrEmpty(error))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 0.55f, 0.55f, 1f));
            ImGui.TextWrapped(error);
            ImGui.PopStyleColor();
        }
    }

    private void DrawCycleOptions(FarmingPreferences? preferences)
    {
        ImGui.Text("结束 FSD...");
        bool infinite = _ddHost?.FsdActive == true ? _ddHost.Infinite : preferences?.Infinite ?? _configuration.NecromancerFsdLoopInfinite;
        int cycles = _ddHost?.FsdActive == true ? _ddHost.TargetLoops : preferences?.Cycles ?? _configuration.NecromancerFsdLoopCount;
        bool changed = ImGui.Checkbox("无限循环##fsf_inf", ref infinite);
        ImGui.SameLine();
        ImGui.BeginDisabled(infinite);
        ImGui.SetNextItemWidth(90);
        changed |= ImGui.InputInt("循环次数##fsf_loops", ref cycles);
        ImGui.EndDisabled();
        if (changed)
        {
            cycles = Math.Max(1, cycles);
            if (preferences != null) { preferences.Cycles = cycles; preferences.Infinite = infinite; }
            else { _configuration.NecromancerFsdLoopCount = cycles; _configuration.NecromancerFsdLoopInfinite = infinite; }
            _configuration.Save();
            _ddHost?.SetCycleTargets(cycles, infinite);
        }
    }

    public FsdControlResult StartSelectedFarming(FsdStartRequest request)
    {
        var scenario = request.Scenario ?? (_configuration.NecromancerFsdScenarioIndex == 2
            ? FsdStartScenario.ControlledSurvey : FsdStartScenario.Farming);
        if (scenario == FsdStartScenario.ControlledSurvey)
        {
            if (request.DiagnosticStartFloor.HasValue || request.ResumeSlot.HasValue || request.HoldOnFailure)
                return new FsdControlResult(false, "Diagnostic checkpoints are unavailable for controlled survey capture.");
            return StartControlledPilgrimsTraverseCapture(
                Math.Max(1, _configuration.NecromancerFsdLoopCount), _configuration.NecromancerFsdLoopInfinite,
                "start-controlled-pt-capture");
        }
        var settings = _configuration.Farming;
        var p = settings.GetPreferences(_configuration);
        bool prepared = settings.Mode is FarmingMode.Aetherpool or FarmingMode.HoardDiscovery;
        var farming = new FsdFarmingRequest(settings.Mode, prepared ? SaveUse.Prepared : SaveUse.Create,
            settings.Mode == FarmingMode.DeepProgression ? 1 : _fsfScenarioIndex == 0 ? 21 : 31,
            p.Cycles, p.Infinite, new FarmingTargets(p.BandedEnabled == true, p.OpenGold, p.OpenSilver, p.OpenBronze), request);
        bool started = TryStartFarming(farming, out string error);
        return new FsdControlResult(started, error);
    }

    public bool TryStartFarming(FsdFarmingRequest request, out string error)
    {
        var (mode, saveUse, startFloor, cycles, infinite, targets, checkpointOptions) = request;
        var (hoard, gold, silver, bronze) = targets;
        var (stopAfterFloor, diagnosticStartFloor, resumeSlot, holdOnFailure, _) = checkpointOptions ?? new FsdStartRequest();
        if (stopAfterFloor.HasValue && (mode != FarmingMode.DeepProgression ||
            stopAfterFloor.Value is < 10 or > 100 || stopAfterFloor.Value % 10 != 0))
        {
            error = "停止层数只支援死灵术士模式，且必须为 10 至 100 的十层边界。";
            return false;
        }
        if (!FarmingPlan.TryCreate(mode, saveUse, startFloor, cycles, infinite, hoard, gold, silver, bronze,
                out var plan, out error)) return false;
        if ((holdOnFailure || resumeSlot.HasValue) && (mode != FarmingMode.DeepProgression ||
            !stopAfterFloor.HasValue || (resumeSlot.HasValue &&
            (resumeSlot.Value < 0 || !diagnosticStartFloor.HasValue))))
        {
            error = "保留现场／续用槽位只支援有停止边界的深层测试；续用槽位须指定预期起点。";
            return false;
        }
        if (diagnosticStartFloor is { } checkpoint && (mode != FarmingMode.DeepProgression ||
            (resumeSlot.HasValue ? checkpoint is < 1 or > 91 || checkpoint % 10 != 1
                : checkpoint is not (1 or 21 or 31 or 51 or 71)) ||
            !stopAfterFloor.HasValue || stopAfterFloor.Value < checkpoint + 9))
        {
            error = "新建测试支援1／21／31／51／71层；指定续用槽位支援1至91层的十层组起点，均须指定组末或之后的停止层数。";
            return false;
        }
        plan = plan! with { StopAfterFloor = stopAfterFloor, StartFloor = diagnosticStartFloor ?? plan.StartFloor,
            UsesDiagnosticCheckpoints = diagnosticStartFloor.HasValue, HoldOnFailure = holdOnFailure };
        bool resumeCurrentDuty = _dutyState?.IsInDuty == true;
        if (resumeCurrentDuty && (!holdOnFailure || !resumeSlot.HasValue ||
            _dutyState!.IsTransitioning || _dutyState.DungeonId != 4 ||
            _dutyState.Floor < plan.StartFloor || _dutyState.Floor >= plan.StartFloor + 10))
        {
            error = "原副本接续只支援保留现场的PT测试，须指定来源槽位与目前所在层段起点。";
            return false;
        }
        var session = new FarmingSession(plan, resumeSlot ?? -1, resumeCurrentDuty);
        string? key = plan!.ShowsDetailedMap ? startFloor == 21
            ? DetailedMapScenarioCatalog.PilgrimsTraverse21To30.Key
            : DetailedMapScenarioCatalog.PilgrimsTraverse31To40.Key : null;
        bool started = TryStartOutsideDutyFsd(() => new FarmingScenario(session), cycles, infinite, key, out error);
        if (started)
        {
            _farmingError = null;
            _configuration.Farming.Mode = mode;
            _fsfScenarioIndex = mode == FarmingMode.Hoard && startFloor == 21 ? 0 : 1;
            _configuration.NecromancerFsdScenarioIndex = _fsfScenarioIndex;
            var p = _configuration.Farming.GetPreferences(_configuration);
            p.BandedEnabled = hoard; p.OpenGold = gold; p.OpenSilver = silver; p.OpenBronze = bronze;
            p.Cycles = cycles; p.Infinite = infinite;
            _configuration.Save();
        }
        return started;
    }
}
