using UnityEngine;

namespace NavalBattle.Config
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Naval Battle/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Board")]
        [Min(1)]
        public int BoardSize = 6;

        [Tooltip("Ship lengths placed randomly by the server.")]
        public int[] ShipLengths = { 3, 2, 2, 1 };

        [Tooltip("If true, ships may not share an orthogonal edge. Diagonal touch is allowed.")]
        public bool ForbidOrthogonalTouch = true;

        [Header("Network defaults")]
        [Min(0f)]
        public float DefaultLatencyMs = 200f;

        [Min(0f)]
        public float MaxLatencyMs = 5000f;

        [Header("Match")]
        [Tooltip("If a player stays disconnected longer than this, opponent wins. 0 = pause forever.")]
        [Min(0f)]
        public float DisconnectForfeitSeconds = 0f;

        [Tooltip("Optional turn timer in seconds. 0 = disabled.")]
        [Min(0f)]
        public float TurnTimeoutSeconds = 0f;

        public void ValidateOrThrow()
        {
            if (BoardSize < 1)
                throw new System.InvalidOperationException("BoardSize must be >= 1.");

            if (ShipLengths == null || ShipLengths.Length == 0)
                throw new System.InvalidOperationException("ShipLengths must not be empty.");

            var cellsNeeded = 0;
            foreach (var length in ShipLengths)
            {
                if (length < 1)
                    throw new System.InvalidOperationException("Ship length must be >= 1.");
                if (length > BoardSize)
                    throw new System.InvalidOperationException($"Ship length {length} exceeds board size.");
                cellsNeeded += length;
            }

            if (cellsNeeded > BoardSize * BoardSize)
                throw new System.InvalidOperationException("Ships do not fit on the board.");
        }
    }
}
