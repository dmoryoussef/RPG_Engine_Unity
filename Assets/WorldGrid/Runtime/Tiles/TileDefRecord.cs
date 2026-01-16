using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGrid.Runtime.Tiles;

[Serializable]
public sealed class TileDefRecord
{
    [SerializeField] private int tileId;
    [SerializeField] private string name;
    [SerializeField] private RectUv uv;

    [SerializeField] private List<string> tags = new List<string>();

    // Key: polymorphic properties editable in inspector
    [SerializeReference]
    private List<WorldGrid.Runtime.Tiles.TileProperty> properties
        = new List<WorldGrid.Runtime.Tiles.TileProperty>();

    public int TileId => tileId;
    public string Name => name;
    public RectUv Uv => uv;
    public IReadOnlyList<string> Tags => tags;
    public IReadOnlyList<WorldGrid.Runtime.Tiles.TileProperty> Properties => properties;
}