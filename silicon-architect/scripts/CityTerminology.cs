using Godot;

/// <summary>
/// Centralizes player-facing wording so the game theme can evolve without forcing risky code renames.
/// </summary>
public static class CityTerminology
{
    public const string PollutionLabel = "Pollution";
    public const string CashLabel = "Cash";
    public const string IncomeLabel = "Income";
    public const string EmptyLotName = "Empty Lot";
    public const string BuildUnitName = "Home";
    public const string InspectPrompt = "Tap a tile to inspect";
    public static string InitialBuildPrompt => $"Tap Build {BuildUnitName}, then tap an empty lot";

    public static string GetRoleName(MotherboardTile.TileRole role)
    {
        return role switch
        {
            MotherboardTile.TileRole.Transistor => "Home",
            MotherboardTile.TileRole.LogicGate => "Apartment",
            MotherboardTile.TileRole.Processor => "Commercial Hub",
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
        return $"Build Cost: {buildCost:0.0} {CashLabel}";
    }

    public static string FormatBuiltMessage(string tileName)
    {
        return $"Built {BuildUnitName} on {tileName}";
    }

    public static string FormatMergeSelected(MotherboardTile.TileRole role, string tileName)
    {
        return $"Selected {GetRoleName(role)} on {tileName}; tap matching tile to merge";
    }

    public static string FormatMergedInto(MotherboardTile.TileRole role, string tileName)
    {
        return $"Merged into {GetRoleName(role)} on {tileName}";
    }
}