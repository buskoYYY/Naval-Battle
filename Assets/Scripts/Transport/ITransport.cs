using System;
using NavalBattle.Messages;

namespace NavalBattle.Transport
{
    public interface INetworkPeer
    {
        string PeerId { get; }
        bool IsConnected { get; }
        event Action<NetworkEnvelope> MessageReceived;
        event Action Disconnected;
        event Action Connected;

        void Send(NetworkEnvelope envelope);
        void Disconnect();
        void Connect();
    }

    public interface ITransportHub
    {
        float LatencyMs { get; set; }
        bool LogEnabled { get; set; }

        event Action<string> LogLine;

        INetworkPeer CreateClientPeer(string peerId);
        void BindServerHandler(Action<string, NetworkEnvelope> onClientMessage);
        void SendToClient(string peerId, NetworkEnvelope envelope);
        void DisconnectPeer(string peerId);
        void ConnectPeer(string peerId);
        void Shutdown();
        void Tick(float nowSeconds);
    }
}
