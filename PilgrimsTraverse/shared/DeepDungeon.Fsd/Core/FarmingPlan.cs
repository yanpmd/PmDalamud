namespace DeepDungeon.Fsd.Core;

public enum FarmingMode { Hoard, Aetherpool, HoardDiscovery, DeepProgression }
public enum SaveUse { Create, Prepared }

/// <summary>Validated, immutable intent shared by UI, API and the runtime.</summary>
public sealed record FarmingPlan(
    FarmingMode Mode, SaveUse SaveUse, int StartFloor, int TargetCycles, bool Infinite,
    bool BandedEnabled, bool OpenGold, bool OpenSilver, bool OpenBronze)
{
    public string DisplayName => Mode switch
    {
        FarmingMode.Hoard => "挖宝藏",
        FarmingMode.Aetherpool => "强化／陶片",
        FarmingMode.HoardDiscovery => "宝藏发现成就",
        _ => "死灵术士(beta)"
    };
    // Optional run-only diagnostic boundary; normal progression still climbs to 100.
    public int? StopAfterFloor { get; init; }
    // Bridge-only checkpoint tests; UI challenge runs still start/restart at floor one.
    public bool UsesDiagnosticCheckpoints { get; init; }
    // Run-only diagnostic option: retain the duty and save for in-place inspection.
    public bool HoldOnFailure { get; init; }
    public bool StopsForTerminalReview => Mode == FarmingMode.DeepProgression && StopAfterFloor == 100;
    public bool ReusesSave => Mode is FarmingMode.Aetherpool or FarmingMode.HoardDiscovery;
    public bool ShowsDetailedMap => Mode == FarmingMode.Hoard && BandedEnabled;
    public string CycleUnit => Mode == FarmingMode.DeepProgression ? "登顶次数" : Mode == FarmingMode.Hoard ? "挖宝轮数" : "采集轮数";

    public static bool TryCreate(FarmingMode mode, SaveUse saveUse, int startFloor,
        int cycles, bool infinite, bool hoard, bool gold, bool silver, bool bronze,
        out FarmingPlan? plan, out string error)
    {
        plan = null;
        error = !Enum.IsDefined(mode) || !Enum.IsDefined(saveUse) ? "未知玩法或存档用途。"
            : cycles < 1 ? "循环次数必须至少为 1。"
            : mode == FarmingMode.Hoard && saveUse != SaveUse.Create
                ? "固定层段模式只使用空槽位新建存档，不接受既有存档。"
            : mode is FarmingMode.Aetherpool or FarmingMode.HoardDiscovery && saveUse != SaveUse.Prepared
                ? "此玩法需要唯一的准备存档。"
            : mode == FarmingMode.Aetherpool && !silver && !bronze
                ? "强化／陶片玩法至少选择银箱或铜箱。"
            : !FarmingTargetPolicy.IsValid(mode, new(hoard, gold, silver, bronze))
                ? "此模式的宝藏／宝箱选项不相容。"
            : mode == FarmingMode.DeepProgression && (saveUse != SaveUse.Create || startFloor != 1)
                ? "深层攻略必须从第一层新建存档。"
            : mode == FarmingMode.Hoard && saveUse == SaveUse.Create && startFloor is not (21 or 31)
                ? "目前固定层段挖宝支援 PT 21–30、31–40。"
            : string.Empty;
        if (error.Length != 0) return false;
        plan = new(mode, saveUse, saveUse == SaveUse.Prepared ? 0 : startFloor,
            cycles, infinite, hoard, gold, silver, bronze);
        return true;
    }
}

public readonly record struct FarmingTargets(bool Hoard, bool Gold, bool Silver, bool Bronze);

public static class FarmingTargetPolicy
{
    public static bool LocksHoard(FarmingMode mode) => mode is FarmingMode.Aetherpool or FarmingMode.HoardDiscovery or FarmingMode.DeepProgression;
    public static bool LocksGold(FarmingMode mode) => LocksHoard(mode);
    public static bool LocksSilverBronze(FarmingMode mode) => mode is FarmingMode.HoardDiscovery or FarmingMode.DeepProgression;

    public static FarmingTargets Constrain(FarmingMode mode, in FarmingTargets targets) => mode switch
    {
        FarmingMode.Aetherpool => new(false, false, targets.Silver || !targets.Bronze, targets.Bronze),
        FarmingMode.HoardDiscovery => new(true, false, false, false),
        FarmingMode.DeepProgression => new(false, true, true, false),
        _ => targets
    };

    public static bool IsValid(FarmingMode mode, in FarmingTargets targets) =>
        targets == Constrain(mode, targets) && (mode != FarmingMode.Aetherpool || targets.Silver || targets.Bronze);
}

public readonly record struct SaveSlotSnapshot(int Index, bool Empty, int StartFloor, string Progress, string Identity = "", bool Enterable = true);

public static class SaveSlotUiPolicy
{
    public static bool IsBlockingMessage(string label, string? failedLabel, string? completedLabel) =>
        (!string.IsNullOrWhiteSpace(failedLabel) && label == failedLabel) ||
        (!string.IsNullOrWhiteSpace(completedLabel) && label == completedLabel);

    public static bool TryParse(int index, string? progress, string? emptyLabel, string identity,
        bool enabled, out SaveSlotSnapshot slot, out string error)
    {
        slot = default;
        error = $"存档 {index + 1} 的进度文字尚未载入或无法识别。";
        if (index < 0 || string.IsNullOrWhiteSpace(progress) || string.IsNullOrWhiteSpace(emptyLabel)) return false;
        progress = progress.Trim();
        bool empty = string.Equals(progress, emptyLabel.Trim(), StringComparison.Ordinal);
        int floor = 0;
        if (!empty)
        {
            var numbers = System.Text.RegularExpressions.Regex.Matches(progress, "[0-9]+");
            if (numbers.Count != 1 || !int.TryParse(numbers[0].Value, out floor)) return false;
        }
        slot = new(index, empty, floor, progress, identity, enabled);
        error = string.Empty;
        return true;
    }
}

public static class PreparedSavePolicy
{
    public static bool TryResolve(IReadOnlyList<SaveSlotSnapshot> slots,
        SaveSlotSnapshot? pinned, bool reusable, int? expectedFloor,
        out SaveSlotSnapshot source, out string error, int? requiredSlot = null)
    {
        source = default;
        error = string.Empty;
        int occupied = 0;
        bool managed = requiredSlot.HasValue && !reusable;
        foreach (var slot in slots)
            if (!slot.Empty && (!managed || slot.Index == requiredSlot.GetValueOrDefault())) { source = slot; occupied++; }
        if (occupied != 1)
            error = managed ? "本任务管理的存档不存在或槽位资料不唯一；已停止，不会改选其他存档。"
                : occupied == 0 ? "请先准备一个存档。" : "使用既有存档要求有且只有一个存档；请自行整理后重试。";
        else if (source.Index < 0)
            error = "无法识别来源槽位。";
        else if (!source.Enterable)
            error = managed ? "本任务存档目前无法入场；已停止，不会改选其他存档。"
                : "唯一存档目前无法入场，不能被当成空槽位。";
        else if (source.StartFloor is < 1 or > 91 || source.StartFloor % 10 != 1)
            error = "无法可靠识别存档起点，或存档已完成／不可入场。";
        else if (reusable && source.StartFloor > 21)
            error = "PT 31 层起弃本会使存档失效；复用玩法只接受 1、11、21 层起点。";
        else if (pinned.HasValue && (source.Index != pinned.Value.Index ||
                 source.Progress != pinned.Value.Progress || source.Identity != pinned.Value.Identity))
            error = "来源存档已变更；已停止，不会改选另一份存档。";
        else if (expectedFloor.HasValue && source.StartFloor != expectedFloor.Value)
            error = $"存档起点为 {source.StartFloor}，预期为 {expectedFloor.Value}；已停止。";
        return error.Length == 0;
    }
}

public static class OwnedSavePolicy
{
    public static string? ValidateForDeletion(IReadOnlyList<SaveSlotSnapshot> slots,
        int ownedSlot, int entryFloor, SaveSlotSnapshot? pinned)
    {
        SaveSlotSnapshot source = default;
        int matches = 0;
        foreach (var slot in slots)
            if (slot.Index == ownedSlot) { source = slot; matches++; }
        if (ownedSlot < 0 || entryFloor < 1 || matches != 1 || source.Empty || source.Progress == null ||
            source.StartFloor < entryFloor || source.StartFloor > entryFloor + 10)
            return "本任务存档内容与预期层段不符；停止清理。";
        if (pinned is { } expected && (source.Index != expected.Index || source.Identity != expected.Identity))
            return "本任务存档来源资讯已变更；停止清理。";
        return null;
    }
}
