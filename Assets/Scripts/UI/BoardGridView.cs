using System;
using NavalBattle.Client;
using NavalBattle.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace NavalBattle.UI
{
    public sealed class BoardGridView : MonoBehaviour
    {
        [SerializeField] private GridLayoutGroup _grid;
        [SerializeField] private Transform _root;

        private Button[] _buttons = Array.Empty<Button>();
        private Text[] _labels = Array.Empty<Text>();
        private int _size;
        private bool _interactable;
        private Action<int, int> _onClick;

        public int BuiltSize => _size;

        public void Build(int size, Action<int, int> onClick)
        {
            _size = size;
            _onClick = onClick;
            Clear();

            if (_grid == null)
                _grid = GetComponent<GridLayoutGroup>();

            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = size;
            _grid.cellSize = new Vector2(36, 36);
            _grid.spacing = new Vector2(2, 2);

            var count = size * size;
            _buttons = new Button[count];
            _labels = new Text[count];

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var index = y * size + x;
                var go = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_root != null ? _root : transform, false);

                var image = go.GetComponent<Image>();
                image.color = Color.white;

                var button = go.GetComponent<Button>();
                var capturedX = x;
                var capturedY = y;
                button.onClick.AddListener(() => _onClick?.Invoke(capturedX, capturedY));

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelGo.transform.SetParent(go.transform, false);
                var label = labelGo.GetComponent<Text>();
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.black;
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 14;
                label.text = string.Empty;
                var labelRt = label.GetComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;

                _buttons[index] = button;
                _labels[index] = label;
            }
        }

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            foreach (var button in _buttons)
            {
                if (button != null)
                    button.interactable = interactable;
            }
        }

        public void Render(CellMark[] marks, bool showShips)
        {
            if (marks == null || marks.Length != _buttons.Length)
                return;

            for (var i = 0; i < marks.Length; i++)
            {
                var mark = marks[i];
                var (text, color) = Describe(mark, showShips);
                _labels[i].text = text;
                _buttons[i].image.color = color;

                var locked = mark is CellMark.Miss or CellMark.Hit or CellMark.Sunk or CellMark.Pending;
                _buttons[i].interactable = _interactable && !locked;
            }
        }

        private static (string text, Color color) Describe(CellMark mark, bool showShips)
        {
            return mark switch
            {
                CellMark.Ship when showShips => ("S", new Color(0.4f, 0.7f, 1f)),
                CellMark.Miss => ("·", new Color(0.75f, 0.85f, 1f)),
                CellMark.Hit => ("X", new Color(1f, 0.6f, 0.3f)),
                CellMark.Sunk => ("#", new Color(1f, 0.25f, 0.25f)),
                CellMark.Pending => ("?", new Color(1f, 1f, 0.4f)),
                CellMark.Unknown => ("", Color.white),
                _ => ("", new Color(0.92f, 0.92f, 0.95f))
            };
        }

        private void Clear()
        {
            var parent = _root != null ? _root : transform;
            for (var i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }
    }
}
