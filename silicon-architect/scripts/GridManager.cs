using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Owns the board state, simulation loop, economy, and HUD wiring for the city-builder prototype.
/// </summary>
public partial class GridManager : GridContainer
{
    private static readonly Vector2I[] OrthogonalNeighborOffsets =
    {
        Vector2I.Left,
        Vector2I.Right,
        Vector2I.Up,
        Vector2I.Down,
    };

    private static readonly Vector2I[] DiagonalNeighborOffsets =
    {
        new Vector2I(-1, -1),
        new Vector2I(1, -1),
        new Vector2I(-1, 1),
        new Vector2I(1, 1),
    };

    [Export] public PackedScene TileScene;
    [Export] public int GridSize = 5;
    [Export] public float BoardWidthRatio = 1.0f;
    [Export] public float BoardHeightRatio = 1.0f;
    [Export] public int MaxTileSize = 256;
    [Export] public int PreferredMinTileSize = 96;
    [Export] public float SimulationTickSeconds = 0.25f;
    [Export] public float AmbientCoolingPerSecond = 1.5f;
    [Export] public float ThermalBleedFactor = 0.45f;
    [Export] public float DiagonalBleedWeight = 0.7f;
    [Export] public float ProcessorHeatGenerationPerSecond = 16.0f;
    [Export] public float ProcessorPassiveCoolingPerSecond = 2.5f;
    [Export] public float ProcessorDataOutputPerSecond = 5.0f;
    [Export] public float ProcessorPowerDraw = 35.0f;
    [Export] public float LogicGateHeatGenerationPerSecond = 10.0f;
    [Export] public float LogicGatePassiveCoolingPerSecond = 2.0f;
    [Export] public float LogicGateDataOutputPerSecond = 2.5f;
    [Export] public float LogicGatePowerDraw = 18.0f;
    [Export] public float TransistorHeatGenerationPerSecond = 4.0f;
    [Export] public float TransistorPassiveCoolingPerSecond = 1.8f;
    [Export] public float TransistorDataOutputPerSecond = 0.25f;
    [Export] public float TransistorPowerDraw = 9.0f;
    [Export] public float TransistorSpawnCost = 25.0f;
    [Export] public float FanHeatGenerationPerSecond = 0.0f;
    [Export] public float FanPassiveCoolingPerSecond = 10.0f;
    [Export] public float FanPowerDraw = 8.0f;
    [Export] public float PowerRailHeatGenerationPerSecond = 4.0f;
    [Export] public float PowerRailPassiveCoolingPerSecond = 3.5f;
    [Export] public float PowerRailCapacity = 180.0f;
    [Export] public float IncomePerDataUnit = 18.0f;
    [Export] public float RewardedIncomeMultiplier = 2.0f;
    [Export] public float RewardedIncomeDurationSeconds = 60.0f;
    [Export] public bool DevInstantRewardedAd = true;

    private bool _gridGenerated;
    private float _simulationAccumulator;
    private float _telemetryAccumulator;
    private float _totalCoins;
    private float _lastCoinsGenerated;
    private bool _isPlacementMode;
    private string _spawnStatus = CityTerminology.InitialBuildPrompt;
    private float _incomeMultiplier = 1.0f;
    private float _rewardedIncomeTimeRemaining;
    private MotherboardTile _selectedMergeTile;
    private MotherboardTile _selectedTouchTile;
    private readonly List<MotherboardTile> _tiles = new();
    private readonly Dictionary<Vector2I, MotherboardTile> _tilesByPosition = new();

    public override void _Ready()
    {
        Columns = GridSize;

        Control parent = GetParentControl();
        if (parent != null)
        {
            parent.Resized += UpdateBoardLayout;
        }

        GenerateGrid();
        UpdateBoardLayout();
        CallDeferred(nameof(EnsureSimulationHud));
    }

    public override void _ExitTree()
    {
        Control parent = GetParentControl();
        if (parent != null)
        {
            parent.Resized -= UpdateBoardLayout;
        }

        foreach (MotherboardTile tile in _tiles)
        {
            tile.TileTapped -= OnTileTapped;
        }

        ClearMergeSelection();

        if (_spawnButton != null)
        {
            _spawnButton.Pressed -= OnBuildButtonPressed;
        }

        if (_doubleIncomeButton != null)
        {
            _doubleIncomeButton.Pressed -= OnIncomeBoostButtonPressed;
        }
    }

    public override void _Process(double delta)
    {
        if (!_gridGenerated)
        {
            return;
        }

        if (Input.IsActionJustPressed("ui_accept"))
        {
            OnBuildButtonPressed();
        }

        _simulationAccumulator += (float)delta;
        if (_rewardedIncomeTimeRemaining > 0.0f)
        {
            _rewardedIncomeTimeRemaining = Mathf.Max(0.0f, _rewardedIncomeTimeRemaining - (float)delta);
            if (_rewardedIncomeTimeRemaining <= 0.0f)
            {
                _incomeMultiplier = 1.0f;
                _spawnStatus = CityTerminology.IncomeBoostExpired;
            }
            UpdateDoubleIncomeButtonState();
        }

        while (_simulationAccumulator >= SimulationTickSeconds)
        {
            StepCitySimulation(SimulationTickSeconds);
            _simulationAccumulator -= SimulationTickSeconds;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            UpdateBoardLayout();
        }
    }

    /// <summary>
    /// Instantiates the tile scene once and seeds the opening board layout.
    /// </summary>
    private void GenerateGrid()
    {
        if (_gridGenerated || TileScene == null)
        {
            return;
        }

        _tiles.Clear();
        _tilesByPosition.Clear();

        for (int i = 0; i < GridSize * GridSize; i++)
        {
            var tile = TileScene.Instantiate<MotherboardTile>();
            AddChild(tile);

            Vector2I coordinates = new(i % GridSize, i / GridSize);
            tile.Name = $"Tile_{coordinates.X}_{coordinates.Y}";
            ConfigureTileForPrototype(tile, coordinates);
            tile.TileTapped += OnTileTapped;
            _tiles.Add(tile);
            _tilesByPosition[coordinates] = tile;
        }

        _gridGenerated = true;
    }

    private void ConfigureTileForPrototype(MotherboardTile tile, Vector2I coordinates)
    {
        MotherboardTile.TileRole role = DeterminePrototypeRole(coordinates);
        ApplyRoleConfiguration(tile, coordinates, role);
    }

    private void ApplyRoleConfiguration(MotherboardTile tile, Vector2I coordinates, MotherboardTile.TileRole role)
    {
        switch (role)
        {
            case MotherboardTile.TileRole.PowerRail:
                tile.Configure(coordinates, role, 0, PowerRailHeatGenerationPerSecond, PowerRailPassiveCoolingPerSecond, 0.0f, 0.0f, PowerRailCapacity);
                break;
            case MotherboardTile.TileRole.Processor:
                tile.Configure(coordinates, role, 3, ProcessorHeatGenerationPerSecond, ProcessorPassiveCoolingPerSecond, ProcessorDataOutputPerSecond, ProcessorPowerDraw, 0.0f);
                break;
            case MotherboardTile.TileRole.LogicGate:
                tile.Configure(coordinates, role, 2, LogicGateHeatGenerationPerSecond, LogicGatePassiveCoolingPerSecond, LogicGateDataOutputPerSecond, LogicGatePowerDraw, 0.0f);
                break;
            case MotherboardTile.TileRole.Transistor:
                tile.Configure(coordinates, role, 1, TransistorHeatGenerationPerSecond, TransistorPassiveCoolingPerSecond, TransistorDataOutputPerSecond, TransistorPowerDraw, 0.0f);
                break;
            case MotherboardTile.TileRole.Fan:
                tile.Configure(coordinates, role, 0, FanHeatGenerationPerSecond, FanPassiveCoolingPerSecond, 0.0f, FanPowerDraw, 0.0f);
                break;
            default:
                tile.Configure(coordinates, role, 0, 0.0f, AmbientCoolingPerSecond, 0.0f, 0.0f, 0.0f);
                break;
        }

        tile.SetHeat(tile.CurrentHeat);
    }

    private MotherboardTile.TileRole DeterminePrototypeRole(Vector2I coordinates)
    {
        Vector2I center = new(GridSize / 2, GridSize / 2);
        if (coordinates == center)
        {
            return MotherboardTile.TileRole.PowerRail;
        }

        if (Mathf.Abs(coordinates.X - center.X) == 1 && Mathf.Abs(coordinates.Y - center.Y) == 1)
        {
            return MotherboardTile.TileRole.Fan;
        }

        int manhattanDistance = Mathf.Abs(coordinates.X - center.X) + Mathf.Abs(coordinates.Y - center.Y);
        return manhattanDistance == 1 ? MotherboardTile.TileRole.LogicGate : MotherboardTile.TileRole.Empty;
    }

    /// <summary>
    /// Advances one simulation tick: power, pollution, throttling, income, and HUD telemetry.
    /// </summary>
    private void StepCitySimulation(float deltaSeconds)
    {
        float totalPowerDemand = 0.0f;
        float totalPowerCapacity = 0.0f;

        // First pass computes the next pollution value from the current board snapshot so tiles
        // do not influence neighbors mid-step.
        foreach (MotherboardTile tile in _tiles)
        {
            totalPowerDemand += tile.RequestedPowerDraw;
            totalPowerCapacity += tile.PowerCapacity;
        }

        float suppliedPowerRatio = totalPowerDemand > 0.0f ? Mathf.Min(totalPowerCapacity / totalPowerDemand, 1.0f) : 1.0f;
        Dictionary<MotherboardTile, float> nextPollutionByTile = new();
        int unhappyDistrictCount = 0;
        int incomeDistrictCount = 0;
        int parkCount = 0;
        float coinsGeneratedThisTick = 0.0f;

        // Second pass applies the snapshot, updates income, and refreshes per-tile presentation.
        foreach (MotherboardTile tile in _tiles)
        {
            float neighborPollutionAverage = GetNeighborPollutionAverage(tile);
            float thermalBleed = (neighborPollutionAverage - tile.CurrentHeat) * ThermalBleedFactor;
            float generatedHeat = tile.HeatGenerationPerSecond * tile.Efficiency * suppliedPowerRatio;
            float cooling = (tile.PassiveCoolingPerSecond + AmbientCoolingPerSecond) * deltaSeconds;
            float nextPollution = tile.CurrentHeat + ((generatedHeat + thermalBleed) * deltaSeconds) - cooling;
            nextPollutionByTile[tile] = nextPollution;
        }

        float hottestTile = 0.0f;
        foreach (MotherboardTile tile in _tiles)
        {
            tile.SetPowerState(suppliedPowerRatio, totalPowerDemand, totalPowerCapacity);
            tile.SetHeat(nextPollutionByTile[tile]);
            hottestTile = Mathf.Max(hottestTile, tile.CurrentHeat);

            if (tile.Role == MotherboardTile.TileRole.Processor || tile.Role == MotherboardTile.TileRole.LogicGate || tile.Role == MotherboardTile.TileRole.Transistor)
            {
                incomeDistrictCount += 1;
                if (tile.Efficiency < 0.999f)
                {
                    unhappyDistrictCount += 1;
                }
            }

            if (tile.DataOutputPerSecond > 0.0f)
            {
                float tileIncome = tile.DataOutputPerSecond * tile.Efficiency * suppliedPowerRatio * deltaSeconds * IncomePerDataUnit * _incomeMultiplier;
                if (tileIncome > 0.0f)
                {
                    coinsGeneratedThisTick += tileIncome;
                    ShowFloatingIncome(tile, tileIncome);
                }
            }

            if (tile.Role == MotherboardTile.TileRole.Fan)
            {
                parkCount += 1;
            }
        }

        _lastCoinsGenerated = coinsGeneratedThisTick;
        _totalCoins += coinsGeneratedThisTick;

        if (_simulationHudLabel != null)
        {
            _simulationHudLabel.Text =
                "Micro-City Prototype\n" +
                $"{CityTerminology.PollutionLabel}: {hottestTile:0.0}%\n" +
                $"Power Draw: {totalPowerDemand:0.0}W / {totalPowerCapacity:0.0}W ({suppliedPowerRatio:P0})\n" +
                $"{CityTerminology.CashLabel}: {_totalCoins:0.0} (+{_lastCoinsGenerated:0.0}/tick)\n" +
                CityTerminology.FormatHudStatus(unhappyDistrictCount, incomeDistrictCount, parkCount) +
                $"Bleed: 4-way=1.0, diagonal={DiagonalBleedWeight:0.00}\n" +
                CityTerminology.FormatBuildCost(TransistorSpawnCost) + "\n" +
                _spawnStatus;
        }

            UpdateSpawnButtonState();

        _telemetryAccumulator += deltaSeconds;
        UpdateSelectedTileReadout();
        if (_telemetryAccumulator >= 1.0f)
        {
            GD.Print($"City tick | {CityTerminology.PollutionLabel} {hottestTile:0.0}% | Power {totalPowerDemand:0.0}/{totalPowerCapacity:0.0} W | Supply {suppliedPowerRatio:P0} | {CityTerminology.CashLabel} {_totalCoins:0.0}");
            _telemetryAccumulator = 0.0f;
        }
    }

    private float GetNeighborPollutionAverage(MotherboardTile tile)
    {
        float totalNeighborPollution = 0.0f;
        float totalWeight = 0.0f;

        foreach (Vector2I offset in OrthogonalNeighborOffsets)
        {
            Vector2I neighborCoordinates = tile.GridPosition + offset;
            if (!_tilesByPosition.TryGetValue(neighborCoordinates, out MotherboardTile neighbor))
            {
                continue;
            }

            totalNeighborPollution += neighbor.CurrentHeat;
            totalWeight += 1.0f;
        }

        foreach (Vector2I offset in DiagonalNeighborOffsets)
        {
            Vector2I neighborCoordinates = tile.GridPosition + offset;
            if (!_tilesByPosition.TryGetValue(neighborCoordinates, out MotherboardTile neighbor))
            {
                continue;
            }

            totalNeighborPollution += neighbor.CurrentHeat * DiagonalBleedWeight;
            totalWeight += DiagonalBleedWeight;
        }

        return totalWeight > 0.0f ? totalNeighborPollution / totalWeight : tile.CurrentHeat;
    }

    private void OnBuildButtonPressed()
    {
        ClearMergeSelection();

        if (_isPlacementMode)
        {
            _isPlacementMode = false;
            _spawnStatus = CityTerminology.PlacementCanceledPrompt;
            UpdatePlacementHighlights();
            return;
        }

        if (_totalCoins < TransistorSpawnCost)
        {
            _spawnStatus = CityTerminology.FormatNeedMoreCash(TransistorSpawnCost - _totalCoins);
            return;
        }

        if (!HasEmptySocket())
        {
            _spawnStatus = CityTerminology.NoEmptyLotsPrompt;
            return;
        }

        _isPlacementMode = true;
        _spawnStatus = CityTerminology.PlacementModePrompt;
        UpdatePlacementHighlights();
    }

    private void OnTileTapped(MotherboardTile tile)
    {
        SetTouchSelection(tile);

        if (_isPlacementMode)
        {
            TryPlaceHome(tile);
            return;
        }

        HandleMergeTap(tile);
    }

    /// <summary>
    /// Builds the base Home unit on an empty lot and applies the idle-game cost ramp.
    /// </summary>
    private void TryPlaceHome(MotherboardTile tile)
    {
        if (tile.Role != MotherboardTile.TileRole.Empty)
        {
            _spawnStatus = CityTerminology.PickEmptyLotPrompt;
            return;
        }

        if (_totalCoins < TransistorSpawnCost)
        {
            _spawnStatus = CityTerminology.FormatNeedMoreCash(TransistorSpawnCost - _totalCoins);
            _isPlacementMode = false;
            UpdatePlacementHighlights();
            return;
        }

        _totalCoins -= TransistorSpawnCost;
        TransistorSpawnCost = Mathf.Round(TransistorSpawnCost * 1.15f);
        ApplyRoleConfiguration(tile, tile.GridPosition, MotherboardTile.TileRole.Transistor);
        tile.PlayPlacementJuice();
        _spawnStatus = CityTerminology.FormatBuiltMessage(tile.Name);
        _isPlacementMode = false;
        UpdatePlacementHighlights();
    }

    /// <summary>
    /// Handles tap-to-select merge flow for upgradeable buildings.
    /// </summary>
    private void HandleMergeTap(MotherboardTile tile)
    {
        if (!IsMergeable(tile.Role))
        {
            _spawnStatus = _selectedMergeTile != null
                ? CityTerminology.MergePendingPrompt
                : CityTerminology.MergeStartPrompt;
            return;
        }

        if (_selectedMergeTile == null)
        {
            _selectedMergeTile = tile;
            _selectedMergeTile.SetMergeSelection(true);
            _spawnStatus = CityTerminology.FormatMergeSelected(tile.Role, tile.Name);
            return;
        }

        if (_selectedMergeTile == tile)
        {
            _spawnStatus = CityTerminology.MergeSelectionCleared;
            ClearMergeSelection();
            return;
        }

        if (_selectedMergeTile.Role != tile.Role)
        {
            _spawnStatus = CityTerminology.MergeRequiresMatchPrompt;
            ClearMergeSelection();
            return;
        }

        if (!TryGetMergedRole(tile.Role, out MotherboardTile.TileRole mergedRole))
        {
            _spawnStatus = CityTerminology.FormatCannotMergeFurther(tile.Role);
            ClearMergeSelection();
            return;
        }

        float mergedHeat = (_selectedMergeTile.CurrentHeat + tile.CurrentHeat) * 0.5f;
        ApplyRoleConfiguration(tile, tile.GridPosition, mergedRole);
        tile.SetHeat(mergedHeat);
        tile.PlayMergePulse();
        ApplyRoleConfiguration(_selectedMergeTile, _selectedMergeTile.GridPosition, MotherboardTile.TileRole.Empty);
        _spawnStatus = CityTerminology.FormatMergedInto(mergedRole, tile.Name);
        ClearMergeSelection();
    }

    private static bool IsMergeable(MotherboardTile.TileRole role)
    {
        return role == MotherboardTile.TileRole.Transistor || role == MotherboardTile.TileRole.LogicGate;
    }

    private static bool TryGetMergedRole(MotherboardTile.TileRole role, out MotherboardTile.TileRole mergedRole)
    {
        if (role == MotherboardTile.TileRole.Transistor)
        {
            mergedRole = MotherboardTile.TileRole.LogicGate;
            return true;
        }

        if (role == MotherboardTile.TileRole.LogicGate)
        {
            mergedRole = MotherboardTile.TileRole.Processor;
            return true;
        }

        mergedRole = MotherboardTile.TileRole.Empty;
        return false;
    }

    private void ClearMergeSelection()
    {
        if (_selectedMergeTile != null)
        {
            _selectedMergeTile.SetMergeSelection(false);
            _selectedMergeTile = null;
        }
    }

    private bool HasEmptySocket()
    {
        foreach (MotherboardTile tile in _tiles)
        {
            if (tile.Role == MotherboardTile.TileRole.Empty)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdatePlacementHighlights()
    {
        foreach (MotherboardTile tile in _tiles)
        {
            bool shouldHighlight = _isPlacementMode && tile.Role == MotherboardTile.TileRole.Empty;
            tile.SetPlacementHighlight(shouldHighlight);
        }
    }

    /// <summary>
    /// Recomputes tile sizing so the square board fits the available portrait layout space.
    /// </summary>
    private void UpdateBoardLayout()
    {
        if (!_gridGenerated || GetParentControl() == null)
        {
            return;
        }

        Vector2 viewportSize = GetParentControl().Size;
        float availableWidth = viewportSize.X * BoardWidthRatio;
        float availableHeight = viewportSize.Y * BoardHeightRatio;
        float maxTileSizeByWidth = availableWidth / GridSize;
        float maxTileSizeByHeight = availableHeight / GridSize;
        float maxFittingTileSize = Mathf.Min(Mathf.Min(maxTileSizeByWidth, maxTileSizeByHeight), MaxTileSize);
        float tileSize = Mathf.Max(1.0f, maxFittingTileSize);

        if (tileSize >= PreferredMinTileSize)
        {
            tileSize = Mathf.Max(PreferredMinTileSize, tileSize);
        }

        Vector2 tileDimensions = new(tileSize, tileSize);
        Vector2 boardSize = tileDimensions * GridSize;

        CustomMinimumSize = boardSize;
        Size = boardSize;
        if (GetParent() is not Container)
        {
            Position = new Vector2(
                Mathf.Round((viewportSize.X - boardSize.X) * 0.5f),
                Mathf.Round((viewportSize.Y - boardSize.Y) * 0.5f)
            );
        }

        foreach (Node child in GetChildren())
        {
            if (child is Control tile)
            {
                tile.CustomMinimumSize = tileDimensions;
                tile.Size = tileDimensions;
            }
        }

        QueueSort();
    }
}