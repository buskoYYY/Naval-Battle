using NavalBattle.Client;
using NavalBattle.Shared;
using NavalBattle.Transport;
using UnityEngine;
using UnityEngine.UI;

namespace NavalBattle.UI
{
    public sealed class PlayerPanelView : MonoBehaviour
    {
        private GameClient _client;
        private INetworkPeer _peer;
        private Text _title;
        private Text _status;
        private BoardGridView _ownBoard;
        private BoardGridView _enemyBoard;

        public void Bind(GameClient client, INetworkPeer peer, string title)
        {
            _client = client;
            _peer = peer;
            BuildUi(title);
            _client.StateChanged += Refresh;
            Refresh();
        }

        public void Unbind()
        {
            if (_client != null)
                _client.StateChanged -= Refresh;
        }

        private void BuildUi(string title)
        {
            var root = gameObject.GetComponent<RectTransform>();
            var layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            _title = CreateText(transform, title, 18, FontStyle.Bold);
            _status = CreateText(transform, "Status", 14, FontStyle.Normal);

            CreateText(transform, "My board", 12, FontStyle.Italic);
            _ownBoard = CreateBoard(transform, false);

            CreateText(transform, "Enemy board", 12, FontStyle.Italic);
            _enemyBoard = CreateBoard(transform, true);

            var buttons = new GameObject("NetButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            buttons.transform.SetParent(transform, false);
            var h = buttons.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            CreateButton(buttons.transform, "Disconnect", () => _peer?.Disconnect());
            CreateButton(buttons.transform, "Connect", () =>
            {
                _peer?.Connect();
                _client?.RequestReconnect();
            });
        }

        private BoardGridView CreateBoard(Transform parent, bool enemy)
        {
            var host = new GameObject(enemy ? "EnemyBoard" : "OwnBoard", typeof(RectTransform));
            host.transform.SetParent(parent, false);
            var view = host.AddComponent<BoardGridView>();
            var size = _client.BoardView.Size > 0 ? _client.BoardView.Size : 6;
            view.Build(host.transform, size, 28f, (x, y) =>
            {
                if (enemy)
                    _client.TryFire(x, y);
            });
            view.SetInteractable(enemy);
            return view;
        }

        private void Refresh()
        {
            if (_client == null)
                return;

            _status.text = $"{_client.StatusText} | phase={_client.Phase} | turn={_client.CurrentTurn}";

            if (_client.BoardView.Size > 0)
            {
                if (_ownBoard != null)
                {
                    _ownBoard.SetInteractable(false);
                    _ownBoard.Render(_client.BoardView.OwnCells);
                }

                if (_enemyBoard != null)
                {
                    _enemyBoard.SetInteractable(_client.IsMyTurn);
                    _enemyBoard.Render(_client.BoardView.OpponentFog);
                }
            }
        }

        private static Text CreateText(Transform parent, string value, int size, FontStyle style)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = size + 8;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
        }

        private static void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 28;
            go.GetComponent<LayoutElement>().preferredWidth = 100;
            go.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(action);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
