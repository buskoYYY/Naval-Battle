using NavalBattle.Client;
using NavalBattle.Shared;
using NavalBattle.Transport;
using UnityEngine;
using UnityEngine.UI;

namespace NavalBattle.UI
{
    public sealed class PlayerPanelView : MonoBehaviour
    {
        [SerializeField] private Text _title;
        [SerializeField] private Text _status;
        [SerializeField] private BoardGridView _ownBoard;
        [SerializeField] private BoardGridView _enemyBoard;
        [SerializeField] private Button _disconnectButton;
        [SerializeField] private Button _connectButton;
        [SerializeField] private InputField _latencyField;

        private GameClient _client;
        private INetworkPeer _peer;
        private InProcessTransportHub _hub;

        public void Bind(GameClient client, INetworkPeer peer, InProcessTransportHub hub, string title)
        {
            _client = client;
            _peer = peer;
            _hub = hub;

            EnsureUi(title);
            _client.StateChanged += Refresh;
            _disconnectButton.onClick.AddListener(() => _peer.Disconnect());
            _connectButton.onClick.AddListener(() =>
            {
                _peer.Connect();
                _client.RequestReconnect();
            });

            if (_latencyField != null)
            {
                _latencyField.text = hub.LatencyMs.ToString("0");
                _latencyField.onEndEdit.AddListener(value =>
                {
                    if (float.TryParse(value, out var ms))
                        _hub.LatencyMs = Mathf.Clamp(ms, 0f, 10000f);
                });
            }

            Refresh();
        }

        public void Unbind()
        {
            if (_client != null)
                _client.StateChanged -= Refresh;
        }

        private void Refresh()
        {
            if (_client == null)
                return;

            if (_title != null)
                _title.text = $"{_client.PlayerId}";

            if (_status != null)
                _status.text = _client.StatusText;

            var size = Mathf.Max(1, _client.BoardView.Size);
            if (_ownBoard != null)
            {
                EnsureBoardBuilt(_ownBoard, size, null);
                _ownBoard.SetInteractable(false);
                _ownBoard.Render(_client.BoardView.OwnCells, showShips: true);
            }

            if (_enemyBoard != null)
            {
                EnsureBoardBuilt(_enemyBoard, size, (x, y) => _client.TryFire(x, y));
                _enemyBoard.SetInteractable(_client.IsMyTurn);
                _enemyBoard.Render(_client.BoardView.OpponentFog, showShips: false);
            }
        }

        private static void EnsureBoardBuilt(BoardGridView board, int size, System.Action<int, int> onClick)
        {
            if (board.BuiltSize != size)
                board.Build(size, onClick);
        }

        private void EnsureUi(string title)
        {
            if (_title == null || _status == null || _ownBoard == null || _enemyBoard == null)
                BuildRuntimeUi(title);
        }

        private void BuildRuntimeUi(string title)
        {
            var root = GetComponent<RectTransform>();
            if (root == null)
                root = gameObject.AddComponent<RectTransform>();

            var vertical = gameObject.GetComponent<VerticalLayoutGroup>();
            if (vertical == null)
                vertical = gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 6;
            vertical.childForceExpandHeight = false;
            vertical.childForceExpandWidth = true;
            vertical.padding = new RectOffset(8, 8, 8, 8);

            _title = CreateText(transform, title, 20);
            _status = CreateText(transform, "Status", 14);

            CreateText(transform, "My board", 12);
            _ownBoard = CreateBoard("OwnBoard");
            CreateText(transform, "Enemy board", 12);
            _enemyBoard = CreateBoard("EnemyBoard");

            var buttons = new GameObject("NetButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            buttons.transform.SetParent(transform, false);
            var h = buttons.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 6;

            _disconnectButton = CreateButton(buttons.transform, "Disconnect");
            _connectButton = CreateButton(buttons.transform, "Connect");

            CreateText(transform, "Latency ms", 12);
            _latencyField = CreateInput(transform, "200");
        }

        private BoardGridView CreateBoard(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup), typeof(BoardGridView));
            go.transform.SetParent(transform, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 230;
            le.preferredHeight = 230;
            return go.GetComponent<BoardGridView>();
        }

        private static Text CreateText(Transform parent, string value, int size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            go.GetComponent<LayoutElement>().preferredHeight = size + 8;
            return text;
        }

        private static Button CreateButton(Transform parent, string label)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.2f, 0.35f, 0.55f);
            go.GetComponent<LayoutElement>().preferredHeight = 28;
            go.GetComponent<LayoutElement>().flexibleWidth = 1;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var rt = text.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go.GetComponent<Button>();
        }

        private static InputField CreateInput(Transform parent, string value)
        {
            var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Color.white;
            go.GetComponent<LayoutElement>().preferredHeight = 28;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.black;
            text.supportRichText = false;
            var rt = text.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6, 0);
            rt.offsetMax = new Vector2(-6, 0);

            var input = go.GetComponent<InputField>();
            input.textComponent = text;
            input.text = value;
            return input;
        }
    }
}
