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
        [SerializeField] private Button _dropUploadButton;
        [SerializeField] private Button _dropDownloadButton;
        [SerializeField] private Button _syncButton;
        [SerializeField] private Slider _latencySlider;
        [SerializeField] private Text _latencyValueLabel;

        private GameClient _client;
        private INetworkPeer _peer;
        private float _maxLatencyMs = 10000f;

        public void Bind(GameClient client, INetworkPeer peer, string title, float maxLatencyMs = 10000f)
        {
            _client = client;
            _peer = peer;
            _maxLatencyMs = Mathf.Max(100f, maxLatencyMs);

            EnsureUi(title);
            _client.StateChanged += Refresh;
            _disconnectButton.onClick.AddListener(() => _peer.Disconnect());
            _connectButton.onClick.AddListener(() =>
            {
                _peer.Connect();
                _client.RequestReconnect();
            });

            if (_dropUploadButton != null)
                _dropUploadButton.onClick.AddListener(() =>
                {
                    _peer.DropNextOutgoing++;
                    Refresh();
                });

            if (_dropDownloadButton != null)
                _dropDownloadButton.onClick.AddListener(() =>
                {
                    _peer.DropNextIncoming++;
                    Refresh();
                });

            if (_syncButton != null)
                _syncButton.onClick.AddListener(() => _client.RequestSync());

            if (_latencySlider != null)
            {
                _latencySlider.minValue = 0f;
                _latencySlider.maxValue = _maxLatencyMs;
                _latencySlider.wholeNumbers = true;
                _latencySlider.SetValueWithoutNotify(_peer.LatencyMs);
                _latencySlider.onValueChanged.AddListener(OnLatencySliderChanged);
                UpdateLatencyLabel(_peer.LatencyMs);
            }

            Refresh();
        }

        public void Unbind()
        {
            if (_client != null)
                _client.StateChanged -= Refresh;

            if (_latencySlider != null)
                _latencySlider.onValueChanged.RemoveListener(OnLatencySliderChanged);
        }

        private void OnLatencySliderChanged(float ms)
        {
            if (_peer == null)
                return;

            _peer.LatencyMs = Mathf.Clamp(ms, 0f, _maxLatencyMs);
            UpdateLatencyLabel(_peer.LatencyMs);
            Refresh();
        }

        private void UpdateLatencyLabel(float ms)
        {
            if (_latencyValueLabel != null)
                _latencyValueLabel.text = $"Receive delay: {ms:0} ms";
        }

        private void Refresh()
        {
            if (_client == null)
                return;

            if (_title != null)
                _title.text = $"{_client.PlayerId}";

            if (_status != null)
            {
                var pending = string.Empty;
                if (_client.HasPendingShot)
                {
                    pending = _client.RetryCount > 0
                        ? $"  |  retry #{_client.RetryCount}"
                        : "  |  waiting reply";
                }

                var drops = string.Empty;
                if (_peer.DropNextOutgoing > 0 || _peer.DropNextIncoming > 0)
                    drops = $"  |  drop↑{_peer.DropNextOutgoing} ↓{_peer.DropNextIncoming}";

                _status.text = _client.StatusText + pending + drops;
            }

            UpdateLatencyLabel(_peer.LatencyMs);

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

            var dropRow = new GameObject("DropButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            dropRow.transform.SetParent(transform, false);
            dropRow.GetComponent<HorizontalLayoutGroup>().spacing = 6;
            _dropUploadButton = CreateButton(dropRow.transform, "Drop next ↑");
            _dropDownloadButton = CreateButton(dropRow.transform, "Drop next ↓");
            _syncButton = CreateButton(dropRow.transform, "Sync");

            _latencyValueLabel = CreateText(transform, "Receive delay: 200 ms", 12);
            _latencySlider = CreateSlider(transform);
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

        private static Slider CreateSlider(Transform parent)
        {
            var root = new GameObject("LatencySlider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<LayoutElement>().preferredHeight = 24;
            root.GetComponent<LayoutElement>().flexibleWidth = 1;

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            Stretch(bg.GetComponent<RectTransform>());
            bg.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            Stretch(fillAreaRt);
            fillAreaRt.offsetMin = new Vector2(5, 6);
            fillAreaRt.offsetMax = new Vector2(-5, -6);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Stretch(fill.GetComponent<RectTransform>());
            fill.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.95f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>());

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(16, 0);
            handle.GetComponent<Image>().color = Color.white;

            var slider = root.GetComponent<Slider>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRt;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
            Stretch(text.GetComponent<RectTransform>());
            return go.GetComponent<Button>();
        }
    }
}
