using System.Collections.Generic;
using System.Text;
using NavalBattle.Transport;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NavalBattle.UI
{
    public sealed class DebugPanelView : MonoBehaviour
    {
        private Toggle _logToggle;
        private Button _restartButton;
        private Text _logText;
        private ScrollRect _scroll;
        private RectTransform _contentRt;

        private readonly List<string> _lines = new();
        private readonly StringBuilder _buffer = new();
        private InProcessTransportHub _hub;
        private bool _uiBuilt;

        private const int MaxLines = 200;

        public void Bind(InProcessTransportHub hub)
        {
            _hub = hub;
            EnsureUi();

            _hub.LogLine += Append;
            _logToggle.isOn = hub.LogEnabled;
            _logToggle.onValueChanged.AddListener(OnLogToggled);
            _restartButton.onClick.AddListener(RestartScene);
            RefreshText();
        }

        public void Unbind()
        {
            if (_hub != null)
                _hub.LogLine -= Append;

            if (_logToggle != null)
                _logToggle.onValueChanged.RemoveListener(OnLogToggled);

            if (_restartButton != null)
                _restartButton.onClick.RemoveListener(RestartScene);
        }

        private void OnLogToggled(bool enabled)
        {
            if (_hub != null)
                _hub.LogEnabled = enabled;
        }

        private static void RestartScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void Append(string line)
        {
            if (_logText == null)
                return;

            _lines.Add(line);
            while (_lines.Count > MaxLines)
                _lines.RemoveAt(0);

            RefreshText();
            Canvas.ForceUpdateCanvases();
            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 0f;
        }

        private void RefreshText()
        {
            _buffer.Clear();
            for (var i = 0; i < _lines.Count; i++)
            {
                if (i > 0)
                    _buffer.Append('\n');
                _buffer.Append(_lines[i]);
            }

            if (_lines.Count == 0)
                _buffer.Append(_hub != null && _hub.LogEnabled
                    ? "Network log is on. Messages will appear here."
                    : "Network log is off.");

            _logText.text = _buffer.ToString();
        }

        private void EnsureUi()
        {
            if (_uiBuilt)
                return;

            _uiBuilt = true;

            var vertical = gameObject.GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 8;
            vertical.padding = new RectOffset(10, 10, 10, 10);
            vertical.childForceExpandHeight = false;
            vertical.childForceExpandWidth = true;
            vertical.childControlHeight = true;
            vertical.childControlWidth = true;

            CreateLabel(transform, "Debug", 18, FontStyle.Bold);

            var row = new GameObject("Toolbar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(transform, false);
            row.GetComponent<LayoutElement>().preferredHeight = 32;
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            _restartButton = CreateButton(row.transform, "Restart Scene", 150);
            _logToggle = CreateToggle(row.transform, "Network log");

            CreateLogScroll();
        }

        private void CreateLogScroll()
        {
            var scrollGo = new GameObject("LogScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            scrollGo.transform.SetParent(transform, false);
            var scrollImage = scrollGo.GetComponent<Image>();
            scrollImage.color = new Color(0.05f, 0.05f, 0.07f, 0.95f);
            var scrollLe = scrollGo.GetComponent<LayoutElement>();
            scrollLe.minHeight = 220;
            scrollLe.flexibleHeight = 1;
            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            Stretch(viewportRt);
            viewportRt.offsetMin = new Vector2(6, 6);
            viewportRt.offsetMax = new Vector2(-6, -6);
            var viewportImage = viewportGo.GetComponent<Image>();
            viewportImage.color = Color.white;
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _contentRt = contentGo.GetComponent<RectTransform>();
            _contentRt.anchorMin = new Vector2(0f, 1f);
            _contentRt.anchorMax = new Vector2(1f, 1f);
            _contentRt.pivot = new Vector2(0.5f, 1f);
            _contentRt.anchoredPosition = Vector2.zero;
            _contentRt.sizeDelta = new Vector2(0f, 0f);
            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("LogText", typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
            textGo.transform.SetParent(contentGo.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 1f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.pivot = new Vector2(0.5f, 1f);
            textRt.anchoredPosition = Vector2.zero;
            textRt.sizeDelta = new Vector2(0f, 0f);

            _logText = textGo.GetComponent<Text>();
            _logText.font = GetUiFont();
            _logText.fontSize = 12;
            _logText.color = new Color(0.85f, 0.9f, 0.85f);
            _logText.alignment = TextAnchor.UpperLeft;
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logText.verticalOverflow = VerticalWrapMode.Overflow;
            _logText.raycastTarget = false;

            var textFitter = textGo.GetComponent<ContentSizeFitter>();
            textFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll.viewport = viewportRt;
            _scroll.content = _contentRt;
        }

        private static Toggle CreateToggle(Transform parent, string label)
        {
            var root = new GameObject(label, typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<LayoutElement>().preferredWidth = 160;
            root.GetComponent<LayoutElement>().preferredHeight = 28;

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(0f, 0.5f);
            bgRt.pivot = new Vector2(0f, 0.5f);
            bgRt.sizeDelta = new Vector2(20f, 20f);
            bgRt.anchoredPosition = new Vector2(0f, 0f);
            bg.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f);

            var check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(bg.transform, false);
            var checkRt = check.GetComponent<RectTransform>();
            Stretch(checkRt);
            checkRt.offsetMin = new Vector2(3, 3);
            checkRt.offsetMax = new Vector2(-3, -3);
            check.GetComponent<Image>().color = new Color(0.15f, 0.55f, 0.25f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(root.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(26, 0);
            labelRt.offsetMax = Vector2.zero;
            var labelText = labelGo.GetComponent<Text>();
            labelText.text = label;
            labelText.font = GetUiFont();
            labelText.fontSize = 14;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;

            var toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = bg.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.isOn = true;
            return toggle;
        }

        private static Button CreateButton(Transform parent, string label, float width)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.28f, 0.32f, 0.4f);
            go.GetComponent<LayoutElement>().preferredHeight = 28;
            go.GetComponent<LayoutElement>().preferredWidth = width;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = GetUiFont();
            text.fontSize = 13;
            Stretch(text.rectTransform);

            return go.GetComponent<Button>();
        }

        private static Text CreateLabel(Transform parent, string value, int size, FontStyle style)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = size + 10;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = GetUiFont();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Font GetUiFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
                return font;
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
