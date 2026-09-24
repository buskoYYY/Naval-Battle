using System.Text;
using NavalBattle.Transport;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NavalBattle.UI
{
    public sealed class DebugPanelView : MonoBehaviour
    {
        [SerializeField] private Toggle _logToggle;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Text _logText;
        [SerializeField] private ScrollRect _scroll;

        private readonly StringBuilder _buffer = new();
        private InProcessTransportHub _hub;
        private const int MaxChars = 12000;

        public void Bind(InProcessTransportHub hub)
        {
            _hub = hub;
            EnsureUi();

            _hub.LogLine += Append;
            if (_logToggle != null)
            {
                _logToggle.isOn = hub.LogEnabled;
                _logToggle.onValueChanged.AddListener(v => _hub.LogEnabled = v);
            }

            if (_restartButton != null)
                _restartButton.onClick.AddListener(() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
        }

        public void Unbind()
        {
            if (_hub != null)
                _hub.LogLine -= Append;
        }

        private void Append(string line)
        {
            if (_logText == null)
                return;

            _buffer.AppendLine(line);
            if (_buffer.Length > MaxChars)
                _buffer.Remove(0, _buffer.Length - MaxChars);

            _logText.text = _buffer.ToString();
            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 0f;
        }

        private void EnsureUi()
        {
            if (_logToggle != null && _restartButton != null && _logText != null)
                return;

            var vertical = gameObject.GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 6;
            vertical.padding = new RectOffset(8, 8, 8, 8);
            vertical.childForceExpandHeight = false;
            vertical.childForceExpandWidth = true;

            var title = CreateText(transform, "Debug", 18);

            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(transform, false);
            row.GetComponent<HorizontalLayoutGroup>().spacing = 8;

            _restartButton = CreateButton(row.transform, "Restart Scene");

            var toggleGo = new GameObject("LogToggle", typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
            toggleGo.transform.SetParent(row.transform, false);
            toggleGo.GetComponent<LayoutElement>().preferredHeight = 28;
            _logToggle = toggleGo.GetComponent<Toggle>();
            var toggleLabel = CreateText(toggleGo.transform, "Network log", 14);
            toggleLabel.color = Color.white;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            scrollGo.transform.SetParent(transform, false);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            scrollGo.GetComponent<LayoutElement>().minHeight = 160;
            scrollGo.GetComponent<LayoutElement>().flexibleHeight = 1;
            _scroll = scrollGo.GetComponent<ScrollRect>();

            var content = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
            content.transform.SetParent(scrollGo.transform, false);
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1f);

            _logText = CreateText(content.transform, string.Empty, 12);
            _logText.alignment = TextAnchor.UpperLeft;
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logText.verticalOverflow = VerticalWrapMode.Overflow;

            _scroll.content = contentRt;
            _scroll.viewport = scrollGo.GetComponent<RectTransform>();
            _scroll.horizontal = false;
            _scroll.vertical = true;

            // silence unused
            _ = title;
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
            return text;
        }

        private static Button CreateButton(Transform parent, string label)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);
            go.GetComponent<LayoutElement>().preferredHeight = 28;
            go.GetComponent<LayoutElement>().preferredWidth = 140;

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
    }
}
