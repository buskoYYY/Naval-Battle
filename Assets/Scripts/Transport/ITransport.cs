using System;
using NavalBattle.Messages;

namespace NavalBattle.Transport
{
    public interface INetworkPeer
    {
        string PeerId { get; }
        bool IsConnected { get; }

        /// <summary>
        /// One-way receive delay for this client (S->C), in milliseconds.
        /// C->S is not delayed so asymmetric lag is visible on both boards at once.
        /// </summary>
        float LatencyMs { get; set; }

        event Action<NetworkEnvelope> MessageReceived;
        event Action Disconnected;
        event Action Connected;

        void Send(NetworkEnvelope envelope);
        void Disconnect();
        void Connect();
    }

    public interface ITransportHub
    {
        float DefaultLatencyMs { get; set; }
        bool LogEnabled { get; set; }

        event Action<string> LogLine;

        INetworkPeer CreateClientPeer(string peerId, float? latencyMs = null);
        void BindServerHandler(Action<string, NetworkEnvelope> onClientMessage);
        void SendToClient(string peerId, NetworkEnvelope envelope);
        void DisconnectPeer(string peerId);
        void ConnectPeer(string peerId);
        void Shutdown();
        void Tick(float nowSeconds);
    }
}
