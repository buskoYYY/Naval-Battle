using System;

namespace NavalBattle.Messages
{
    [Serializable]
    public class NetworkEnvelope
    {
        public MessageType Type;
        public int Seq;
        public string PayloadJson;
        public long SentUnixMs;
    }

    [Serializable]
    public class JoinRequest
    {
        public byte PreferredPlayerId;
    }

    [Serializable]
    public class FireRequest
    {
        public string RequestId;
        public int X;
        public int Y;
    }

    [Serializable]
    public class ReconnectRequest
    {
        public byte PlayerId;
        public int LastReceivedSeq;
    }

    [Serializable]
    public class SyncRequest
    {
        public byte PlayerId;
    }

    [Serializable]
    public class WelcomeMessage
    {
        public byte PlayerId;
        public byte Phase;
    }

    [Serializable]
    public class MatchStartedMessage
    {
        public byte YourPlayerId;
        public byte CurrentTurnPlayerId;
        public int BoardSize;
        /// <summary>Flat row-major marks for own board (Empty/Ship).</summary>
        public byte[] YourCells;
        public float TurnSecondsRemaining;
        public float TurnTimeoutSeconds;
        /// <summary>Absolute Time.realtimeSinceStartup when the turn ends (same process clock).</summary>
        public float TurnEndsAtRealtime;
    }

    [Serializable]
    public class FireRejectedMessage
    {
        public string RequestId;
        public byte Reason;
    }

    [Serializable]
    public class ShotResultMessage
    {
        public string RequestId;
        public byte ShooterPlayerId;
        public int X;
        public int Y;
        public byte Outcome;
        public int[] SunkCellsX;
        public int[] SunkCellsY;
        public byte NextTurnPlayerId;
        public bool GameOver;
        public byte WinnerPlayerId;
        public float TurnSecondsRemaining;
        public float TurnTimeoutSeconds;
        public float TurnEndsAtRealtime;
    }

    [Serializable]
    public class StateSnapshotMessage
    {
        public byte YourPlayerId;
        public byte Phase;
        public byte CurrentTurnPlayerId;
        public int BoardSize;
        public byte[] YourCells;
        public byte[] OpponentFogCells;
        public bool GameOver;
        public byte WinnerPlayerId;
        public bool HasPendingShot;
        public string PendingRequestId;
        public int PendingX;
        public int PendingY;
        public float TurnSecondsRemaining;
        public float TurnTimeoutSeconds;
        public float TurnEndsAtRealtime;
    }

    [Serializable]
    public class TurnUpdateMessage
    {
        public byte CurrentTurnPlayerId;
        public bool TimedOut;
        public byte TimedOutPlayerId;
        public float TurnSecondsRemaining;
        public float TurnTimeoutSeconds;
        public float TurnEndsAtRealtime;
        public string Reason;
    }

    [Serializable]
    public class OpponentConnectionChangedMessage
    {
        public byte OpponentPlayerId;
        public bool IsConnected;
    }

    [Serializable]
    public class MatchFinishedMessage
    {
        public byte WinnerPlayerId;
        public string Reason;
    }

    [Serializable]
    public class ErrorMessage
    {
        public string Text;
    }
}
