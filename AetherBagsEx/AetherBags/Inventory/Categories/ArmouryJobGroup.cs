using System;
using System.Collections.Generic;
using AetherBags.Inventory.Items;
using Lumina.Excel.Sheets;

namespace AetherBags.Inventory.Categories;

/// <summary>
/// Combat role groupings used by the armoury window. The raw game class job
/// category is too granular (it lists every job a piece fits), so the armoury
/// folds them into the five combat roles plus the crafting/gathering roles.
/// The numeric values double as the display order of the category headers.
/// </summary>
public enum ArmouryJobGroup : uint
{
    Tank = 1,
    Healer = 2,
    Melee = 3,
    Ranged = 4,
    Caster = 5,
    Crafter = 6,
    Gatherer = 7,
    Combat = 8,   // shared by several roles / all classes, sorted last
    None = 9,
}

public static class ArmouryJobGroups
{
    // Bucket keys are sorted numerically by the category list, so the key itself
    // encodes the display order: role first, then the job inside that role.
    //   role key : flag | role << 16
    //   job key  : flag | role << 16 | jobId
    //   unknown  : a separate, larger flag so those categories sort last
    private const uint GroupKeyFlag = 0x0800_0000;
    private const uint UnknownCategoryKeyFlag = 0x1000_0000;
    private const int GroupShift = 16;
    private const uint JobIdMask = 0xFFFF;
    private const uint CategoryIdMask = 0x00FF_FFFF;

    /// <summary>
    /// Covering this many combat roles means "shared by everyone" rather than a
    /// specific role, e.g. Disciple of War gear or All Classes accessories.
    /// </summary>
    private const int BroadCoverageThreshold = 3;

    private static readonly Dictionary<uint, uint> BucketKeyByCategoryId = new(capacity: 128);
    private static readonly Dictionary<uint, string> JobNameCache = new(capacity: 64);
    private static readonly Dictionary<uint, string> CategoryNameCache = new(capacity: 128);

    /// <summary>
    /// Class job rows with their final role and the matching ClassJobCategory
    /// boolean column. Base classes are folded into their job's role so counting
    /// stays proportional.
    /// </summary>
    private static readonly (uint jobId, ArmouryJobGroup group, Func<ClassJobCategory, bool> test)[] JobColumns =
    [
        (1, ArmouryJobGroup.Tank, row => row.GLA),
        (2, ArmouryJobGroup.Melee, row => row.PGL),
        (3, ArmouryJobGroup.Tank, row => row.MRD),
        (4, ArmouryJobGroup.Melee, row => row.LNC),
        (5, ArmouryJobGroup.Ranged, row => row.ARC),
        (6, ArmouryJobGroup.Healer, row => row.CNJ),
        (7, ArmouryJobGroup.Caster, row => row.THM),
        (8, ArmouryJobGroup.Crafter, row => row.CRP),
        (9, ArmouryJobGroup.Crafter, row => row.BSM),
        (10, ArmouryJobGroup.Crafter, row => row.ARM),
        (11, ArmouryJobGroup.Crafter, row => row.GSM),
        (12, ArmouryJobGroup.Crafter, row => row.LTW),
        (13, ArmouryJobGroup.Crafter, row => row.WVR),
        (14, ArmouryJobGroup.Crafter, row => row.ALC),
        (15, ArmouryJobGroup.Crafter, row => row.CUL),
        (16, ArmouryJobGroup.Gatherer, row => row.MIN),
        (17, ArmouryJobGroup.Gatherer, row => row.BTN),
        (18, ArmouryJobGroup.Gatherer, row => row.FSH),
        (19, ArmouryJobGroup.Tank, row => row.PLD),
        (20, ArmouryJobGroup.Melee, row => row.MNK),
        (21, ArmouryJobGroup.Tank, row => row.WAR),
        (22, ArmouryJobGroup.Melee, row => row.DRG),
        (23, ArmouryJobGroup.Ranged, row => row.BRD),
        (24, ArmouryJobGroup.Healer, row => row.WHM),
        (25, ArmouryJobGroup.Caster, row => row.BLM),
        (26, ArmouryJobGroup.Caster, row => row.ACN),
        (27, ArmouryJobGroup.Caster, row => row.SMN),
        (28, ArmouryJobGroup.Healer, row => row.SCH),
        (29, ArmouryJobGroup.Melee, row => row.ROG),
        (30, ArmouryJobGroup.Melee, row => row.NIN),
        (31, ArmouryJobGroup.Ranged, row => row.MCH),
        (32, ArmouryJobGroup.Tank, row => row.DRK),
        (33, ArmouryJobGroup.Healer, row => row.AST),
        (34, ArmouryJobGroup.Melee, row => row.SAM),
        (35, ArmouryJobGroup.Caster, row => row.RDM),
        (36, ArmouryJobGroup.Caster, row => row.BLU),
        (37, ArmouryJobGroup.Tank, row => row.GNB),
        (38, ArmouryJobGroup.Ranged, row => row.DNC),
        (39, ArmouryJobGroup.Melee, row => row.RPR),
        (40, ArmouryJobGroup.Healer, row => row.SGE),
        (41, ArmouryJobGroup.Melee, row => row.VPR),
        (42, ArmouryJobGroup.Caster, row => row.PCT),
    ];

    /// <summary>
    /// Base class to job links. A piece that only fits a base class and its job
    /// belongs to a single class line and is listed under the job name, e.g.
    /// Marauder + Warrior shows up as "Warrior" instead of the tank role.
    /// </summary>
    private static readonly (uint baseJob, uint job)[] JobLines =
    [
        (1, 19),   // Gladiator -> Paladin
        (2, 20),   // Pugilist -> Monk
        (3, 21),   // Marauder -> Warrior
        (4, 22),   // Lancer -> Dragoon
        (5, 23),   // Archer -> Bard
        (6, 24),   // Conjurer -> White Mage
        (7, 25),   // Thaumaturge -> Black Mage
        (26, 27),  // Arcanist -> Summoner
        (26, 28),  // Arcanist -> Scholar
        (29, 30),  // Rogue -> Ninja
    ];

    /// <summary>
    /// Beastmaster has no dedicated column in the current sheet schema, so its
    /// exclusive categories are recognised by name and folded into melee.
    /// </summary>
    private static readonly string[] BeastmasterNameMarkers =
    [
        "驯兽师",
        "Beastmaster",
    ];

    /// <summary>
    /// Resolves the category bucket key for an armoury item: the job name when the
    /// gear only fits one job, a role group otherwise, and the game's own category
    /// name when it fits no known job at all.
    /// </summary>
    public static uint ResolveBucketKey(ItemInfo info)
    {
        uint categoryId = info.ClassJobCategoryId;
        if (categoryId == 0) return MakeGroupKey(ArmouryJobGroup.None);

        if (BucketKeyByCategoryId.TryGetValue(categoryId, out uint cached))
            return cached;

        uint key;
        try
        {
            key = ComputeBucketKey(info, categoryId);
        }
        catch
        {
            key = MakeGroupKey(ArmouryJobGroup.None);
        }

        BucketKeyByCategoryId[categoryId] = key;
        return key;
    }

    /// <summary>Base class id shared by a base class and its job; a job returns its base.</summary>
    private static uint GetJobLine(uint jobId)
    {
        foreach (var (baseJob, job) in JobLines)
        {
            if (job == jobId) return baseJob;
        }

        return jobId;
    }

    /// <summary>The job of a class line, or the id itself when it has no job.</summary>
    private static uint GetDefaultJobForLine(uint line)
    {
        foreach (var (baseJob, job) in JobLines)
        {
            if (baseJob == line) return job;
        }

        return line;
    }

    private static ArmouryJobGroup GetJobGroup(uint jobId)
    {
        foreach (var (id, group, _) in JobColumns)
        {
            if (id == jobId) return group;
        }

        return ArmouryJobGroup.None;
    }

    private static uint MakeGroupKey(ArmouryJobGroup group) => GroupKeyFlag | ((uint)group << GroupShift);

    private static uint MakeJobKey(ArmouryJobGroup group, uint jobId)
        => GroupKeyFlag | ((uint)group << GroupShift) | (jobId & JobIdMask);

    private static uint MakeUnknownCategoryKey(uint categoryId)
        => UnknownCategoryKeyFlag | (categoryId & CategoryIdMask);

    private static uint ComputeBucketKey(ItemInfo info, uint categoryId)
    {
        var rowRef = info.ClassJobCategory;
        if (rowRef.RowId == 0 || rowRef.ValueNullable is not { } row)
            return MakeGroupKey(ArmouryJobGroup.None);

        int totalJobs = 0;
        uint firstLine = 0;
        bool allSameLine = true;
        uint jobInLine = 0;

        int tank = 0, healer = 0, melee = 0, ranged = 0, caster = 0, crafter = 0, gatherer = 0;

        foreach (var (jobId, group, test) in JobColumns)
        {
            if (!test(row)) continue;

            totalJobs++;

            uint line = GetJobLine(jobId);
            if (totalJobs == 1)
                firstLine = line;
            else if (line != firstLine)
                allSameLine = false;

            // Remember the job (not the base class) of that line.
            if (jobId != line)
                jobInLine = jobId;

            switch (group)
            {
                case ArmouryJobGroup.Tank: tank++; break;
                case ArmouryJobGroup.Healer: healer++; break;
                case ArmouryJobGroup.Melee: melee++; break;
                case ArmouryJobGroup.Ranged: ranged++; break;
                case ArmouryJobGroup.Caster: caster++; break;
                case ArmouryJobGroup.Crafter: crafter++; break;
                case ArmouryJobGroup.Gatherer: gatherer++; break;
            }
        }

        // No known job matched: fall back to the game's own category name so new or
        // limited jobs never end up in a generic bucket.
        if (totalJobs == 0)
        {
            string rawName = GetRawCategoryName(categoryId);
            return IsMeleeOnlyCategory(rawName)
                ? MakeGroupKey(ArmouryJobGroup.Melee)
                : MakeUnknownCategoryKey(categoryId);
        }

        // Everything fits a single class line (base class and/or its job), so the
        // item belongs to that job - "Marauder + Warrior" is just "Warrior".
        if (allSameLine)
        {
            uint jobId = jobInLine != 0 ? jobInLine : GetDefaultJobForLine(firstLine);

            return TryGetJobName(jobId) is not null
                ? MakeJobKey(GetJobGroup(jobId), jobId)
                : MakeUnknownCategoryKey(categoryId);
        }

        int combatRoles = (tank > 0 ? 1 : 0) + (healer > 0 ? 1 : 0) + (melee > 0 ? 1 : 0)
                          + (ranged > 0 ? 1 : 0) + (caster > 0 ? 1 : 0);

        if (combatRoles == 0)
        {
            if (crafter > 0) return MakeGroupKey(ArmouryJobGroup.Crafter);
            if (gatherer > 0) return MakeGroupKey(ArmouryJobGroup.Gatherer);
            return MakeGroupKey(ArmouryJobGroup.None);
        }

        // Gear that fits three or more roles is role agnostic.
        if (combatRoles >= BroadCoverageThreshold)
            return MakeGroupKey(ArmouryJobGroup.Combat);

        // Otherwise the role with the most jobs wins, e.g. "ARC ROG BRD NIN MCH DNC
        // VPR" is ranged (4) over melee (3). A tie falls back to the shared bucket.
        ArmouryJobGroup best = ArmouryJobGroup.None;
        int bestCount = 0;
        bool tied = false;

        void Consider(int count, ArmouryJobGroup group)
        {
            if (count == 0) return;

            if (count > bestCount)
            {
                bestCount = count;
                best = group;
                tied = false;
            }
            else if (count == bestCount)
            {
                tied = true;
            }
        }

        Consider(tank, ArmouryJobGroup.Tank);
        Consider(healer, ArmouryJobGroup.Healer);
        Consider(melee, ArmouryJobGroup.Melee);
        Consider(ranged, ArmouryJobGroup.Ranged);
        Consider(caster, ArmouryJobGroup.Caster);

        if (best == ArmouryJobGroup.None || tied)
            return MakeGroupKey(ArmouryJobGroup.Combat);

        return MakeGroupKey(best);
    }

    private static bool IsMeleeOnlyCategory(string categoryName)
    {
        if (string.IsNullOrEmpty(categoryName)) return false;

        foreach (string marker in BeastmasterNameMarkers)
        {
            if (categoryName.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static string GetBucketName(uint key)
    {
        if ((key & UnknownCategoryKeyFlag) != 0)
            return GetRawCategoryName(key & CategoryIdMask);

        if ((key & GroupKeyFlag) != 0)
        {
            uint jobId = key & JobIdMask;
            if (jobId != 0) return GetJobName(jobId);

            var group = (ArmouryJobGroup)((key >> GroupShift) & 0xF);
            return Localization.Get($"ArmouryGroup.{group}");
        }

        return Localization.Get($"ArmouryGroup.{(ArmouryJobGroup)key}");
    }

    private static string GetJobName(uint jobId) => TryGetJobName(jobId) ?? Localization.Get("ArmouryGroup.None");

    private static string? TryGetJobName(uint jobId)
    {
        if (JobNameCache.TryGetValue(jobId, out string? cached))
            return cached.Length == 0 ? null : cached;

        string name = string.Empty;
        try
        {
            var sheet = Services.DataManager.GetExcelSheet<ClassJob>();
            if (sheet.TryGetRow(jobId, out var row))
                name = ShortenJobName(row.Name.ToString(), row.Abbreviation.ToString());
        }
        catch
        {
            // Class job sheet unavailable, fall through to the null result.
        }

        JobNameCache[jobId] = name;
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>
    /// Category headers stay compact: CJK job names keep their first two
    /// characters ("白魔法师" -> "白魔"), Latin ones use the game's own
    /// abbreviation ("PLD", "WHM", ...).
    /// </summary>
    private static string ShortenJobName(string name, string abbreviation)
    {
        if (string.IsNullOrEmpty(name)) return abbreviation;

        if (TryTakeTwoCjk(name, out string? shortened))
            return shortened;

        return string.IsNullOrWhiteSpace(abbreviation) ? name : abbreviation;
    }

    private static bool TryTakeTwoCjk(string text, out string shortened)
    {
        shortened = text;

        if (text.Length < 2) return false;
        if (!IsCjk(text[0]) || !IsCjk(text[1])) return false;

        shortened = text[..2];
        return true;
    }

    // Covers CJK ideographs as well as kana; Latin names fall below this range.
    private static bool IsCjk(char value) => value >= '\u2E80';

    private static string GetRawCategoryName(uint categoryId)
    {
        if (CategoryNameCache.TryGetValue(categoryId, out string? cached)) return cached;

        string name = string.Empty;
        try
        {
            var sheet = Services.DataManager.GetExcelSheet<ClassJobCategory>();
            if (sheet.TryGetRow(categoryId, out var row))
                name = row.Name.ToString();
        }
        catch
        {
            // Class job category sheet unavailable, fall through to the generic label.
        }

        if (string.IsNullOrWhiteSpace(name))
            name = Localization.Get("ArmouryGroup.None");
        else if (TryTakeTwoCjk(name, out string? shortened))
            name = shortened;

        CategoryNameCache[categoryId] = name;
        return name;
    }
}
