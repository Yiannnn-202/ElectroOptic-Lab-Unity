using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.ConoscopicAnalysis
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ConoscopicJonesSurfaceVisualizer : MonoBehaviour
    {
        private const string VertexColorShaderName = "ElectroOptics/ConoscopicIntensityVertexColor";

        [SerializeField] private ConoscopicJonesGpuCore _core;
        [SerializeField] private CrystalProfile _profile;
        [SerializeField] [Range(16, 256)] private int _resolution = 128;
        [SerializeField] private float _surfaceSize = 5f;
        [SerializeField] private float _heightScale = 1.6f;
        [SerializeField] private bool _normalizeDisplayIntensity = true;
        [SerializeField] private bool _recalculateOnStart = true;

        [Header("Runtime Demo Panel")]
        public CrystalProfile liNbO3Profile;
        public CrystalProfile ktpProfile;
        public TextMesh statusText;
        public Transform surfaceRoot;
        [SerializeField] private bool _showPanel = true;
        [SerializeField] private Rect _panelRect = new Rect(16f, 16f, 390f, 760f);

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Texture2D _readbackTexture;
        private int _profileIndex;
        private float _surfaceYaw;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnEnable()
        {
            EnsureComponents();
            EnsureCore();
            _core.OnJonesIntensityUpdated -= HandleJonesIntensityUpdated;
            _core.OnJonesIntensityUpdated += HandleJonesIntensityUpdated;
        }

        private void Start()
        {
            EnsureCore();
            if (_profile != null)
            {
                _core.SetProfile(_profile);
            }

            _core.SetResolution(_resolution);
            if (_recalculateOnStart)
            {
                _core.ForceRecalculate();
            }
        }

        private void OnDisable()
        {
            if (_core != null)
            {
                _core.OnJonesIntensityUpdated -= HandleJonesIntensityUpdated;
            }
        }

        private void OnDestroy()
        {
            if (_readbackTexture != null)
            {
                DestroyImmediateSafe(_readbackTexture);
                _readbackTexture = null;
            }
        }

        public void SetCore(ConoscopicJonesGpuCore core)
        {
            if (_core == core)
            {
                return;
            }

            if (_core != null)
            {
                _core.OnJonesIntensityUpdated -= HandleJonesIntensityUpdated;
            }

            _core = core;
            if (_core != null && isActiveAndEnabled)
            {
                _core.OnJonesIntensityUpdated += HandleJonesIntensityUpdated;
            }
        }

        public void SetProfile(CrystalProfile profile)
        {
            _profile = profile;
            if (_core != null)
            {
                _core.SetProfile(profile);
            }
        }

        public void SetResolution(int resolution)
        {
            _resolution = Mathf.Clamp(resolution, 16, 256);
            if (_core != null)
            {
                _core.SetResolution(_resolution);
            }
        }

        private void EnsureCore()
        {
            if (_core == null)
            {
                _core = GetComponent<ConoscopicJonesGpuCore>();
                if (_core == null)
                {
                    _core = gameObject.AddComponent<ConoscopicJonesGpuCore>();
                }
            }
        }

        private void EnsureComponents()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Conoscopic Jones Intensity Surface" };
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
                        name = "Conoscopic Jones Vertex Color"
                    };
                }
            }
        }

        private void HandleJonesIntensityUpdated(ConoscopicJonesResult result)
        {
            RebuildMesh(result);
            UpdateStatus();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!_showPanel)
            {
                if (GUI.Button(new Rect(16f, 16f, 120f, 28f), "Show Panel"))
                {
                    _showPanel = true;
                }

                return;
            }

            _panelRect = GUI.Window(GetInstanceID(), _panelRect, DrawPanel, "Jones Conoscopic Controls");
        }

        private void DrawPanel(int windowId)
        {
            EnsureCore();
            if (_core == null)
            {
                GUILayout.Label("Core not ready");
                GUI.DragWindow();
                return;
            }

            var p = new ConoscopicJonesParameters(_core.Parameters);
            bool changed = false;
            float yaw = _surfaceYaw;
            float height = _heightScale;

            GUILayout.Label($"Profile: {(_core.Profile != null ? _core.Profile.crystalName : "manual")}");
            GUILayout.Label($"Class: {p.CrystalOpticClass}  n=({p.principalIndexNx:F4}, {p.principalIndexNy:F4}, {p.principalIndexNz:F4})");
            GUILayout.Label($"Valid: {_core.Result.IsValid}  Min: {_core.Result.MinIntensity:F3}  Max: {_core.Result.MaxIntensity:F3}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("LiNbO3"))
            {
                ApplyDemoProfile(0);
                p = new ConoscopicJonesParameters(_core.Parameters);
                changed = true;
            }
            if (GUILayout.Button("KTP"))
            {
                ApplyDemoProfile(1);
                p = new ConoscopicJonesParameters(_core.Parameters);
                changed = true;
            }
            if (GUILayout.Button("Reset"))
            {
                p = new ConoscopicJonesParameters();
                _core.SetParameters(p);
                ApplyDemoProfile(_profileIndex);
                changed = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            changed |= SliderRow("Wavelength nm", ref p.wavelengthNm, 400f, 800f, "F1");
            changed |= SliderRow("Thickness mm", ref p.thicknessMm, 0.1f, 60f, "F2");
            changed |= SliderRow("no", ref p.ordinaryIndexNo, 1.2f, 3.5f, "F4");
            changed |= SliderRow("ne", ref p.extraordinaryIndexNe, 1.2f, 3.5f, "F4");
            changed |= SliderRow("nx", ref p.principalIndexNx, 1.2f, 3.5f, "F4");
            changed |= SliderRow("ny", ref p.principalIndexNy, 1.2f, 3.5f, "F4");
            changed |= SliderRow("nz", ref p.principalIndexNz, 1.2f, 3.5f, "F4");
            changed |= SliderRow("Screen m", ref p.screenDistanceM, 0.05f, 2.0f, "F2");
            changed |= SliderRow("Screen Half m", ref p.screenHalfSizeM, ConoscopicJonesParameters.MinScreenHalfSizeM, ConoscopicJonesParameters.MaxScreenHalfSizeM, "F3");
            changed |= SliderRow("I0", ref p.initialIntensity, 0f, 2f, "F2");
            changed |= SliderRow("AA Strength", ref p.phaseAntiAliasStrength, ConoscopicJonesParameters.MinPhaseAntiAliasStrength, ConoscopicJonesParameters.MaxPhaseAntiAliasStrength, "F2");
            changed |= SliderRow("Polarizer", ref p.polarizerAngleDeg, 0f, 180f, "F0");
            changed |= SliderRow("Analyzer", ref p.analyzerAngleDeg, 0f, 180f, "F0");
            changed |= SliderRow("Crystal Axis", ref p.crystalAxisAngleDeg, 0f, 180f, "F0");
            changed |= SliderRow("Optic Tilt", ref p.opticAxisTiltDeg, ConoscopicJonesParameters.MinOpticAxisTiltDeg, ConoscopicJonesParameters.MaxOpticAxisTiltDeg, "F1");
            changed |= SliderRow("Optic Azimuth", ref p.opticAxisAzimuthDeg, 0f, 180f, "F0");
            changed |= SliderRow("E Field", ref p.electricFieldStrength, -5000f, 5000f, "F0");
            changed |= SliderRow("r22", ref p.electroOpticCoefficientR22, -50f, 50f, "F2");
            changed |= SliderRow("Aperture", ref p.apertureRadius, ConoscopicJonesParameters.MinApertureRadius, 1f, "F2");

            float resolution = p.resolution;
            if (SliderRow("Resolution", ref resolution, 16f, 256f, "F0"))
            {
                p.resolution = Mathf.RoundToInt(resolution);
                changed = true;
            }

            bool heightChanged = SliderRow("Height", ref height, 0.05f, 5f, "F2");
            bool yawChanged = SliderRow("View Yaw", ref yaw, -180f, 180f, "F0");
            bool normalizeDisplay = GUILayout.Toggle(_normalizeDisplayIntensity, "Normalize Display");

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Recalculate"))
            {
                changed = true;
            }
            if (GUILayout.Button("Orthogonal"))
            {
                p.polarizerAngleDeg = 0f;
                p.analyzerAngleDeg = 90f;
                changed = true;
            }
            if (GUILayout.Button("Hide"))
            {
                _showPanel = false;
            }
            GUILayout.EndHorizontal();

            if (changed)
            {
                p.Clamp();
                _resolution = Mathf.Clamp(p.resolution, 16, 256);
                _core.SetParameters(p);
                _core.ForceRecalculate();
            }

            if (heightChanged)
            {
                _heightScale = Mathf.Max(0.05f, height);
                RebuildMesh(_core.Result);
            }

            if (yawChanged)
            {
                _surfaceYaw = yaw;
                Transform root = surfaceRoot != null ? surfaceRoot : transform;
                root.rotation = Quaternion.Euler(0f, _surfaceYaw, 0f);
            }

            if (normalizeDisplay != _normalizeDisplayIntensity)
            {
                _normalizeDisplayIntensity = normalizeDisplay;
                RebuildMesh(_core.Result);
            }

            UpdateStatus();
            GUI.DragWindow();
        }

        private void ApplyDemoProfile(int index)
        {
            _profileIndex = Mathf.Clamp(index, 0, 1);
            CrystalProfile profile = _profileIndex == 1 ? ktpProfile : liNbO3Profile;
            if (profile == null)
            {
                profile = liNbO3Profile != null ? liNbO3Profile : ktpProfile;
            }

            SetProfile(profile);
        }

        private void UpdateStatus()
        {
            if (statusText == null || _core == null)
            {
                return;
            }

            ConoscopicJonesParameters p = _core.Parameters;
            statusText.text =
                $"Jones GPU  Profile: {(_core.Profile != null ? _core.Profile.crystalName : "manual")}  Class: {p.CrystalOpticClass}  Valid: {_core.Result.IsValid}  Max: {_core.Result.MaxIntensity:F3}\n" +
                $"lambda {p.wavelengthNm:F1}nm  h {p.thicknessMm:F2}mm  n({p.principalIndexNx:F4}, {p.principalIndexNy:F4}, {p.principalIndexNz:F4})\n" +
                $"P {p.polarizerAngleDeg:F0}  A {p.analyzerAngleDeg:F0}  Crystal {p.crystalAxisAngleDeg:F0}  Tilt {p.opticAxisTiltDeg:F1}  Half {p.screenHalfSizeM:F3}m  AA {p.phaseAntiAliasStrength:F2}";
        }

        private static bool SliderRow(string label, ref float value, float min, float max, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110f));
            float newValue = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(180f));
            GUILayout.Label(newValue.ToString(format), GUILayout.Width(64f));
            GUILayout.EndHorizontal();

            if (Mathf.Abs(newValue - value) <= 0.0001f)
            {
                return false;
            }

            value = newValue;
            return true;
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
            bool normalizeDisplay = _normalizeDisplayIntensity && displayRange > 0.00001f;
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
                    float intensity = Mathf.Clamp01(pixels[index].r);
                    float displayIntensity = normalizeDisplay
                        ? Mathf.Clamp01((intensity - displayMin) / displayRange)
                        : intensity;
                    vertices[index] = new Vector3(
                        coordinate.x * _surfaceSize * 0.5f,
                        displayIntensity * _heightScale,
                        coordinate.y * _surfaceSize * 0.5f);
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
            _readbackTexture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            _readbackTexture.Apply(false, false);
            RenderTexture.active = previous;
            return _readbackTexture.GetPixels();
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
            _resolution = Mathf.Clamp(_resolution, 16, 256);
            _surfaceSize = Mathf.Max(0.1f, _surfaceSize);
            _heightScale = Mathf.Max(0.05f, _heightScale);

            if (Application.isPlaying && isActiveAndEnabled)
            {
                UnityEditor.EditorApplication.delayCall += DelayedEditorRefresh;
            }
        }

        private void DelayedEditorRefresh()
        {
            if (this == null || !Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            EnsureCore();
            if (_profile != null)
            {
                _core.SetProfile(_profile);
            }

            _core.SetResolution(_resolution);
            _core.ForceRecalculate();
            RebuildMesh(_core.Result);
        }

        [ContextMenu("Recalculate Jones Surface")]
        private void ContextMenuRecalculate()
        {
            EnsureCore();
            if (_profile != null)
            {
                _core.SetProfile(_profile);
            }

            _core.SetResolution(_resolution);
            _core.ForceRecalculate();
        }
#endif
    }
}
