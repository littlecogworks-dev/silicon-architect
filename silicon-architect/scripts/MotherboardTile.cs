using Godot;
using System;

public partial class MotherboardTile : Control
{
    // Use TextureRect here to match your new nodes
    [Export] public TextureRect Background;
    [Export] public TextureRect Traces;
    [Export] public TextureRect Socket;

    private float _heat = 0.0f;

    public void SetHeat(float value)
    {
        _heat = Mathf.Clamp(value, 0, 100);
        // Lerp from Gold (#ffd700) to Melting Orange (#ff4500)
        if (Traces != null)
        {
            Traces.SelfModulate = new Color("#ffd700").Lerp(new Color("#ff4500"), _heat / 100f);
        }
    }
}