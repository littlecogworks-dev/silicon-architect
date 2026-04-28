using Godot;

/// <summary>
/// Lightweight popup label used for coin gain and other short-lived board feedback.
/// </summary>
public partial class FloatingText : Label
{
    [Export] public float LifetimeSeconds = 0.8f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        AddThemeFontSizeOverride("font_size", 28);
    }

    /// <summary>
    /// Animates the label upward while fading it out, then frees the node.
    /// </summary>
    public void Play(string message, Color color, Vector2 travel)
    {
        Text = message;
        Modulate = color;
        Scale = new Vector2(0.7f, 0.7f);

        Vector2 startPosition = Position;
        Vector2 endPosition = startPosition + travel;

        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(this, "position", endPosition, LifetimeSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "scale", Vector2.One, LifetimeSeconds * 0.35f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "modulate:a", 0.0f, LifetimeSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
        tween.Finished += QueueFree;
    }
}