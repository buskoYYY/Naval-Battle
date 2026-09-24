using System;
using System.Collections.Generic;
using NavalBattle.Config;
using NavalBattle.Shared;
using UnityEngine;

namespace NavalBattle.Server
{
    public static class ShipPlacer
    {
        private static readonly CellCoord[] Orthogonal =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        public static Board CreateRandomBoard(GameConfig config, System.Random rng = null)
        {
            config.ValidateOrThrow();
            rng ??= new System.Random();

            const int maxAttempts = 200;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (TryPlaceAll(config, rng, out var ships))
                    return new Board(config.BoardSize, ships);
            }

            throw new InvalidOperationException("Failed to place ships after many attempts.");
        }

        private static bool TryPlaceAll(GameConfig config, System.Random rng, out List<Ship> ships)
        {
            ships = new List<Ship>();
            var occupied = new HashSet<CellCoord>();

            foreach (var length in config.ShipLengths)
            {
                if (!TryPlaceShip(config, rng, length, occupied, out var ship))
                {
                    ships = null;
                    return false;
                }

                ships.Add(ship);
                foreach (var cell in ship.Cells)
                    occupied.Add(cell);
            }

            return true;
        }

        private static bool TryPlaceShip(
            GameConfig config,
            System.Random rng,
            int length,
            HashSet<CellCoord> occupied,
            out Ship ship)
        {
            ship = null;
            var size = config.BoardSize;

            for (var attempt = 0; attempt < 100; attempt++)
            {
                var horizontal = rng.Next(2) == 0;
                var x = rng.Next(0, horizontal ? size - length + 1 : size);
                var y = rng.Next(0, horizontal ? size : size - length + 1);

                var cells = new List<CellCoord>(length);
                var ok = true;

                for (var i = 0; i < length; i++)
                {
                    var cell = horizontal ? new CellCoord(x + i, y) : new CellCoord(x, y + i);
                    if (occupied.Contains(cell) ||
                        (config.ForbidOrthogonalTouch && TouchesOccupied(cell, occupied)))
                    {
                        ok = false;
                        break;
                    }

                    cells.Add(cell);
                }

                if (!ok)
                    continue;

                ship = new Ship(length);
                ship.Cells.AddRange(cells);
                return true;
            }

            return false;
        }

        private static bool TouchesOccupied(CellCoord cell, HashSet<CellCoord> occupied)
        {
            foreach (var d in Orthogonal)
            {
                var n = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                if (occupied.Contains(n))
                    return true;
            }

            return false;
        }
    }
}
