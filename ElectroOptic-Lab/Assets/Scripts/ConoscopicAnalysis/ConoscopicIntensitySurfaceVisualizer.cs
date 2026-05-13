using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.ConoscopicAnalysis
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ConoscopicIntensitySurfaceVisualizer : MonoBehaviour
    {
        private const string VertexColorShaderName = "ElectroOptics/ConoscopicIntensityVertexColor";

        [SerializeField] private ConoscopicIntensityCore _core;
        [SerializeField] private CrystalProfile _profile;
        [SerializeField] [Range(16, 256)] private int _resolution = 96;
        [SerializeField] private float _surfaceSize = 5f;
        [SerializeField] private float _heightScale = 1.5f;
        [SerializeField] private bool _recalculateOnStart = true;

        [Header("Runtime Demo Panel")]
        public CrystalProfile liNbO3Profile;
        public CrystalProfile ktpProfile;
        public TextMesh statusText;
        public Transform surfaceRoot;
        [SerializeField] private bool _showPanel = true;
        [SerializeField] private Rect _panelRect = new Rect(16f, 16f, 340f, 560f);

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private int _profileIndex;
        private float _surfaceYaw;

        public float HeightScale => _heightScale;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnEnable()
        {
            EnsureComponents();
            if (_core != null)
            {
                _core.OnIntensityUpdated += HandleIntensityUpdated;
            }
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
                _core.OnIntensityUpdated -= HandleIntensityUpdated;
            }
        }

        public void SetCore(ConoscopicIntensityCore core)
        {
            if (_core == core)
            {
                return;
            }

            if (_core != null)
            {
                _core.OnIntensityUpdated -= HandleIntensityUpdated;
            }

            _core = core;
            if (_core != null && isActiveAndEnabled)
            {
                _core.OnIntensityUpdated += HandleIntensityUpdated;
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

        public void SetHeightScale(float heightScale)
        {
            _heightScale = Mathf.Max(0.05f, heightScale);
            if (_core != null && _core.Result != null && _core.Result.IsValid)
            {
                RebuildMesh(_core.Result);
            }
        }

        private void EnsureCore()
        {
            if (_core == null)
            {
                _core = GetComponent<ConoscopicIntensityCore>();
                if (_core == null)
                {
                    _core = gameObject.AddComponent<ConoscopicIntensityCore>();
                }

                _core.OnIntensityUpdated -= HandleIntensityUpdated;
                _core.OnIntensityUpdated += HandleIntensityUpdated;
            }
        }

        private void EnsureComponents()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Conoscopic Intensity Surface" };
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
                        name = "Conoscopic Intensity Vertex Color"
                    };
                }
            }
        }

        private void HandleIntensityUpdated(ConoscopicIntensityResult result)
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

            _panelRect = GUI.Window(GetInstanceID(), _panelRect, DrawPanel, "Conoscopic Controls");
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

            var p = new ConoscopicIntensityParameters(_core.Parameters);
            bool changed = false;
            float height = _heightScale;
            float yaw = _surfaceYaw;

            GUILayout.Label($"Profile: {(_core.Profile != null ? _core.Profile.crystalName : "null")}");
            GUILayout.Label($"Valid: {_core.Result.IsValid}  Max: {_core.Result.MaxIntensity:F3}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("LiNbO3"))
            {
                ApplyDemoProfile(0);
                changed = true;
            }
            if (GUILayout.Button("KTP"))
            {
                ApplyDemoProfile(1);
                changed = true;
            }
            if (GUILayout.Button("Reset"))
            {
                p = new ConoscopicIntensityParameters();
                _core.SetParameters(p);
                ApplyDemoProfile(_profileIndex);
                changed = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            changed |= SliderRow("FOV", ref p.fov, ConoscopicIntensityParameters.MinFov, ConoscopicIntensityParameters.MaxFov, "F1");
            changed |= SliderRow("Phase", ref p.phaseScale, ConoscopicIntensityParameters.MinPhaseScale, 1.5f, "F3");
            changed |= SliderRow("Gamma", ref p.displayGamma, ConoscopicIntensityParameters.MinDisplayGamma, 3f, "F2");
            changed |= SliderRow("Black", ref p.blackCutoff, 0f, ConoscopicIntensityParameters.MaxBlackCutoff, "F3");
            changed |= SliderRow("Sharp", ref p.ringSharpness, ConoscopicIntensityParameters.MinRingSharpness, 4f, "F2");
            changed |= SliderRow("Cross", ref p.crossWidth, ConoscopicIntensityParameters.MinCrossWidth, ConoscopicIntensityParameters.MaxCrossWidth, "F3");

            Vector2 rotation = p.crystalRotation;
            changed |= SliderRow("Rot X", ref rotation.x, -45f, 45f, "F1");
            changed |= SliderRow("Rot Y", ref rotation.y, -45f, 45f, "F1");
            p.crystalRotation = rotation;

            Vector2 melatope = p.initialMelatopeOffset;
            changed |= SliderRow("Mel X", ref melatope.x, -ConoscopicIntensityParameters.MaxInitialMelatopeOffset, ConoscopicIntensityParameters.MaxInitialMelatopeOffset, "F3");
            changed |= SliderRow("Mel Y", ref melatope.y, -ConoscopicIntensityParameters.MaxInitialMelatopeOffset, ConoscopicIntensityParameters.MaxInitialMelatopeOffset, "F3");
            p.initialMelatopeOffset = melatope;

            float resolution = p.resolution;
            if (SliderRow("Res", ref resolution, 16f, 256f, "F0"))
            {
                p.resolution = Mathf.RoundToInt(resolution);
                changed = true;
            }

            bool heightChanged = SliderRow("Height", ref height, 0.05f, 4f, "F2");
            bool yawChanged = SliderRow("View Yaw", ref yaw, -180f, 180f, "F0");

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Recalculate"))
            {
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
                _core.SetParameters(p);
                _core.ForceRecalculate();
            }

            if (heightChanged)
            {
                SetHeightScale(height);
            }

            if (yawChanged)
            {
                _surfaceYaw = yaw;
                Transform root = surfaceRoot != null ? surfaceRoot : transform;
                root.rotation = Quaternion.Euler(0f, _surfaceYaw, 0f);
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

            ConoscopicIntensityParameters p = _core.Parameters;
            statusText.text =
                $"Profile: {(_core.Profile != null ? _core.Profile.crystalName : "null")}  Valid: {_core.Result.IsValid}  Max: {_core.Result.MaxIntensity:F3}\n" +
                $"FOV {p.fov:F1}  Phase {p.phaseScale:F3}  Gamma {p.displayGamma:F2}  Black {p.blackCutoff:F3}\n" +
                $"Sharp {p.ringSharpness:F2}  Cross {p.crossWidth:F3}  Rot({p.crystalRotation.x:F1}, {p.crystalRotation.y:F1})";
        }

        private static bool SliderRow(string label, ref float value, float min, float max, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(68f));
            float newValue = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(170f));
            GUILayout.Label(newValue.ToString(format), GUILayout.Width(54f));
            GUILayout.EndHorizontal();

            if (Mathf.Abs(newValue - value) <= 0.0001f)
            {
                return false;
            }

            value = newValue;
            return true;
        }

        private void RebuildMesh(ConoscopicIntensityResult result)
        {
            if (result == null || !result.IsValid || result.Intensities == null)
            {
                return;
            }

            EnsureComponents();
            int resolution = result.Resolution;
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
                    Vector2 coordinate = result.GetCoordinate(x, y);
                    float intensity = Mathf.Clamp01(result.Intensities[index]);
                    vertices[index] = new Vector3(
                        coordinate.x * _surfaceSize * 0.5f,
                        intensity * _heightScale,
                        coordinate.y * _surfaceSize * 0.5f);
                    uvs[index] = new Vector2(x / (float)(resolution - 1), y / (float)(resolution - 1));
                    colors[index] = Color.Lerp(new Color(0.02f, 0.02f, 0.04f, 1f), Color.red, intensity);
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
            if (_core.Result != null && _core.Result.IsValid)
            {
                RebuildMesh(_core.Result);
            }
        }

        [ContextMenu("Recalculate Surface")]
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
