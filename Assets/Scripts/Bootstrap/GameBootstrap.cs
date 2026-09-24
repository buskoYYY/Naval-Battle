using NavalBattle.Client;
using NavalBattle.Config;
using NavalBattle.Server;
using NavalBattle.Shared;
using NavalBattle.Transport;
using NavalBattle.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NavalBattle.Bootstrap
{
    /// <summary>
    /// Boots server + two clients in one process. Drop on an empty scene object.
    /// Builds a minimal UI at runtime so the scene needs no manual wiring.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<GameBootstrap>() != null)
                return;

            var go = new GameObject("GameBootstrap");
            go.AddComponent<GameBootstrap>();
        }

        private InProcessTransportHub _transport;
        private GameServer _server;
        private GameClient _client1;
        private GameClient _client2;
        private PlayerPanelView _panel1;
        private PlayerPanelView _panel2;
        private DebugPanelView _debug;

        private void Awake()
        {
            if (_config == null)
                _config = ScriptableObject.CreateInstance<GameConfig>();

            _config.ValidateOrThrow();

            _transport = new InProcessTransportHub
            {
                LatencyMs = _config.DefaultLatencyMs,
                LogEnabled = true
            };

            _server = new GameServer(_config, _transport);

            var peer1 = _transport.CreateClientPeer("client-1");
            var peer2 = _transport.CreateClientPeer("client-2");

            peer1.Disconnected += () => _server.NotifyPeerDisconnected("client-1");
            peer2.Disconnected += () => _server.NotifyPeerDisconnected("client-2");
            peer1.Connected += () => _server.NotifyPeerConnected("client-1");
            peer2.Connected += () => _server.NotifyPeerConnected("client-2");

            _client1 = new GameClient(peer1, PlayerId.Player1);
            _client2 = new GameClient(peer2, PlayerId.Player2);

            BuildUi();
            _panel1.Bind(_client1, peer1, _transport, "Player 1");
            _panel2.Bind(_client2, peer2, _transport, "Player 2");
            _debug.Bind(_transport);

            _client1.Start();
            _client2.Start();
        }

        private void Update()
        {
            _transport?.Tick(Time.realtimeSinceStartup);
        }

        private void OnDestroy()
        {
            _panel1?.Unbind();
            _panel2?.Unbind();
            _debug?.Unbind();

            _client1?.Shutdown();
            _client2?.Shutdown();
            _server?.Shutdown();
            _transport?.Shutdown();
        }

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);

            var root = new GameObject("Root", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            root.transform.SetParent(canvasGo.transform, false);
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var p1 = new GameObject("Player1", typeof(RectTransform), typeof(Image), typeof(PlayerPanelView));
            p1.transform.SetParent(root.transform, false);
            p1.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.22f, 0.92f);
            _panel1 = p1.GetComponent<PlayerPanelView>();

            var p2 = new GameObject("Player2", typeof(RectTransform), typeof(Image), typeof(PlayerPanelView));
            p2.transform.SetParent(root.transform, false);
            p2.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.22f, 0.92f);
            _panel2 = p2.GetComponent<PlayerPanelView>();

            var debug = new GameObject("Debug", typeof(RectTransform), typeof(Image), typeof(DebugPanelView));
            debug.transform.SetParent(root.transform, false);
            debug.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.92f);
            var debugLe = debug.AddComponent<LayoutElement>();
            debugLe.preferredWidth = 420;
            _debug = debug.GetComponent<DebugPanelView>();
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }
    }
}
