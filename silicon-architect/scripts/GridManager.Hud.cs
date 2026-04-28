using Godot;

public partial class GridManager
{
    private const float ObjectiveHudFlashDurationSeconds = 0.6f;
    private static readonly Color ObjectiveHudFlashColor = new("#c8ff9f");
    private Label _simulationHudLabel;
    private Label _objectiveLabel;
    private Label _selectedTileLabel;
    private Button _spawnButton;
    private Button _parkButton;
    private Button _shopButton;
    private Button _doubleIncomeButton;
    private Control _floatingTextRoot;
    private float _objectiveHudFlashRemaining;

    /// <summary>
    /// Binds scene-level HUD controls after the main scene tree is fully available.
    /// </summary>
    private void EnsureSimulationHud()
    {
        Node sceneRoot = GetTree().CurrentScene;
        if (sceneRoot == null)
        {
            return;
        }

        _simulationHudLabel = sceneRoot.FindChild("SimulationHudLabel", true, false) as Label;
        _objectiveLabel = sceneRoot.FindChild("ObjectiveLabel", true, false) as Label;
        _selectedTileLabel = sceneRoot.FindChild("SelectedTileLabel", true, false) as Label;
        _spawnButton = sceneRoot.FindChild("SpawnTransistorButton", true, false) as Button;
        _parkButton = sceneRoot.FindChild("SpawnParkButton", true, false) as Button;
        _shopButton = sceneRoot.FindChild("SpawnShopButton", true, false) as Button;
        _doubleIncomeButton = sceneRoot.FindChild("DoubleIncomeButton", true, false) as Button;

        if (_simulationHudLabel != null)
        {
            _simulationHudLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }

        if (_selectedTileLabel != null)
        {
            _selectedTileLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _selectedTileLabel.Text = CityTerminology.InspectPrompt;
        }

        if (_objectiveLabel != null)
        {
            _objectiveLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }

        if (_spawnButton != null)
        {
            var spawnCallable = Callable.From(OnBuildButtonPressed);
            if (_spawnButton.IsConnected(BaseButton.SignalName.Pressed, spawnCallable))
            {
                _spawnButton.Pressed -= OnBuildButtonPressed;
            }
            _spawnButton.Pressed += OnBuildButtonPressed;
        }

        if (_doubleIncomeButton != null)
        {
            var doubleCallable = Callable.From(OnIncomeBoostButtonPressed);
            if (_doubleIncomeButton.IsConnected(BaseButton.SignalName.Pressed, doubleCallable))
            {
                _doubleIncomeButton.Pressed -= OnIncomeBoostButtonPressed;
            }
            _doubleIncomeButton.Pressed += OnIncomeBoostButtonPressed;
            UpdateDoubleIncomeButtonState();
        }

        if (_parkButton != null)
        {
            var parkCallable = Callable.From(OnParkButtonPressed);
            if (_parkButton.IsConnected(BaseButton.SignalName.Pressed, parkCallable))
            {
                _parkButton.Pressed -= OnParkButtonPressed;
            }
            _parkButton.Pressed += OnParkButtonPressed;
        }

        if (_shopButton != null)
        {
            var shopCallable = Callable.From(OnShopButtonPressed);
            if (_shopButton.IsConnected(BaseButton.SignalName.Pressed, shopCallable))
            {
                _shopButton.Pressed -= OnShopButtonPressed;
            }
            _shopButton.Pressed += OnShopButtonPressed;
        }

        _floatingTextRoot = sceneRoot.FindChild("FloatingTextRoot", true, false) as Control;
        if (_floatingTextRoot == null)
        {
            CanvasLayer floatingLayer = sceneRoot.FindChild("FloatingTextLayer", true, false) as CanvasLayer;
            if (floatingLayer == null)
            {
                floatingLayer = new CanvasLayer { Name = "FloatingTextLayer", Layer = 50 };
                sceneRoot.AddChild(floatingLayer);
            }

            _floatingTextRoot = floatingLayer.GetNodeOrNull<Control>("FloatingTextRoot");
            if (_floatingTextRoot == null)
            {
                _floatingTextRoot = new Control { Name = "FloatingTextRoot" };
                floatingLayer.AddChild(_floatingTextRoot);
            }

            _floatingTextRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _floatingTextRoot.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        UpdateSpawnButtonState();
    }

    private void UpdateObjectiveReadout(string objectiveDescription, int progress, int target, float rewardCash)
    {
        if (_objectiveLabel == null)
        {
            return;
        }

        _objectiveLabel.Text = CityTerminology.FormatObjectiveStatus(objectiveDescription, progress, target, rewardCash);
    }

    /// <summary>
    /// Tracks the tile currently being inspected from a touch or click interaction.
    /// </summary>
    private void SetTouchSelection(MotherboardTile tile)
    {
        if (_selectedTouchTile == tile)
        {
            UpdateSelectedTileReadout();
            return;
        }

        if (_selectedTouchTile != null)
        {
            _selectedTouchTile.SetTouchSelection(false);
        }

        _selectedTouchTile = tile;
        if (_selectedTouchTile != null)
        {
            _selectedTouchTile.SetTouchSelection(true);
        }

        UpdateSelectedTileReadout();
    }

    private void UpdateSelectedTileReadout()
    {
        if (_selectedTileLabel == null)
        {
            return;
        }

        if (_selectedTouchTile == null)
        {
            _selectedTileLabel.Text = CityTerminology.InspectPrompt;
            return;
        }

        float incomePerSecond = _selectedTouchTile.DataOutputPerSecond * _selectedTouchTile.Efficiency * IncomePerDataUnit * _incomeMultiplier;
        _selectedTileLabel.Text = CityTerminology.FormatSelectedTile(_selectedTouchTile, incomePerSecond);
    }

    /// <summary>
    /// Spawns a short-lived floating cash readout near the tile that generated the income.
    /// </summary>
    private void ShowFloatingIncome(MotherboardTile tile, float amount)
    {
        if (_floatingTextRoot == null || amount < 0.5f)
        {
            return;
        }

        FloatingText text = new FloatingText();
        _floatingTextRoot.AddChild(text);

        Vector2 screenPoint = tile.GetGlobalRect().GetCenter();
        Vector2 localPoint = _floatingTextRoot.GetGlobalTransformWithCanvas().AffineInverse() * screenPoint;
        text.Position = localPoint - new Vector2(30.0f, 24.0f);

        float horizontalDrift = (float)GD.RandRange(-26.0, 26.0);
        Color incomeColor = _incomeMultiplier > 1.0f ? new Color("#00ffcc") : new Color("#ffd166");
        text.Play($"+{Mathf.RoundToInt(amount)}", incomeColor, new Vector2(horizontalDrift, -42.0f));
    }

    private void ShowObjectiveRewardPopup(float rewardCash)
    {
        if (_floatingTextRoot == null)
        {
            return;
        }

        FloatingText text = new FloatingText();
        _floatingTextRoot.AddChild(text);

        Vector2 viewportCenter = GetViewportRect().Size * 0.5f;
        Vector2 localPoint = _floatingTextRoot.GetGlobalTransformWithCanvas().AffineInverse() * viewportCenter;
        text.Position = localPoint + new Vector2(-80.0f, -180.0f);
        text.Play(CityTerminology.FormatObjectiveRewardPopup(rewardCash), new Color("#9dff8a"), new Vector2(0.0f, -56.0f));
    }

    private void StartObjectiveHudFlash()
    {
        _objectiveHudFlashRemaining = ObjectiveHudFlashDurationSeconds;
    }

    private void UpdateObjectiveHudFlash(float deltaSeconds)
    {
        if (_objectiveLabel == null)
        {
            return;
        }

        if (_objectiveHudFlashRemaining <= 0.0f)
        {
            _objectiveLabel.SelfModulate = Colors.White;
            return;
        }

        _objectiveHudFlashRemaining = Mathf.Max(0.0f, _objectiveHudFlashRemaining - deltaSeconds);
        float blend = _objectiveHudFlashRemaining / ObjectiveHudFlashDurationSeconds;
        _objectiveLabel.SelfModulate = Colors.White.Lerp(ObjectiveHudFlashColor, blend);
    }

    /// <summary>
    /// Entry point for the rewarded-ad style income boost.
    /// </summary>
    private void OnIncomeBoostButtonPressed()
    {
        if (!_incomeBoostUnlocked)
        {
            _spawnStatus = CityTerminology.IncomeBoostLockedPrompt;
            return;
        }

        if (DevInstantRewardedAd)
        {
            GrantRewardedIncome();
            return;
        }

        _spawnStatus = "TODO: wire rewarded ad callback, then call GrantRewardedIncome()";
    }

    private void GrantRewardedIncome()
    {
        _incomeMultiplier = RewardedIncomeMultiplier;
        _rewardedIncomeTimeRemaining = RewardedIncomeDurationSeconds;
        _spawnStatus = $"Double Income active for {_rewardedIncomeTimeRemaining:0}s";
        UpdateDoubleIncomeButtonState();
    }

    private void UpdateDoubleIncomeButtonState()
    {
        if (_doubleIncomeButton == null)
        {
            return;
        }

        if (_rewardedIncomeTimeRemaining > 0.0f)
        {
            _doubleIncomeButton.Text = $"2x Active ({_rewardedIncomeTimeRemaining:0}s)";
            _doubleIncomeButton.Disabled = true;
            return;
        }

        if (!_incomeBoostUnlocked)
        {
            _doubleIncomeButton.Text = "2x Income (Locked)";
            _doubleIncomeButton.Disabled = true;
            return;
        }

        _doubleIncomeButton.Text = "2x Income (Ad)";
        _doubleIncomeButton.Disabled = false;
    }

    private void UpdateSpawnButtonState()
    {
        if (_spawnButton == null)
        {
            return;
        }

        _spawnButton.Text = _isPlacementMode ? "Cancel Placement" : CityTerminology.FormatBuildButton(_currentHomeCost);

        if (_isPlacementMode)
        {
            _spawnButton.Disabled = false;
        }
        else
        {
            _spawnButton.Disabled = _totalCoins < _currentHomeCost || !HasEmptySocket();
        }

        if (_parkButton == null)
        {
            return;
        }

        if (!_parksUnlocked)
        {
            _parkButton.Text = "Build Park (Locked)";
            _parkButton.Disabled = true;
            return;
        }

        _parkButton.Text = _isParkPlacementMode ? "Cancel Park" : $"Build Park ({_currentParkCost:0})";
        if (_isParkPlacementMode)
        {
            _parkButton.Disabled = false;
            return;
        }

        _parkButton.Disabled = _totalCoins < _currentParkCost || !HasEmptySocket();

        if (_shopButton == null)
        {
            return;
        }

        _shopButton.Text = _isShopPlacementMode ? "Cancel Shop" : $"Build Shop ({_currentShopCost:0})";
        if (_isShopPlacementMode)
        {
            _shopButton.Disabled = false;
            return;
        }

        _shopButton.Disabled = _totalCoins < _currentShopCost || !HasEmptySocket();
    }
}