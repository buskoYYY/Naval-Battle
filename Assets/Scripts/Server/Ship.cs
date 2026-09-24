using System.Collections.Generic;
using NavalBattle.Shared;

namespace NavalBattle.Server
{
    public sealed class Ship
    {
        public int Length { get; }
        public List<CellCoord> Cells { get; } = new();
        public HashSet<CellCoord> Hits { get; } = new();

        public Ship(int length)
        {
            Length = length;
        }

        public bool Contains(CellCoord cell) => Cells.Contains(cell);

        public bool IsSunk => Hits.Count >= Cells.Count;

        public void RegisterHit(CellCoord cell)
        {
            if (Contains(cell))
                Hits.Add(cell);
        }
    }
}
