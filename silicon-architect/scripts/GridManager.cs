using Godot;
using System;

public partial class GridManager : GridContainer
{
    [Export] public PackedScene TileScene;
    [Export] public int GridSize = 5;
    [Export] public float BoardWidthRatio = 1.0f;
    [Export] public float BoardHeightRatio = 1.0f;
    [Export] public int MaxTileSize = 256;
    [Export] public int MinTileSize = 96;

    private bool _gridGenerated;

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
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            UpdateBoardLayout();
        }
    }

    private void GenerateGrid()
    {
        if (_gridGenerated || TileScene == null)
        {
            return;
        }

        for (int i = 0; i < GridSize * GridSize; i++)
        {
            var tile = TileScene.Instantiate<MotherboardTile>();
            AddChild(tile);

            int x = i % GridSize;
            int y = i / GridSize;
            tile.Name = $"Tile_{x}_{y}";
        }

        _gridGenerated = true;
    }

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

        if (tileSize >= MinTileSize)
        {
            tileSize = Mathf.Max(MinTileSize, tileSize);
        }

        Vector2 tileDimensions = new(tileSize, tileSize);
        Vector2 boardSize = tileDimensions * GridSize;

        CustomMinimumSize = boardSize;
        Size = boardSize;
        Position = new Vector2(
            Mathf.Round((viewportSize.X - boardSize.X) * 0.5f),
            Mathf.Round((viewportSize.Y - boardSize.Y) * 0.5f)
        );

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