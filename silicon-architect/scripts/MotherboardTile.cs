using Godot;
using System;

/// <summary>
/// Visual and gameplay state for a single board tile, including selection and juice animations.
/// </summary>
public partial class MotherboardTile : Control
{
    [Signal]
    public delegate void TileTappedEventHandler(MotherboardTile tile);

    public enum TileRole
    {
        Empty,
        Transistor,
        LogicGate,
        Processor,
        PowerRail,
        Fan,
    }

    [Export] public TextureRect Background;
    [Export] public TextureRect Traces;
    [Export] public TextureRect Socket;
    [Export] public AudioStreamPlayer TapSound;
    [Export] public AudioStreamPlayer MergeSound;
    [Export] public float IdleBobStrength = 0.025f;
    [Export] public float IdleBobSpeed = 2.0f;
    [Export] public float WarmThrottleThreshold = 45.0f;
    [Export] public float HotThrottleThreshold = 75.0f;
    [Export] public float CriticalThrottleThreshold = 90.0f;

    public Vector2I GridPosition { get; private set; }
    public TileRole Role { get; private set; } = TileRole.Empty;
    public float CurrentHeat { get; private set; }
    public float Efficiency { get; private set; } = 1.0f;
    public float HeatGenerationPerSecond { get; private set; }
    public float PassiveCoolingPerSecond { get; private set; }
    public float DataOutputPerSecond { get; private set; }
    public float RequestedPowerDraw { get; private set; }
    public float PowerCapacity { get; private set; }
    public int ComponentTier { get; private set; }

    private float _suppliedPowerRatio = 1.0f;
    private float _totalGridDemand;
    private float _totalGridCapacity;
    private bool _placementHighlighted;
    private bool _mergeSelected;
    private bool _touchSelected;
    private float _idleTime;
    private bool _isPulseAnimating;
    private Tween _mergePulseTween;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Resized += UpdatePivotOffset;
        UpdatePivotOffset();
        SetProcess(true);
    }

    /// <summary>
    /// Runs the idle breathing animation on the inner socket only so tiles never overlap neighbors.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_isPulseAnimating)
        {
            return;
        }

        Scale = Vector2.One;

        if (Role == TileRole.Empty)
        {
            if (Socket != null)
            {
                Socket.Scale = Vector2.One;
            }
            return;
        }

        _idleTime += (float)delta * IdleBobSpeed;
        float wave = Mathf.Sin(_idleTime);
        float socketScale = 1.0f + (wave * IdleBobStrength);
        if (Socket != null)
        {
            Socket.Scale = new Vector2(socketScale, socketScale);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
        {
            TapSound?.Play();
            EmitSignal(SignalName.TileTapped, this);
            AcceptEvent();
        }

        if (@event is InputEventScreenTouch touch && touch.Pressed)
        {
            TapSound?.Play();
            EmitSignal(SignalName.TileTapped, this);
            AcceptEvent();
        }
    }

    /// <summary>
    /// Applies the role-specific stats pushed in by the board manager.
    /// </summary>
    public void Configure(Vector2I position, TileRole role, int componentTier, float heatGenerationPerSecond, float passiveCoolingPerSecond, float dataOutputPerSecond, float requestedPowerDraw, float powerCapacity)
    {
        GridPosition = position;
        Role = role;
        ComponentTier = componentTier;
        HeatGenerationPerSecond = heatGenerationPerSecond;
        PassiveCoolingPerSecond = passiveCoolingPerSecond;
        DataOutputPerSecond = dataOutputPerSecond;
        RequestedPowerDraw = requestedPowerDraw;
        PowerCapacity = powerCapacity;
        UpdateVisualState();
        UpdateTooltip();
    }

    public void SetPowerState(float suppliedPowerRatio, float totalGridDemand, float totalGridCapacity)
    {
        _suppliedPowerRatio = Mathf.Clamp(suppliedPowerRatio, 0.0f, 1.0f);
        _totalGridDemand = totalGridDemand;
        _totalGridCapacity = totalGridCapacity;
        UpdateVisualState();
        UpdateTooltip();
    }

    public void SetHeat(float value)
    {
        CurrentHeat = Mathf.Clamp(value, 0.0f, 100.0f);
        Efficiency = CalculateOutputEfficiency(CurrentHeat);
        UpdateVisualState();
        UpdateTooltip();
    }

    public void SetPlacementHighlight(bool isHighlighted)
    {
        _placementHighlighted = isHighlighted;
        UpdateVisualState();
    }

    public void SetMergeSelection(bool isSelected)
    {
        _mergeSelected = isSelected;
        UpdateVisualState();
    }

    public void SetTouchSelection(bool isSelected)
    {
        _touchSelected = isSelected;
        UpdateVisualState();
    }

    /// <summary>
    /// Plays the larger merge confirmation animation and optional merge sound.
    /// </summary>
    public void PlayMergePulse()
    {
        _mergePulseTween?.Kill();
        UpdatePivotOffset();

        if (Socket == null)
        {
            return;
        }

        MergeSound?.Play();
        _isPulseAnimating = true;
        Vector2 baseScale = Vector2.One;
        Scale = baseScale;
        Socket.Scale = baseScale;
        Socket.Rotation = 0.0f;
        _mergePulseTween = CreateTween();
        _mergePulseTween.SetTrans(Tween.TransitionType.Sine);
        _mergePulseTween.SetEase(Tween.EaseType.Out);
        _mergePulseTween.TweenProperty(Socket, "scale", new Vector2(1.15f, 0.9f), 0.08f);
        _mergePulseTween.Parallel().TweenProperty(Socket, "rotation", 0.087f, 0.08f);
        _mergePulseTween.SetEase(Tween.EaseType.InOut);
        _mergePulseTween.TweenProperty(Socket, "scale", new Vector2(0.95f, 1.1f), 0.08f);
        _mergePulseTween.Parallel().TweenProperty(Socket, "rotation", -0.087f, 0.08f);
        _mergePulseTween.TweenProperty(Socket, "scale", baseScale, 0.1f);
        _mergePulseTween.Parallel().TweenProperty(Socket, "rotation", 0.0f, 0.1f);
        _mergePulseTween.Finished += () => { _isPulseAnimating = false; Socket.Rotation = 0.0f; };
    }

    /// <summary>
    /// Plays the smaller placement animation used when building a new Home.
    /// </summary>
    public void PlayPlacementJuice()
    {
        _mergePulseTween?.Kill();
        UpdatePivotOffset();

        if (Socket == null)
        {
            return;
        }

        _isPulseAnimating = true;
        Scale = Vector2.One;
        Socket.Scale = new Vector2(0.8f, 1.2f);
        _mergePulseTween = CreateTween();
        _mergePulseTween.SetTrans(Tween.TransitionType.Back);
        _mergePulseTween.SetEase(Tween.EaseType.Out);
        _mergePulseTween.TweenProperty(Socket, "scale", new Vector2(1.06f, 0.95f), 0.1f);
        _mergePulseTween.TweenProperty(Socket, "scale", Vector2.One, 0.1f);
        _mergePulseTween.Finished += () => _isPulseAnimating = false;
    }

    private void UpdatePivotOffset()
    {
        PivotOffset = Size * 0.5f;
    }

    /// <summary>
    /// Converts pollution into a production penalty for income-generating buildings.
    /// </summary>
    private float CalculateOutputEfficiency(float heat)
    {
        if (Role != TileRole.Processor && Role != TileRole.LogicGate && Role != TileRole.Transistor)
        {
            return 1.0f;
        }

        if (heat < WarmThrottleThreshold)
        {
            return 1.0f;
        }

        if (heat < HotThrottleThreshold)
        {
            return Mathf.Lerp(1.0f, 0.7f, NormalizeRange(heat, WarmThrottleThreshold, HotThrottleThreshold));
        }

        if (heat < CriticalThrottleThreshold)
        {
            return Mathf.Lerp(0.7f, 0.35f, NormalizeRange(heat, HotThrottleThreshold, CriticalThrottleThreshold));
        }

        return 0.2f;
    }

    private static float NormalizeRange(float value, float min, float max)
    {
        if (Mathf.IsEqualApprox(min, max))
        {
            return 0.0f;
        }

        return Mathf.Clamp((value - min) / (max - min), 0.0f, 1.0f);
    }

    /// <summary>
    /// Recolors the tile to reflect role, power state, placement targeting, and selection.
    /// </summary>
    private void UpdateVisualState()
    {
        Color backgroundColor = Role switch
        {
            TileRole.PowerRail => new Color("#20363f"),
            TileRole.Processor => new Color("#2a2620"),
            TileRole.LogicGate => new Color("#2b2a1f"),
            TileRole.Transistor => new Color("#243022"),
            TileRole.Fan => new Color("#1f2b35"),
            _ => new Color("#1c2b24"),
        };

        if (Background != null)
        {
            Background.SelfModulate = backgroundColor;
        }

        if (Traces != null)
        {
            Color heatColor = new Color("#ffd700").Lerp(new Color("#ff4500"), CurrentHeat / 100.0f);
            Traces.SelfModulate = heatColor;
        }

        if (Socket != null)
        {
            Color socketColor = Role switch
            {
                TileRole.PowerRail => new Color("#70d6ff"),
                TileRole.Processor => new Color("#f2f5f7"),
                TileRole.LogicGate => new Color("#ffe680"),
                TileRole.Transistor => new Color("#b7f171"),
                TileRole.Fan => new Color("#7df9ff"),
                _ => new Color("#7fb069"),
            };

            if ((Role == TileRole.Processor || Role == TileRole.LogicGate || Role == TileRole.Transistor) && _suppliedPowerRatio < 1.0f)
            {
                socketColor = socketColor.Lerp(new Color("#ff7f50"), 1.0f - _suppliedPowerRatio);
            }

            if (_placementHighlighted)
            {
                socketColor = socketColor.Lerp(new Color("#80ff72"), 0.75f);
            }

            if (_mergeSelected)
            {
                socketColor = socketColor.Lerp(new Color("#80a5ff"), 0.8f);
            }

            if (_touchSelected)
            {
                socketColor = socketColor.Lerp(new Color("#ffffff"), 0.55f);
            }

            Socket.SelfModulate = socketColor;
        }
    }

    /// <summary>
    /// Keeps the desktop tooltip aligned with the current simulation numbers.
    /// </summary>
    private void UpdateTooltip()
    {
        TooltipText = CityTerminology.FormatTileTooltip(this, _suppliedPowerRatio, _totalGridDemand, _totalGridCapacity);
    }
}