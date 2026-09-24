using System;
using System.Collections.Generic;
using NavalBattle.Shared;

namespace NavalBattle.Server
{
    public sealed class Board
    {
        private readonly Dictionary<CellCoord, Ship> _shipByCell = new();
        private readonly HashSet<CellCoord> _shots = new();

        public int Size { get; }
        public IReadOnlyList<Ship> Ships { get; }

        public Board(int size, List<Ship> ships)
        {
            Size = size;
            Ships = ships;

            foreach (var ship in ships)
            {
                foreach (var cell in ship.Cells)
                    _shipByCell[cell] = ship;
            }
        }

        public bool InBounds(CellCoord cell) =>
            cell.X >= 0 && cell.Y >= 0 && cell.X < Size && cell.Y < Size;

        public bool WasShot(CellCoord cell) => _shots.Contains(cell);

        public bool TryGetShip(CellCoord cell, out Ship ship) =>
            _shipByCell.TryGetValue(cell, out ship);

        public ShotOutcome Fire(CellCoord cell, out Ship hitShip, out List<CellCoord> sunkCells)
        {
            hitShip = null;
            sunkCells = null;

            if (!InBounds(cell))
                throw new ArgumentOutOfRangeException(nameof(cell));

            if (_shots.Contains(cell))
                throw new InvalidOperationException("Cell already shot.");

            _shots.Add(cell);

            if (!_shipByCell.TryGetValue(cell, out hitShip))
                return ShotOutcome.Miss;

            hitShip.RegisterHit(cell);
            if (!hitShip.IsSunk)
                return ShotOutcome.Hit;

            sunkCells = new List<CellCoord>(hitShip.Cells);
            return ShotOutcome.Sunk;
        }

        public bool AllShipsSunk()
        {
            foreach (var ship in Ships)
            {
                if (!ship.IsSunk)
                    return false;
            }

            return true;
        }

        public byte[] ToOwnCellMarks()
        {
            var marks = new byte[Size * Size];
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var cell = new CellCoord(x, y);
                var index = y * Size + x;

                if (_shots.Contains(cell))
                {
                    if (_shipByCell.TryGetValue(cell, out var ship))
                        marks[index] = (byte)(ship.IsSunk ? CellMark.Sunk : CellMark.Hit);
                    else
                        marks[index] = (byte)CellMark.Miss;
                }
                else if (_shipByCell.ContainsKey(cell))
                {
                    marks[index] = (byte)CellMark.Ship;
                }
                else
                {
                    marks[index] = (byte)CellMark.Empty;
                }
            }

            return marks;
        }

        public byte[] ToFogCellMarks()
        {
            var marks = new byte[Size * Size];
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var cell = new CellCoord(x, y);
                var index = y * Size + x;

                if (!_shots.Contains(cell))
                {
                    marks[index] = (byte)CellMark.Unknown;
                    continue;
                }

                if (_shipByCell.TryGetValue(cell, out var ship))
                    marks[index] = (byte)(ship.IsSunk ? CellMark.Sunk : CellMark.Hit);
                else
                    marks[index] = (byte)CellMark.Miss;
            }

            return marks;
        }
    }
}
