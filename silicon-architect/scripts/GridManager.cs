using Godot;
using System;

public partial class GridManager : GridContainer
{
    // Point these to your Tile scene in the Inspector
    [Export] public PackedScene TileScene;
    [Export] public int GridSize = 5;

    public override void _Ready()
    {
        // Set the GridContainer columns to match our size
        Columns = GridSize;
        GenerateGrid();
    }

    private void GenerateGrid()
    {
        // Clear any placeholder tiles
        foreach (Node child in GetChildren()) { child.QueueFree(); }

        for (int i = 0; i < GridSize * GridSize; i++)
        {
            var tile = TileScene.Instantiate<MotherboardTile>();
            AddChild(tile);
            
            // Give each tile its coordinates (useful for later heat-bleed logic)
            int x = i % GridSize;
            int y = i / GridSize;
            tile.Name = $"Tile_{x}_{y}";
        }
    }
}