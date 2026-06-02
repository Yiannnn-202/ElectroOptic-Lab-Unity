using ElectroOptics.ConoscopicAnalysis;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class AdditionalConoscopicSurfaceView : MonoBehaviour
{
    private const string VertexColorShaderName = "ElectroOptics/ConoscopicIntensityVertexColor";
    private const float EoSmoothMinFieldVm = 5000000f;

    [Header("Source")]
    [SerializeField] private ConoscopicJonesGpuCore core;
    [SerializeField] private AdditionalConoscopicVisualizationSettings visualizationSettings;

    [Header("Output")]
    [SerializeField] private RawImage outputImage;
    [SerializeField] private Camera renderCamera;
    [SerializeField] private int outputTextureSize = 1024;

    [Header("Surface Preset")]
    [SerializeField] private float surfaceSize = 5f;
    [SerializeField] private float heightScale = 1.6f;
    [SerializeField] private bool normalizeDisplayIntensity = false;

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;
    private Texture2D _readbackTexture;
    private RenderTexture _renderTexture;

    public RenderTexture RenderTexture => _renderTexture;
    public Camera RenderCamera => renderCamera;
    public AdditionalConoscopicVisualizationSettings VisualizationSettings => visualizationSettings;
    public int EffectiveOutputTextureSize => Mathf.Clamp(
        visualizationSettings != null ? visualizationSettings.OutputTextureSize : outputTextureSize,
        256,
        2048);
    public float EffectiveSurfaceSize => visualizationSettings != null ? visualizationSettings.SurfaceSize : surfaceSize;
    public float EffectiveHeightScale => visualizationSettings != null ? visualizationSettings.HeightScale : heightScale;
    public bool EffectiveNormalizeDisplayIntensity => visualizationSettings != null
        ? visualizationSettings.NormalizeDisplayIntensity
        : normalizeDisplayIntensity;

    public float ResolveEffectiveHeightScale(ConoscopicJonesParameters parameters)
    {
        return visualizationSettings != null
            ? visualizationSettings.ResolveHeightScale(parameters)
            : heightScale;
    }

    private void Awake()
    {
        EnsureComponents();
        EnsureRenderOutput();
    }

    private void OnEnable()
    {
        EnsureComponents();
        EnsureRenderOutput();
        SubscribeCore();
    }

    private void OnDisable()
    {
        UnsubscribeCore();
    }

    private void OnDestroy()
    {
        UnsubscribeCore();
        if (_readbackTexture != null)
        {
            DestroyImmediateSafe(_readbackTexture);
            _readbackTexture = null;
        }

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            DestroyImmediateSafe(_renderTexture);
            _renderTexture = null;
        }
    }

    public void Configure(ConoscopicJonesGpuCore sourceCore, RawImage targetImage, Camera targetCamera)
    {
        Configure(sourceCore, targetImage, targetCamera, visualizationSettings);
    }

    public void Configure(
        ConoscopicJonesGpuCore sourceCore,
        RawImage targetImage,
        Camera targetCamera,
        AdditionalConoscopicVisualizationSettings settings)
    {
        SetCore(sourceCore);
        outputImage = targetImage;
        if (settings != null)
        {
            visualizationSettings = settings;
        }
        if (targetCamera != null)
        {
            renderCamera = targetCamera;
        }
        EnsureRenderOutput();
    }

    public void SetVisualizationSettings(AdditionalConoscopicVisualizationSettings settings)
    {
        visualizationSettings = settings;
        EnsureRenderOutput();
    }

    public void SetCore(ConoscopicJonesGpuCore sourceCore)
    {
        if (core == sourceCore)
        {
            return;
        }

        UnsubscribeCore();
        core = sourceCore;
        SubscribeCore();
    }

    public void RebuildFromCurrentResult()
    {
        if (core == null)
        {
            return;
        }

        RebuildMesh(core.Result);
        RenderNow();
    }

    private void SubscribeCore()
    {
        if (core == null || !isActiveAndEnabled)
        {
            return;
        }

        core.OnJonesIntensityUpdated -= HandleJonesIntensityUpdated;
        core.OnJonesIntensityUpdated += HandleJonesIntensityUpdated;
    }

    private void UnsubscribeCore()
    {
        if (core != null)
        {
            core.OnJonesIntensityUpdated -= HandleJonesIntensityUpdated;
        }
    }

    private void HandleJonesIntensityUpdated(ConoscopicJonesResult result)
    {
        RebuildMesh(result);
        RenderNow();
    }

    private void EnsureComponents()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "Additional Conoscopic Jones Surface" };
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _meshFilter.sharedMesh = _mesh;
        }

        if (_meshRenderer.sharedMaterial == null)
        {
            Shader shader = Shader.Find(VertexColorShaderName);
            if (shader != null)
            {
                _meshRenderer.sharedMaterial = new Material(shader)
                {
                    name = "Additional Conoscopic Vertex Color"
                };
            }
        }
    }

    private void EnsureRenderOutput()
    {
        int targetTextureSize = EffectiveOutputTextureSize;
        if (visualizationSettings == null)
        {
            outputTextureSize = targetTextureSize;
        }

        if (_renderTexture == null
            || _renderTexture.width != targetTextureSize
            || _renderTexture.height != targetTextureSize)
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                DestroyImmediateSafe(_renderTexture);
            }

            _renderTexture = new RenderTexture(targetTextureSize, targetTextureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "Additional Conoscopic Surface View",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _renderTexture.Create();
        }

        if (renderCamera == null)
        {
            renderCamera = CreateRenderCamera();
        }

        if (renderCamera != null)
        {
            renderCamera.targetTexture = _renderTexture;
        }

        if (outputImage != null)
        {
            outputImage.texture = _renderTexture;
            outputImage.color = Color.white;
        }
    }

    private Camera CreateRenderCamera()
    {
        GameObject cameraObject = new GameObject("Additional Conoscopic Surface Camera");
        cameraObject.transform.position = new Vector3(0f, 4.6f, -6.2f);
        cameraObject.transform.rotation = Quaternion.Euler(48f, 0f, 0f);

        Camera cameraComponent = cameraObject.AddComponent<Camera>();
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.backgroundColor = new Color(0.025f, 0.04f, 0.045f, 1f);
        cameraComponent.nearClipPlane = 0.1f;
        cameraComponent.farClipPlane = 100f;
        cameraComponent.enabled = true;
        return cameraComponent;
    }

    private void RenderNow()
    {
        EnsureRenderOutput();
        if (renderCamera != null && renderCamera.isActiveAndEnabled)
        {
            renderCamera.Render();
        }
    }

    private void RebuildMesh(ConoscopicJonesResult result)
    {
        if (result == null || !result.IsValid || result.IntensityHeightMap == null)
        {
            return;
        }

        EnsureComponents();
        Color[] pixels = ReadIntensityPixels(result.IntensityHeightMap);
        if (pixels == null || pixels.Length == 0)
        {
            return;
        }

        int resolution = result.IntensityHeightMap.width;
        float displayMin = result.MinIntensity;
        float displayMax = result.MaxIntensity;
        float displayRange = displayMax - displayMin;
        bool normalizeDisplay = EffectiveNormalizeDisplayIntensity && displayRange > 0.00001f;
        bool smoothRawJonesDisplay = ShouldSmoothRawJonesDisplay(result.ParametersSnapshot);
        float targetSurfaceSize = EffectiveSurfaceSize;
        float targetHeightScale = ResolveEffectiveHeightScale(result.ParametersSnapshot);
        float[] displayValues = new float[pixels.Length];

        for (int i = 0; i < pixels.Length; i++)
        {
            float intensity = Mathf.Clamp01(pixels[i].r);
            displayValues[i] = normalizeDisplay
                ? Mathf.Clamp01((intensity - displayMin) / displayRange)
                : intensity;
        }

        if (smoothRawJonesDisplay)
        {
            displayValues = SmoothDisplayValues(displayValues, resolution);
        }

        int vertexCount = resolution * resolution;
        var vertices = new Vector3[vertexCount];
        var colors = new Color[vertexCount];
        var uvs = new Vector2[vertexCount];
        int quadCount = (resolution - 1) * (resolution - 1);
        var triangles = new int[quadCount * 6];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = y * resolution + x;
                Vector2 coordinate = ConoscopicJonesCpuReference.GetCoordinate(x, y, resolution);
                float displayIntensity = displayValues[index];
                vertices[index] = new Vector3(
                    coordinate.x * targetSurfaceSize * 0.5f,
                    displayIntensity * targetHeightScale,
                    coordinate.y * targetSurfaceSize * 0.5f);
                uvs[index] = new Vector2(x / (float)(resolution - 1), y / (float)(resolution - 1));
                colors[index] = EvaluateHeatColor(displayIntensity);
            }
        }

        int t = 0;
        for (int y = 0; y < resolution - 1; y++)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                int i0 = y * resolution + x;
                int i1 = i0 + 1;
                int i2 = i0 + resolution;
                int i3 = i2 + 1;
                triangles[t++] = i0;
                triangles[t++] = i2;
                triangles[t++] = i1;
                triangles[t++] = i1;
                triangles[t++] = i2;
                triangles[t++] = i3;
            }
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.colors = colors;
        _mesh.uv = uvs;
        _mesh.triangles = triangles;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    private Color[] ReadIntensityPixels(RenderTexture texture)
    {
        int resolution = texture.width;
        if (_readbackTexture == null || _readbackTexture.width != resolution || _readbackTexture.height != resolution)
        {
            if (_readbackTexture != null)
            {
                DestroyImmediateSafe(_readbackTexture);
            }

            _readbackTexture = new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        try
        {
            _readbackTexture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            _readbackTexture.Apply(false, false);
            return _readbackTexture.GetPixels();
        }
        finally
        {
            RenderTexture.active = previous;
        }
    }

    private static bool ShouldSmoothRawJonesDisplay(ConoscopicJonesParameters parameters)
    {
        return parameters != null
               && parameters.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.RawJones
               && (Mathf.Abs(parameters.electricFieldStrength) >= EoSmoothMinFieldVm || parameters.phaseScale <= 0.05f);
    }

    private static Color EvaluateHeatColor(float value)
    {
        value = Mathf.Clamp01(value);
        Color c0 = new Color(0.02f, 0.05f, 0.45f, 1f);
        Color c1 = new Color(0.0f, 0.65f, 1f, 1f);
        Color c2 = new Color(0.1f, 0.85f, 0.2f, 1f);
        Color c3 = new Color(1f, 0.88f, 0.05f, 1f);
        Color c4 = new Color(1f, 0.18f, 0.04f, 1f);

        if (value < 0.25f) return Color.Lerp(c0, c1, value / 0.25f);
        if (value < 0.5f) return Color.Lerp(c1, c2, (value - 0.25f) / 0.25f);
        if (value < 0.75f) return Color.Lerp(c2, c3, (value - 0.5f) / 0.25f);
        return Color.Lerp(c3, c4, (value - 0.75f) / 0.25f);
    }

    private static float[] SmoothDisplayValues(float[] values, int resolution)
    {
        var smoothed = new float[values.Length];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float weightedSum = 0f;
                float weightSum = 0f;
                for (int oy = -1; oy <= 1; oy++)
                {
                    int sy = Mathf.Clamp(y + oy, 0, resolution - 1);
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int sx = Mathf.Clamp(x + ox, 0, resolution - 1);
                        float weight = ox == 0 && oy == 0 ? 4f : (ox == 0 || oy == 0 ? 2f : 1f);
                        weightedSum += values[sy * resolution + sx] * weight;
                        weightSum += weight;
                    }
                }

                smoothed[y * resolution + x] = Mathf.Clamp01(weightedSum / Mathf.Max(weightSum, 0.0001f));
            }
        }

        return smoothed;
    }

    private static void DestroyImmediateSafe(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        outputTextureSize = Mathf.Clamp(outputTextureSize, 256, 2048);
        surfaceSize = Mathf.Max(0.1f, surfaceSize);
        heightScale = Mathf.Max(0.05f, heightScale);
    }
#endif
}
