using Godot;
using System;

public partial class MotherboardTile : Control
{
    [Export] public TextureRect Background;
    [Export] public TextureRect Traces;
    [Export] public TextureRect Socket;

    private float _heat = 0.0f;

    public void SetHeat(float value)
    {
        _heat = Mathf.Clamp(value, 0, 100);
        // Turn the traces from Cold Blue to Hot Orange
        Traces.SelfModulate = new Color("#40e0d0").Lerp(new Color("#ff4500"), _heat / 100f);
    }
}