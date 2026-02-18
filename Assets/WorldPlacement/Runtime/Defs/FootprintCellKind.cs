namespace WorldPlacement.Runtime.Defs
{
    public enum FootprintCellKind : byte
    {
        Empty = 0,
        Solid = 1,
        Door = 2, // MVP: treated as occupied; access rules can come later.
    }
}
