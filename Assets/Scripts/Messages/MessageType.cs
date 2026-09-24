namespace NavalBattle.Messages
{
    public enum MessageType : byte
    {
        // Client -> Server
        JoinRequest = 1,
        FireRequest = 2,
        ReconnectRequest = 3,

        // Server -> Client
        Welcome = 50,
        MatchStarted = 51,
        FireRejected = 52,
        ShotResult = 53,
        StateSnapshot = 54,
        OpponentConnectionChanged = 55,
        MatchFinished = 56,
        Error = 57
    }
}
