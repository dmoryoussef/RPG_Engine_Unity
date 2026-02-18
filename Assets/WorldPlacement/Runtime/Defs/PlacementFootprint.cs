using System;
using System.Collections.Generic;
using WorldPlacement.Runtime.Grid;

namespace WorldPlacement.Runtime.Defs
{
    /// <summary>
    /// Mini tilemap footprint. Width/Height are helpers; Cells are authoritative.
    /// Pivot is the local cell aligned to the anchor world cell when placing.
    /// </summary>
    public sealed class PlacementFootprint
    {
        public int Width { get; }
        public int Height { get; }
        public Cell2i Pivot { get; }

        /// <summary>Row-major: index = x + y*Width</summary>
        public FootprintCellKind[] Cells { get; }

        public PlacementFootprint(int width, int height, Cell2i pivot, FootprintCellKind[] cells)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (cells.Length != width * height) throw new ArgumentException("Cells length must equal width*height.");

            Width = width;
            Height = height;

            pivot = new Cell2i(
                pivot.X < 0 ? 0 : (pivot.X >= width ? width - 1 : pivot.X),
                pivot.Y < 0 ? 0 : (pivot.Y >= height ? height - 1 : pivot.Y)
            );
            Pivot = pivot;

            Cells = cells;
        }

        public FootprintCellKind GetCell(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return FootprintCellKind.Empty;
            return Cells[x + y * Width];
        }

        public IEnumerable<Cell2i> EnumerateOccupiedLocal()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var k = GetCell(x, y);
                    if (k != FootprintCellKind.Empty)
                        yield return new Cell2i(x, y);
                }
        }

        public void GetOccupiedWorldCells(Cell2i anchorWorld, Rotation4 rotation, List<Cell2i> outCells)
        {
            if (outCells == null) throw new ArgumentNullException(nameof(outCells));
            outCells.Clear();

            int rot = ((int)rotation) & 3;

            var rotPivot = RotateLocal(Pivot, rot, Width, Height);

            foreach (var local in EnumerateOccupiedLocal())
            {
                var rl = RotateLocal(local, rot, Width, Height);
                // world = anchor - rotPivot + rl
                outCells.Add(new Cell2i(anchorWorld.X - rotPivot.X + rl.X, anchorWorld.Y - rotPivot.Y + rl.Y));
            }
        }

        private static Cell2i RotateLocal(Cell2i p, int rot, int w, int h)
        {
            // Clockwise rotation in discrete grid around the w x h rectangle.
            // (x,y) -> (y, w-1-x) for 90°
            return rot switch
            {
                0 => p,
                1 => new Cell2i(p.Y, w - 1 - p.X),
                2 => new Cell2i(w - 1 - p.X, h - 1 - p.Y),
                3 => new Cell2i(h - 1 - p.Y, p.X),
                _ => p
            };
        }
    }
}
