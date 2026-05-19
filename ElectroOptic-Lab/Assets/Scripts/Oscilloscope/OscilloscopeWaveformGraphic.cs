using UnityEngine;
using UnityEngine.UI;

namespace ElectroOptics.Oscilloscope
{
    /// <summary>
    /// GPU waveform renderer using RawImage + custom antialiased glow shader.
    /// Waveform float[] data is packed into a 1D Texture2D and sampled in the shader.
    /// </summary>
    public class OscilloscopeWaveformGraphic : RawImage
    {
        private static Shader _cachedShader;
        private static Shader WaveformShader
        {
            get
            {
                if (_cachedShader == null)
                    _cachedShader = Shader.Find("UI/WaveformLine");
                return _cachedShader;
            }
        }

        [SerializeField] private float lineWidth = 2.5f;
        [SerializeField] private float glowWidth = 6f;
        [SerializeField] private float intensity = 1.2f;
        [SerializeField] [Range(0f, 0.45f)] private float verticalPadding = 0.06f;

        private Texture2D _waveTexture;
        private Material _runtimeMaterial;
        private Color _pendingColor = Color.white;
        private float[] _samples;
        private float _minY = -1f;
        private float _maxY = 1f;
        private bool _hasSamples;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            color = new Color(1f, 1f, 1f, 0f); // hidden until first SetSamples
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_runtimeMaterial != null)
                DestroyImmediate(_runtimeMaterial);
            if (_waveTexture != null)
                DestroyImmediate(_waveTexture);
        }

        public void SetSamples(float[] samples, float minY, float maxY)
        {
            _samples = samples;
            _minY = minY;
            _maxY = maxY;
            _hasSamples = samples != null && samples.Length > 1 && !Mathf.Approximately(minY, maxY);

            if (!_hasSamples)
            {
                color = new Color(color.r, color.g, color.b, 0f);
                return;
            }

            EnsureMaterial();
            EnsureTexture(samples.Length);
            WriteSamplesToTexture(samples, minY, maxY);

            _runtimeMaterial.SetTexture("_MainTex", _waveTexture);
            color = new Color(color.r, color.g, color.b, 1f);
            SetMaterialDirty();
        }

        public void SetColor(Color newColor)
        {
            _pendingColor = newColor;
            color = new Color(newColor.r, newColor.g, newColor.b, _hasSamples ? 1f : 0f);
            if (_runtimeMaterial != null)
                _runtimeMaterial.SetColor("_LineColor", newColor);
        }

        public new void Clear()
        {
            _samples = null;
            _hasSamples = false;
            color = new Color(color.r, color.g, color.b, 0f);
        }

        private void EnsureMaterial()
        {
            if (_runtimeMaterial != null)
                return;

            var shader = WaveformShader;
            if (shader == null)
            {
                Debug.LogError("[OscilloscopeWaveformGraphic] Shader 'UI/WaveformLine' not found.");
                return;
            }

            _runtimeMaterial = new Material(shader);
            _runtimeMaterial.SetFloat("_LineWidth", lineWidth);
            _runtimeMaterial.SetFloat("_GlowWidth", glowWidth);
            _runtimeMaterial.SetFloat("_Intensity", intensity);
            _runtimeMaterial.SetColor("_LineColor", _pendingColor);
            material = _runtimeMaterial;
        }

        private void EnsureTexture(int width)
        {
            if (_waveTexture != null && _waveTexture.width == width)
                return;

            if (_waveTexture != null)
                DestroyImmediate(_waveTexture);

            _waveTexture = new Texture2D(width, 1, TextureFormat.RGBA32, false, true);
            _waveTexture.filterMode = FilterMode.Bilinear;
            _waveTexture.wrapMode = TextureWrapMode.Clamp;
        }

        private void WriteSamplesToTexture(float[] samples, float minY, float maxY)
        {
            float pad = Mathf.Clamp(verticalPadding, 0f, 0.45f);
            var colors = new Color[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                float normalized = Mathf.InverseLerp(minY, maxY, samples[i]);
                float padded = pad + normalized * (1f - pad * 2f);
                colors[i] = new Color(padded, 0f, 0f, 1f);
            }

            _waveTexture.SetPixels(colors);
            _waveTexture.Apply();
        }
    }
}
