using Godot;

/// <summary>
/// Centralizes player-facing wording so the game theme can evolve without forcing risky code renames.
/// </summary>
public static class CityTerminology
{
    public const string PollutionLabel = "Pollution";
    public const string CashLabel = "Cash";
    public const string IncomeLabel = "Income";
    public const string BuildCostLabel = "Build Cost";
    public const string ObjectiveLabel = "Objective";
    public const string BoostLabel = "2x Income";
    public const string EmptyLotName = "Empty Lot";
    public const string BuildUnitName = "Home";
    public const string InspectPrompt = "Tap a tile to inspect";
    public static string InitialBuildPrompt => $"Tap an empty lot to build a {BuildUnitName}, or use Build mode";

    public static string IncomeBoostExpired => $"{BoostLabel} expired";
    public static string IncomeBoostLockedPrompt => "2x Income unlocks after reaching more cash";
    public static string IncomeBoostUnlockedPrompt => "2x Income unlocked";
    public static string NoEmptyLotsPrompt => "No empty lots available";
    public static string PlacementModePrompt => "Build mode: tap an empty lot";
    public static string ParkPlacementModePrompt => "Park mode: tap an empty lot";
    public static string ShopPlacementModePrompt => "Shop mode: tap an empty lot";
    public static string PickEmptyLotPrompt => "Pick an empty lot";
    public static string MergePendingPrompt => "Merge pending - drag onto a matching building to complete";
    public static string MergeStartPrompt => "Tap and drag from one building onto a matching building";
    public static string MergeSelectionCleared => "Merge selection cleared";
    public static string MergeRequiresMatchPrompt => "Merge requires matching buildings";
    public static string PlacementCanceledPrompt => "Build canceled";
    public static string ParksLockedPrompt => "Parks unlock after your first Apartment";
    public static string ParksUnlockedPrompt => "Parks unlocked";
    public static string EventsUnlockedPrompt => "Event tiles unlocked (coming soon)";

    public static string GetRoleName(MotherboardTile.TileRole role)
    {
        return role switch
        {
            MotherboardTile.TileRole.Transistor => "Home",
            MotherboardTile.TileRole.LogicGate => "Apartment",
            MotherboardTile.TileRole.Processor => "High-Rise",
            MotherboardTile.TileRole.Shop => "Shop",
            MotherboardTile.TileRole.PowerRail => "Power Grid",
            MotherboardTile.TileRole.Fan => "Park",
            _ => EmptyLotName,
        };
    }

    public static string FormatSelectedTile(MotherboardTile tile, float incomePerSecond)
    {
        return
            $"Selected: {GetRoleName(tile.Role)} ({tile.GridPosition.X},{tile.GridPosition.Y})  " +
            $"Tier {tile.ComponentTier}  " +
            $"{PollutionLabel} {tile.CurrentHeat:0}%  " +
            $"{IncomeLabel} +{incomePerSecond:0.0}/s";
    }

    public static string FormatTileTooltip(MotherboardTile tile, float suppliedPowerRatio, float totalGridDemand, float totalGridCapacity)
    {
        float deliveredPower = tile.RequestedPowerDraw * suppliedPowerRatio;
        return $"{tile.Name}\n" +
            $"District: {GetRoleName(tile.Role)}\n" +
            $"Tier: {tile.ComponentTier}\n" +
            $"{PollutionLabel}: {tile.CurrentHeat:0.0}\n" +
            $"Output: {tile.Efficiency:P0}\n" +
            $"{IncomeLabel}/s: {tile.DataOutputPerSecond * tile.Efficiency * suppliedPowerRatio:0.0}\n" +
            $"Power: {deliveredPower:0.0} / {tile.RequestedPowerDraw:0.0} W\n" +
            $"Grid Load: {totalGridDemand:0.0} / {totalGridCapacity:0.0} W";
    }

    public static string FormatBuildButton(float buildCost)
    {
        return $"Build {BuildUnitName} ({buildCost:0})";
    }

    public static string FormatNeedMoreCash(float missingCash)
    {
        return $"Need {missingCash:0.0} more {CashLabel}";
    }

    public static string FormatBuildCost(float buildCost)
    {
        return $"{BuildCostLabel}: {buildCost:0.0} {CashLabel}";
    }

    public static string FormatBuiltMessage(string tileName)
    {
        return $"Built {BuildUnitName} on {tileName}";
    }

    public static string FormatBuiltParkMessage(string tileName)
    {
        return $"Built Park on {tileName}";
    }

    public static string FormatBuiltShopMessage(string tileName)
    {
        return $"Built Shop on {tileName}";
    }

    public static string FormatMergeSelected(MotherboardTile.TileRole role, string tileName)
    {
        return $"Selected {GetRoleName(role)} on {tileName}; drag onto a matching tile to merge";
    }

    public static string FormatMergedInto(MotherboardTile.TileRole role, string tileName)
    {
        return $"Merged into {GetRoleName(role)} on {tileName}";
    }

    public static string FormatCannotMergeFurther(MotherboardTile.TileRole role)
    {
        return $"{GetRoleName(role)} cannot merge further yet";
    }

    public static string FormatHudStatus(int unhappyDistrictCount, int incomeDistrictCount, int parkCount)
    {
        return $"Unhappy Districts: {unhappyDistrictCount}/{incomeDistrictCount}\n" +
            $"Parks Active: {parkCount}\n";
    }

    public static string FormatObjectiveStatus(string objectiveDescription, int progress, int target, float rewardCash)
    {
        return $"{ObjectiveLabel}: {objectiveDescription} ({progress}/{target})  Reward +{rewardCash:0}";
    }

    public static string FormatObjectiveCompleted(string objectiveName, float rewardCash)
    {
        return $"Objective complete: {objectiveName} (+{rewardCash:0} {CashLabel})";
    }

    public static string FormatObjectiveRewardPopup(float rewardCash)
    {
        return $"Objective +{rewardCash:0} {CashLabel}";
    }
}