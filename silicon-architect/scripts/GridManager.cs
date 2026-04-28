using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Owns the board state, simulation loop, economy, and HUD wiring for the city-builder prototype.
/// </summary>
public partial class GridManager : GridContainer
{
    private enum ObjectiveType
    {
        BuildHomes,
        PerformMerges,
        EarnCash,
    }

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
    [Export] public float StartingCash = 60.0f;
    [Export] public float StartingHomeCost = 20.0f;
    [Export] public int OpeningFlatCostBuildCount = 3;
    [Export] public float OpeningHomeCostStep = 5.0f;
    [Export] public float HomeCostMultiplier = 1.15f;
    [Export] public float StartingParkCost = 45.0f;
    [Export] public float ParkCostMultiplier = 1.2f;
    [Export] public float StartingShopCost = 55.0f;
    [Export] public float ShopCostMultiplier = 1.2f;
    [Export] public float ShopHeatGenerationPerSecond = 7.0f;
    [Export] public float ShopPassiveCoolingPerSecond = 1.5f;
    [Export] public float ShopDataOutputPerSecond = 0.4f;
    [Export] public float ShopPowerDraw = 12.0f;
    [Export] public float AmenitiesCashMultiplier = 1.25f;
    [Export] public float AmenitiesHeatMultiplier = 0.75f;
    [Export] public float HighRiseParkAdjacencyIncomeMultiplier = 1.2f;
    [Export] public int ShopServiceRadius = 2;
    [Export] public float IncomeBoostUnlockCashThreshold = 180.0f;
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
    private float _currentHomeCost;
    private float _currentParkCost;
    private float _currentShopCost;
    private int _homesBuilt;
    private int _parksBuilt;
    private int _shopsBuilt;
    private bool _isPlacementMode;
    private bool _isParkPlacementMode;
    private bool _isShopPlacementMode;
    private bool _parksUnlocked;
    private bool _incomeBoostUnlocked;
    private bool _eventsUnlocked;
    private string _spawnStatus = CityTerminology.InitialBuildPrompt;
    private float _incomeMultiplier = 1.0f;
    private float _rewardedIncomeTimeRemaining;
    private MotherboardTile _shopPreviewAnchorTile;
    private MotherboardTile _selectedMergeTile;
    private MotherboardTile _mergeHoverTile;
    private MotherboardTile _selectedTouchTile;
    private ObjectiveType _activeObjectiveType;
    private int _activeObjectiveTarget;
    private int _activeObjectiveProgress;
    private float _activeObjectiveRewardCash;
    private int _objectiveStage;
    private readonly List<MotherboardTile> _tiles = new();
    private readonly Dictionary<Vector2I, MotherboardTile> _tilesByPosition = new();

    public override void _Ready()
    {
        Columns = GridSize;
        _totalCoins = StartingCash;
        _currentHomeCost = StartingHomeCost;
        _currentParkCost = StartingParkCost;
        _currentShopCost = StartingShopCost;
        _homesBuilt = 0;
        _parksBuilt = 0;
        _shopsBuilt = 0;
        _parksUnlocked = false;
        _incomeBoostUnlocked = false;
        _eventsUnlocked = false;
        InitializeObjectives();

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

        if (_parkButton != null)
        {
            _parkButton.Pressed -= OnParkButtonPressed;
        }

        if (_shopButton != null)
        {
            _shopButton.Pressed -= OnShopButtonPressed;
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

        UpdateObjectiveHudFlash((float)delta);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_gridGenerated)
        {
            return;
        }

        if (_isShopPlacementMode)
        {
            if (@event is InputEventMouseMotion shopMouseMotion)
            {
                UpdateShopPlacementPreview(shopMouseMotion.GlobalPosition);
                return;
            }

            if (@event is InputEventScreenDrag shopScreenDrag)
            {
                UpdateShopPlacementPreview(shopScreenDrag.Position);
                return;
            }
        }

        if (_selectedMergeTile == null)
        {
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            UpdateMergeDragHover(mouseMotion.GlobalPosition);
            return;
        }

        if (@event is InputEventScreenDrag screenDrag)
        {
            UpdateMergeDragHover(screenDrag.Position);
            return;
        }

        Vector2? releasePosition = null;

        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
        {
            releasePosition = mb.GlobalPosition;
        }
        else if (@event is InputEventScreenTouch touch && !touch.Pressed)
        {
            releasePosition = touch.Position;
        }

        if (releasePosition.HasValue)
        {
            MotherboardTile target = GetTileAtGlobalPosition(releasePosition.Value);
            TryCompleteMergeDrag(target);
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
            case MotherboardTile.TileRole.Shop:
                tile.Configure(coordinates, role, 1, ShopHeatGenerationPerSecond, ShopPassiveCoolingPerSecond, ShopDataOutputPerSecond, ShopPowerDraw, 0.0f);
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

        return MotherboardTile.TileRole.Empty;
    }

    private void AdvanceHomeCost()
    {
        _homesBuilt += 1;

        if (_homesBuilt < OpeningFlatCostBuildCount)
        {
            _currentHomeCost += OpeningHomeCostStep;
            return;
        }

        _currentHomeCost = Mathf.Round(_currentHomeCost * HomeCostMultiplier);
    }

    private void AdvanceParkCost()
    {
        _parksBuilt += 1;
        _currentParkCost = Mathf.Round(_currentParkCost * ParkCostMultiplier);
    }

    private void AdvanceShopCost()
    {
        _shopsBuilt += 1;
        _currentShopCost = Mathf.Round(_currentShopCost * ShopCostMultiplier);
    }

    private void SetPlacementMode(bool homeMode, bool parkMode, bool shopMode)
    {
        _isPlacementMode = homeMode;
        _isParkPlacementMode = parkMode;
        _isShopPlacementMode = shopMode;
        if (!shopMode)
        {
            ClearShopPlacementPreview();
        }
        UpdatePlacementHighlights();
    }

    private void CancelPlacementMode(string statusMessage)
    {
        SetPlacementMode(false, false, false);
        _spawnStatus = statusMessage;
    }

    private void TryUnlockIncomeBoostFromCash()
    {
        if (_incomeBoostUnlocked || _totalCoins < IncomeBoostUnlockCashThreshold)
        {
            return;
        }

        _incomeBoostUnlocked = true;
        _spawnStatus = CityTerminology.IncomeBoostUnlockedPrompt;
    }

    private void HandleMergeMilestones(MotherboardTile.TileRole mergedRole)
    {
        if (mergedRole == MotherboardTile.TileRole.LogicGate && !_parksUnlocked)
        {
            _parksUnlocked = true;
            _spawnStatus = CityTerminology.ParksUnlockedPrompt;
        }

        if (mergedRole == MotherboardTile.TileRole.Processor && !_eventsUnlocked)
        {
            _eventsUnlocked = true;
            _spawnStatus = CityTerminology.EventsUnlockedPrompt;
        }
    }

    private void InitializeObjectives()
    {
        _objectiveStage = 0;
        SetObjectiveFromStage(_objectiveStage);
    }

    private void SetObjectiveFromStage(int stage)
    {
        int cycle = stage % 3;
        int tier = stage / 3;

        switch (cycle)
        {
            case 0:
                _activeObjectiveType = ObjectiveType.BuildHomes;
                _activeObjectiveTarget = 2 + tier;
                _activeObjectiveRewardCash = 55.0f + (tier * 18.0f);
                break;
            case 1:
                _activeObjectiveType = ObjectiveType.PerformMerges;
                _activeObjectiveTarget = 2 + tier;
                _activeObjectiveRewardCash = 85.0f + (tier * 22.0f);
                break;
            default:
                _activeObjectiveType = ObjectiveType.EarnCash;
                _activeObjectiveTarget = 90 + (tier * 45);
                _activeObjectiveRewardCash = 110.0f + (tier * 28.0f);
                break;
        }

        _activeObjectiveProgress = 0;
    }

    private void AdvanceObjective()
    {
        _objectiveStage += 1;
        SetObjectiveFromStage(_objectiveStage);
    }

    private void AddObjectiveProgress(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _activeObjectiveProgress = Mathf.Min(_activeObjectiveProgress + amount, _activeObjectiveTarget);

        if (_activeObjectiveProgress < _activeObjectiveTarget)
        {
            return;
        }

        _totalCoins += _activeObjectiveRewardCash;
        float rewardCash = _activeObjectiveRewardCash;
        string completedObjectiveName = GetObjectiveName(_activeObjectiveType);
        AdvanceObjective();
        ShowObjectiveRewardPopup(rewardCash);
        StartObjectiveHudFlash();
        _spawnStatus = CityTerminology.FormatObjectiveCompleted(completedObjectiveName, rewardCash);
    }

    private void AddObjectiveCashProgress(float amount)
    {
        if (_activeObjectiveType != ObjectiveType.EarnCash || amount <= 0.0f)
        {
            return;
        }

        AddObjectiveProgress(Mathf.RoundToInt(amount));
    }

    private string GetObjectiveName(ObjectiveType objective)
    {
        return objective switch
        {
            ObjectiveType.BuildHomes => "Build Homes",
            ObjectiveType.PerformMerges => "Complete Merges",
            _ => "Earn Cash",
        };
    }

    private string GetObjectiveDescription(ObjectiveType objective)
    {
        return objective switch
        {
            ObjectiveType.BuildHomes => "Build Homes",
            ObjectiveType.PerformMerges => "Merge matching districts",
            _ => "Generate Cash",
        };
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
            bool hasShopAmenities =
                (tile.Role == MotherboardTile.TileRole.Transistor || tile.Role == MotherboardTile.TileRole.LogicGate) &&
                HasRoleWithinRadius(tile, MotherboardTile.TileRole.Shop, ShopServiceRadius);
            float neighborPollutionAverage = GetNeighborPollutionAverage(tile);
            float thermalBleed = (neighborPollutionAverage - tile.CurrentHeat) * ThermalBleedFactor;
            float heatMultiplier = hasShopAmenities ? AmenitiesHeatMultiplier : 1.0f;
            float generatedHeat = tile.HeatGenerationPerSecond * tile.Efficiency * suppliedPowerRatio * heatMultiplier;
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

            if (tile.Role == MotherboardTile.TileRole.Processor || tile.Role == MotherboardTile.TileRole.LogicGate || tile.Role == MotherboardTile.TileRole.Transistor || tile.Role == MotherboardTile.TileRole.Shop)
            {
                incomeDistrictCount += 1;
                if (tile.Efficiency < 0.999f)
                {
                    unhappyDistrictCount += 1;
                }
            }

            if (tile.DataOutputPerSecond > 0.0f)
            {
                float incomeMultiplier = 1.0f;
                if ((tile.Role == MotherboardTile.TileRole.Transistor || tile.Role == MotherboardTile.TileRole.LogicGate) && HasRoleWithinRadius(tile, MotherboardTile.TileRole.Shop, ShopServiceRadius))
                {
                    incomeMultiplier *= AmenitiesCashMultiplier;
                }

                if (tile.Role == MotherboardTile.TileRole.Processor && IsAdjacentToRole(tile, MotherboardTile.TileRole.Fan))
                {
                    incomeMultiplier *= HighRiseParkAdjacencyIncomeMultiplier;
                }

                float tileIncome = tile.DataOutputPerSecond * tile.Efficiency * suppliedPowerRatio * deltaSeconds * IncomePerDataUnit * _incomeMultiplier * incomeMultiplier;
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
        AddObjectiveCashProgress(coinsGeneratedThisTick);
        TryUnlockIncomeBoostFromCash();
        RefreshShopCoverageHighlights();

        if (_simulationHudLabel != null)
        {
            _simulationHudLabel.Text =
                "Micro-City Prototype\n" +
                $"{CityTerminology.PollutionLabel}: {hottestTile:0.0}%\n" +
                $"Power Draw: {totalPowerDemand:0.0}W / {totalPowerCapacity:0.0}W ({suppliedPowerRatio:P0})\n" +
                $"{CityTerminology.CashLabel}: {_totalCoins:0.0} (+{_lastCoinsGenerated:0.0}/tick)\n" +
                CityTerminology.FormatHudStatus(unhappyDistrictCount, incomeDistrictCount, parkCount) +
                $"Bleed: 4-way=1.0, diagonal={DiagonalBleedWeight:0.00}\n" +
                CityTerminology.FormatBuildCost(_currentHomeCost) + "\n" +
                _spawnStatus;
        }

        UpdateObjectiveReadout(GetObjectiveDescription(_activeObjectiveType), _activeObjectiveProgress, _activeObjectiveTarget, _activeObjectiveRewardCash);

        UpdateSpawnButtonState();
        UpdateDoubleIncomeButtonState();

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

    private void RefreshShopCoverageHighlights()
    {
        foreach (MotherboardTile tile in _tiles)
        {
            bool hasCoverage =
                (tile.Role == MotherboardTile.TileRole.Transistor || tile.Role == MotherboardTile.TileRole.LogicGate) &&
                HasRoleWithinRadius(tile, MotherboardTile.TileRole.Shop, ShopServiceRadius);
            tile.SetShopCoverageHighlight(hasCoverage);
        }
    }

    private void UpdateShopPlacementPreview(Vector2 pointerPosition)
    {
        MotherboardTile hoveredTile = GetTileAtGlobalPosition(pointerPosition);
        if (hoveredTile == _shopPreviewAnchorTile)
        {
            return;
        }

        ClearShopPlacementPreview();

        if (hoveredTile == null || hoveredTile.Role != MotherboardTile.TileRole.Empty)
        {
            return;
        }

        _shopPreviewAnchorTile = hoveredTile;
        foreach (MotherboardTile tile in _tiles)
        {
            bool receivesAmenities =
                (tile.Role == MotherboardTile.TileRole.Transistor || tile.Role == MotherboardTile.TileRole.LogicGate) &&
                IsWithinServiceRadius(tile.GridPosition, _shopPreviewAnchorTile.GridPosition, ShopServiceRadius);
            tile.SetShopPreviewHighlight(receivesAmenities);
        }
    }

    private static bool IsWithinServiceRadius(Vector2I a, Vector2I b, int radius)
    {
        return Mathf.Abs(a.X - b.X) <= radius && Mathf.Abs(a.Y - b.Y) <= radius;
    }

    private void ClearShopPlacementPreview()
    {
        _shopPreviewAnchorTile = null;
        foreach (MotherboardTile tile in _tiles)
        {
            tile.SetShopPreviewHighlight(false);
        }
    }

    private bool HasRoleWithinRadius(MotherboardTile centerTile, MotherboardTile.TileRole role, int radius)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                Vector2I candidate = centerTile.GridPosition + new Vector2I(x, y);
                if (_tilesByPosition.TryGetValue(candidate, out MotherboardTile neighbor) && neighbor.Role == role)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsAdjacentToRole(MotherboardTile centerTile, MotherboardTile.TileRole role)
    {
        foreach (Vector2I offset in OrthogonalNeighborOffsets)
        {
            Vector2I candidate = centerTile.GridPosition + offset;
            if (_tilesByPosition.TryGetValue(candidate, out MotherboardTile neighbor) && neighbor.Role == role)
            {
                return true;
            }
        }

        return false;
    }

    private void OnBuildButtonPressed()
    {
        ClearMergeSelection();

        if (_isPlacementMode)
        {
            CancelPlacementMode(CityTerminology.PlacementCanceledPrompt);
            return;
        }

        if (_isParkPlacementMode || _isShopPlacementMode)
        {
            SetPlacementMode(false, false, false);
        }

        if (_totalCoins < _currentHomeCost)
        {
            _spawnStatus = CityTerminology.FormatNeedMoreCash(_currentHomeCost - _totalCoins);
            return;
        }

        if (!HasEmptySocket())
        {
            _spawnStatus = CityTerminology.NoEmptyLotsPrompt;
            return;
        }

        SetPlacementMode(true, false, false);
        _spawnStatus = CityTerminology.PlacementModePrompt;
    }

    private void OnParkButtonPressed()
    {
        ClearMergeSelection();

        if (!_parksUnlocked)
        {
            _spawnStatus = CityTerminology.ParksLockedPrompt;
            return;
        }

        if (_isParkPlacementMode)
        {
            CancelPlacementMode(CityTerminology.PlacementCanceledPrompt);
            return;
        }

        if (_isPlacementMode)
        {
            SetPlacementMode(false, false, false);
        }

        if (_isShopPlacementMode)
        {
            SetPlacementMode(false, false, false);
        }

        if (_totalCoins < _currentParkCost)
        {
            _spawnStatus = CityTerminology.FormatNeedMoreCash(_currentParkCost - _totalCoins);
            return;
        }

        if (!HasEmptySocket())
        {
            _spawnStatus = CityTerminology.NoEmptyLotsPrompt;
            return;
        }

        SetPlacementMode(false, true, false);
        _spawnStatus = CityTerminology.ParkPlacementModePrompt;
    }

    private void OnShopButtonPressed()
    {
        ClearMergeSelection();

        if (_isShopPlacementMode)
        {
            CancelPlacementMode(CityTerminology.PlacementCanceledPrompt);
            return;
        }

        if (_isPlacementMode || _isParkPlacementMode)
        {
            SetPlacementMode(false, false, false);
        }

        if (_totalCoins < _currentShopCost)
        {
            _spawnStatus = CityTerminology.FormatNeedMoreCash(_currentShopCost - _totalCoins);
            return;
        }

        if (!HasEmptySocket())
        {
            _spawnStatus = CityTerminology.NoEmptyLotsPrompt;
            return;
        }

        SetPlacementMode(false, false, true);
        _spawnStatus = CityTerminology.ShopPlacementModePrompt;
    }

    private void OnTileTapped(MotherboardTile tile)
    {
        SetTouchSelection(tile);

        // One-off quick build: clicking an empty lot places immediately even when build mode is off.
        // Build mode still exists for continuous placement until canceled.
        if (tile.Role == MotherboardTile.TileRole.Empty)
        {
            if (_isShopPlacementMode)
            {
                TryPlaceShop(tile);
                return;
            }

            if (_isParkPlacementMode)
            {
                TryPlacePark(tile);
                return;
            }

            TryPlaceHome(tile);
            return;
        }

        if (_isPlacementMode || _isParkPlacementMode || _isShopPlacementMode)
        {
            // Tapping an occupied tile while building exits build mode cleanly.
            // Does NOT fall through to merge — the press here is a mode switch, not a drag start.
            CancelPlacementMode(CityTerminology.PlacementCanceledPrompt);
            return;
        }

        StartMergeDrag(tile);
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

        if (_totalCoins < _currentHomeCost)
        {
            _spawnStatus = CityTerminology.FormatNeedMoreCash(_currentHomeCost - _totalCoins);
            SetPlacementMode(false, false, false);
            return;
        }

        _totalCoins -= _currentHomeCost;
        AdvanceHomeCost();
        if (_activeObjectiveType == ObjectiveType.BuildHomes)
        {
            AddObjectiveProgress(1);
        }
        ApplyRoleConfiguration(tile, tile.GridPosition, MotherboardTile.TileRole.Transistor);
        tile.PlayPlacementJuice();

        if (_totalCoins >= _currentHomeCost && HasEmptySocket())
        {
            SetPlacementMode(true, false, false);
            _spawnStatus = $"{CityTerminology.FormatBuiltMessage(tile.Name)}. {CityTerminology.PlacementModePrompt}";
        }
        else if (!HasEmptySocket())
        {
            SetPlacementMode(false, false, false);
            _spawnStatus = $"{CityTerminology.FormatBuiltMessage(tile.Name)}. {CityTerminology.NoEmptyLotsPrompt}";
        }
        else
        {
            SetPlacementMode(false, false, false);
            _spawnStatus = $"{CityTerminology.FormatBuiltMessage(tile.Name)}. {CityTerminology.FormatNeedMoreCash(_currentHomeCost - _totalCoins)}";
        }

        UpdatePlacementHighlights();
    }

    private void TryPlacePark(MotherboardTile tile)
    {
        if (tile.Role != MotherboardTile.TileRole.Empty)
        {
            _spawnStatus = CityTerminology.PickEmptyLotPrompt;
            return;
        }

        if (_totalCoins < _currentParkCost)
        {
            _spawnStatus = CityTerminology.FormatNeedMoreCash(_currentParkCost - _totalCoins);
            SetPlacementMode(false, false, false);
            return;
        }

        _totalCoins -= _currentParkCost;
        AdvanceParkCost();
        ApplyRoleConfiguration(tile, tile.GridPosition, MotherboardTile.TileRole.Fan);
        tile.PlayPlacementJuice();

        if (_totalCoins >= _currentParkCost && HasEmptySocket())
        {
            SetPlacementMode(false, true, false);
            _spawnStatus = $"{CityTerminology.FormatBuiltParkMessage(tile.Name)}. {CityTerminology.ParkPlacementModePrompt}";
        }
        else if (!HasEmptySocket())
        {
            SetPlacementMode(false, false, false);
            _spawnStatus = $"{CityTerminology.FormatBuiltParkMessage(tile.Name)}. {CityTerminology.NoEmptyLotsPrompt}";
        }
        else
        {
            SetPlacementMode(false, false, false);
            _spawnStatus = $"{CityTerminology.FormatBuiltParkMessage(tile.Name)}. {CityTerminology.FormatNeedMoreCash(_currentParkCost - _totalCoins)}";
        }

        UpdatePlacementHighlights();
    }

    private void TryPlaceShop(MotherboardTile tile)
    {
        if (tile.Role != MotherboardTile.TileRole.Empty)
        {
            _spawnStatus = CityTerminology.PickEmptyLotPrompt;
            return;
        }

        if (_totalCoins < _currentShopCost)
        {
            _spawnStatus = CityTerminology.FormatNeedMoreCash(_currentShopCost - _totalCoins);
            SetPlacementMode(false, false, false);
            return;
        }

        _totalCoins -= _currentShopCost;
        AdvanceShopCost();
        ClearShopPlacementPreview();
        ApplyRoleConfiguration(tile, tile.GridPosition, MotherboardTile.TileRole.Shop);
        tile.PlayPlacementJuice();

        if (_totalCoins >= _currentShopCost && HasEmptySocket())
        {
            SetPlacementMode(false, false, true);
            _spawnStatus = $"{CityTerminology.FormatBuiltShopMessage(tile.Name)}. {CityTerminology.ShopPlacementModePrompt}";
        }
        else if (!HasEmptySocket())
        {
            SetPlacementMode(false, false, false);
            _spawnStatus = $"{CityTerminology.FormatBuiltShopMessage(tile.Name)}. {CityTerminology.NoEmptyLotsPrompt}";
        }
        else
        {
            SetPlacementMode(false, false, false);
            _spawnStatus = $"{CityTerminology.FormatBuiltShopMessage(tile.Name)}. {CityTerminology.FormatNeedMoreCash(_currentShopCost - _totalCoins)}";
        }

        UpdatePlacementHighlights();
    }

    /// <summary>
    /// Begins a merge drag: highlights the pressed tile as the source.
    /// Merge completes in _Input on release over a valid target.
    /// </summary>
    private void StartMergeDrag(MotherboardTile tile)
    {
        if (!IsMergeable(tile.Role))
        {
            ClearMergeSelection();
            _spawnStatus = CityTerminology.MergeStartPrompt;
            return;
        }

        ClearMergeSelection();
        _selectedMergeTile = tile;
        _selectedMergeTile.SetMergeSelection(true);
        _spawnStatus = CityTerminology.FormatMergeSelected(tile.Role, tile.Name);
    }

    private void UpdateMergeDragHover(Vector2 pointerPosition)
    {
        MotherboardTile hoveredTile = GetTileAtGlobalPosition(pointerPosition);
        bool isValid = IsValidMergeTarget(hoveredTile);

        if (_mergeHoverTile != null && _mergeHoverTile != hoveredTile)
        {
            _mergeHoverTile.SetMergeTargetHighlight(false, false);
            _mergeHoverTile = null;
        }

        if (hoveredTile == null || hoveredTile == _selectedMergeTile)
        {
            return;
        }

        _mergeHoverTile = hoveredTile;
        _mergeHoverTile.SetMergeTargetHighlight(true, isValid);
    }

    private bool IsValidMergeTarget(MotherboardTile target)
    {
        if (_selectedMergeTile == null || target == null || target == _selectedMergeTile)
        {
            return false;
        }

        if (_selectedMergeTile.Role != target.Role)
        {
            return false;
        }

        return TryGetMergedRole(target.Role, out _);
    }

    /// <summary>
    /// Completes a merge drag on release. Called from _Input with the tile under the release position.
    /// </summary>
    private void TryCompleteMergeDrag(MotherboardTile target)
    {
        if (target == null || target == _selectedMergeTile)
        {
            ClearMergeSelection();
            _spawnStatus = CityTerminology.MergeStartPrompt;
            return;
        }

        if (_selectedMergeTile.Role != target.Role)
        {
            _spawnStatus = CityTerminology.MergeRequiresMatchPrompt;
            ClearMergeSelection();
            return;
        }

        if (!TryGetMergedRole(target.Role, out MotherboardTile.TileRole mergedRole))
        {
            _spawnStatus = CityTerminology.FormatCannotMergeFurther(target.Role);
            ClearMergeSelection();
            return;
        }

        float mergedHeat = (_selectedMergeTile.CurrentHeat + target.CurrentHeat) * 0.5f;
        ApplyRoleConfiguration(target, target.GridPosition, mergedRole);
        target.SetHeat(mergedHeat);
        target.PlayMergePulse();
        ApplyRoleConfiguration(_selectedMergeTile, _selectedMergeTile.GridPosition, MotherboardTile.TileRole.Empty);
        HandleMergeMilestones(mergedRole);
        if (_activeObjectiveType == ObjectiveType.PerformMerges)
        {
            AddObjectiveProgress(1);
        }
        _spawnStatus = CityTerminology.FormatMergedInto(mergedRole, target.Name);
        ClearMergeSelection();
    }

    private MotherboardTile GetTileAtGlobalPosition(Vector2 globalPosition)
    {
        foreach (MotherboardTile tile in _tiles)
        {
            if (tile.GetGlobalRect().HasPoint(globalPosition))
            {
                return tile;
            }
        }

        return null;
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
        if (_mergeHoverTile != null)
        {
            _mergeHoverTile.SetMergeTargetHighlight(false, false);
            _mergeHoverTile = null;
        }

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
            bool shouldHighlight = (_isPlacementMode || _isParkPlacementMode || _isShopPlacementMode) && tile.Role == MotherboardTile.TileRole.Empty;
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