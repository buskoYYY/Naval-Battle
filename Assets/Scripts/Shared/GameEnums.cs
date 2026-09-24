namespace NavalBattle.Shared
{
    public enum PlayerId : byte
    {
        None = 0,
        Player1 = 1,
        Player2 = 2
    }

    public enum ShotOutcome : byte
    {
        Miss = 0,
        Hit = 1,
        Sunk = 2
    }

    public enum CellMark : byte
    {
        Unknown = 0,
        Empty = 1,
        Ship = 2,
        Miss = 3,
        Hit = 4,
        Sunk = 5,
        Pending = 6
    }

    public enum RejectReason : byte
    {
        None = 0,
        NotYourTurn = 1,
        AlreadyShot = 2,
        OutOfBounds = 3,
        ShotPending = 4,
        GameNotRunning = 5,
        DuplicateRequest = 6,
        NotConnected = 7
    }

    public enum MatchPhase : byte
    {
        WaitingForPlayers = 0,
        Playing = 1,
        PausedDisconnected = 2,
        Finished = 3
    }
}
