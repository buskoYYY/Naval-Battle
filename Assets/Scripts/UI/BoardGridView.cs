using System;
using NavalBattle.Client;
using NavalBattle.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace NavalBattle.UI
{
    public sealed class BoardGridView : MonoBehaviour
    {
        private CellMark[] _marks = Array.Empty<CellMark>();
        private Button[] _buttons = Array.Empty<Button>();
        private int _size;
        private bool _interactable;
        private Action<int, int> _onClick;

        public void Build(Transform parent, int size, float cellSize, Action<int, int> onClick)
        {
            _size = size;
            _onClick = onClick;
            _buttons = new Button[size * size];
            _marks = new CellMark[size * size];

            var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(parent, false);
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = size;
            grid.cellSize = new Vector2(cellSize, cellSize);
            grid.spacing = new Vector2(2f, 2f);

            var rt = gridGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size * (cellSize + 2f), size * (cellSize + 2f));

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var cx = x;
                var cy = y;
                var btnGo = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(gridGo.transform, false);
                var image = btnGo.GetComponent<Image>();
                image.color = ColorFor(CellMark.Unknown);
                var button = btnGo.GetComponent<Button>();
                button.onClick.AddListener(() => _onClick?.Invoke(cx, cy));

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelGo.transform.SetParent(btnGo.transform, false);
                var text = labelGo.GetComponent<Text>();
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.black;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 12;
                text.raycastTarget = false;
                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;

                _buttons[y * size + x] = button;
                _marks[y * size + x] = CellMark.Unknown;
            }
        }

        public void SetInteractable(bool value)
        {
            _interactable = value;
            RefreshInteractable();
        }

        public void Render(CellMark[] marks)
        {
            if (marks == null || marks.Length != _buttons.Length)
                return;

            for (var i = 0; i < marks.Length; i++)
            {
                _marks[i] = marks[i];
                var image = _buttons[i].GetComponent<Image>();
                image.color = ColorFor(marks[i]);
                var text = _buttons[i].GetComponentInChildren<Text>();
                text.text = LabelFor(marks[i]);
            }

            RefreshInteractable();
        }

        private void RefreshInteractable()
        {
            for (var i = 0; i < _buttons.Length; i++)
            {
                var mark = _marks[i];
                var canClick = _interactable &&
                               mark is CellMark.Unknown or CellMark.Empty;
                _buttons[i].interactable = canClick;
            }
        }

        private static Color ColorFor(CellMark mark) => mark switch
        {
            CellMark.Ship => new Color(0.2f, 0.55f, 0.2f),
            CellMark.Miss => new Color(0.55f, 0.7f, 0.9f),
            CellMark.Hit => new Color(0.9f, 0.45f, 0.2f),
            CellMark.Sunk => new Color(0.7f, 0.1f, 0.1f),
            CellMark.Pending => new Color(0.95f, 0.9f, 0.3f),
            CellMark.Empty => new Color(0.75f, 0.85f, 0.95f),
            _ => new Color(0.85f, 0.85f, 0.85f)
        };

        private static string LabelFor(CellMark mark) => mark switch
        {
            CellMark.Ship => "S",
            CellMark.Miss => "·",
            CellMark.Hit => "X",
            CellMark.Sunk => "#",
            CellMark.Pending => "?",
            _ => string.Empty
        };
    }
}
