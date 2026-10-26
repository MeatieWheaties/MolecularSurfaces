using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MolecularSurfaces.Presentation
{
    [ExecuteAlways]
    public class ScreenCornerText : MonoBehaviour
    {
        public string TopLeft = "Top Left";
        public string TopRight = "Top Right";
        public string BottomLeft = "Bottom Left";
        public string BottomRight = "Bottom Right";

        [Min(0f)] public float Padding = 24f;
        [Min(1f)] public float FontSize = 24f;
        public Color Color = Color.white;

        private const string CanvasName = "[Corner Text Canvas]";
        private Canvas _canvas;
        private TextMeshProUGUI _topLeft;
        private TextMeshProUGUI _topRight;
        private TextMeshProUGUI _bottomLeft;
        private TextMeshProUGUI _bottomRight;

        private void OnEnable()
        {
            EnsureCanvas();
            Refresh();
        }

        private void OnValidate()
        {
            if (_canvas != null)
                Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void EnsureCanvas()
        {
            var canvasTransform = transform.Find(CanvasName);
            if (canvasTransform == null)
            {
                var canvasObject = new GameObject(
                    CanvasName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                canvasTransform = canvasObject.transform;
                canvasTransform.SetParent(transform, false);
            }

            _canvas = canvasTransform.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasTransform.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _topLeft = EnsureLabel(canvasTransform, "Top Left", new Vector2(0f, 1f), TextAlignmentOptions.TopLeft);
            _topRight = EnsureLabel(canvasTransform, "Top Right", new Vector2(1f, 1f), TextAlignmentOptions.TopRight);
            _bottomLeft = EnsureLabel(canvasTransform, "Bottom Left", new Vector2(0f, 0f), TextAlignmentOptions.BottomLeft);
            _bottomRight = EnsureLabel(canvasTransform, "Bottom Right", new Vector2(1f, 0f), TextAlignmentOptions.BottomLeft);
        }

        private static TextMeshProUGUI EnsureLabel(
            Transform parent,
            string name,
            Vector2 anchor,
            TextAlignmentOptions alignment)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                child = labelObject.transform;
                child.SetParent(parent, false);
            }

            var rect = (RectTransform)child;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = new Vector2(800f, 120f);

            var label = child.GetComponent<TextMeshProUGUI>();
            label.alignment = alignment;
            label.raycastTarget = false;
            label.enableWordWrapping = true;
            return label;
        }

        private void Refresh()
        {
            if (_canvas == null || _topLeft == null)
                EnsureCanvas();

            Configure(_topLeft, TopLeft, new Vector2(Padding, -Padding));
            Configure(_topRight, TopRight, new Vector2(-Padding, -Padding));
            Configure(_bottomLeft, BottomLeft, new Vector2(Padding, Padding));
            Configure(_bottomRight, BottomRight, new Vector2(-Padding, Padding));
        }

        private void Configure(TextMeshProUGUI label, string value, Vector2 position)
        {
            label.text = value;
            label.fontSize = FontSize;
            label.color = Color;
            label.rectTransform.anchoredPosition = position;
        }
    }
}
