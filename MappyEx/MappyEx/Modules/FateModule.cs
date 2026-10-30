using System;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using Lumina.Extensions;
using Mappy.Classes;
using Mappy.Extensions;

namespace Mappy.Modules;

public class FateModule : ModuleBase {
    public override unsafe bool ProcessMarker(MarkerInfo markerInfo) {
        if (markerInfo.MarkerType is not MarkerType.Fate) return false;

        var fateData = FateManager.Instance()->Fates.FirstOrNull(fate => fate.Value->FateId == markerInfo.DataId);
        if (fateData is null) return false;

        var fate = fateData.Value;
        var timeRemaining = fate.GetTimeRemaining();

        var levelKind = Localization.Get("Marker.LevelPrefix", "Lv. ");
        var level = fate.Value->MaxLevel == 255 ? Localization.Get("Marker.UnknownLevel", "??") : fate.Value->Level.ToString();
        if (fate.Value->EurekaFate == 3) {
            levelKind = "\uE037";
        }

        markerInfo.PrimaryText = () => $"{levelKind}{level} {fate.Value->Name}";

        // Don't show additional information for any fate that is preparing
        if (fate.Value->State is FateState.Preparing) return true;

        if (timeRemaining >= TimeSpan.Zero) {
            markerInfo.SecondaryText = () => Localization.F("Marker.TimeRemainingProgress",
                $"{timeRemaining:mm\\:ss}", fate.Value->Progress);

            if (timeRemaining.TotalSeconds <= 300) {
                markerInfo.RadiusColor = fate.GetColor();
                markerInfo.RadiusOutlineColor = fate.GetColor();
            }
        }
        else {
            markerInfo.SecondaryText = () => Localization.F("Marker.Progress", fate.Value->Progress);
        }

        return true;
    }
}