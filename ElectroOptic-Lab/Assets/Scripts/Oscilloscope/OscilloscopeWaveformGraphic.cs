using UnityEngine;
using UnityEngine.UI;

namespace ElectroOptics.Oscilloscope
{
    /// <summary>
    /// Minimal uGUI waveform renderer for oscilloscope channel panels.
    /// </summary>
    public class OscilloscopeWaveformGraphic : Graphic
    {
        [SerializeField] private float lineWidth = 4f;
        [SerializeField] private Vector2 padding = new Vector2(10f, 8f);

        private float[] _samples;
        private float _minY = -1f;
        private float _maxY = 1f;
        private bool _hasSamples;

        public void SetSamples(float[] samples, float minY, float maxY)
        {
            _samples = samples;
            _minY = minY;
            _maxY = maxY;
            _hasSamples = samples != null && samples.Length > 1 && !Mathf.Approximately(minY, maxY);
            SetLayoutDirty();
            SetMaterialDirty();
            SetVerticesDirty();
        }

        public void SetColor(Color newColor)
        {
            color = newColor;
            SetVerticesDirty();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        public void Clear()
        {
            _samples = null;
            _hasSamples = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (!_hasSamples)
                return;

            Rect r = GetPixelAdjustedRect();
            float left = r.xMin + padding.x;
            float right = r.xMax - padding.x;
            float bottom = r.yMin + padding.y;
            float top = r.yMax - padding.y;

            if (right <= left || top <= bottom)
                return;

            Vector2 previous = SampleToPoint(0, left, right, bottom, top);
            for (int i = 1; i < _samples.Length; i++)
            {
                Vector2 current = SampleToPoint(i, left, right, bottom, top);
                AddLine(vh, previous, current, lineWidth, color);
                previous = current;
            }
        }

        private Vector2 SampleToPoint(int index, float left, float right, float bottom, float top)
        {
            float x = Mathf.Lerp(left, right, index / (float)(_samples.Length - 1));
            float normalized = Mathf.InverseLerp(_minY, _maxY, _samples[index]);
            float y = Mathf.Lerp(bottom, top, Mathf.Clamp01(normalized));
            return new Vector2(x, y);
        }

        private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color32 lineColor)
        {
            Vector2 direction = b - a;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
            int start = vh.currentVertCount;

            vh.AddVert(a - normal, lineColor, Vector2.zero);
            vh.AddVert(a + normal, lineColor, Vector2.zero);
            vh.AddVert(b + normal, lineColor, Vector2.zero);
            vh.AddVert(b - normal, lineColor, Vector2.zero);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
