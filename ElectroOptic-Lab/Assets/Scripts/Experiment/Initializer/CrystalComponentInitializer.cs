using UnityEngine;
using ElectroOptics;
using ElectroOptics.DataTransfer;
using ElectroOptics.Experiment.Controller;
using ElectroOptics.Experiment.Renderer;

namespace ElectroOptics.Experiment.Initializer
{
    /// <summary>
    /// Initializes the Scene2 crystal components and loads the selected crystal profile.
    /// </summary>
    public class CrystalComponentInitializer : MonoBehaviour
    {
        #region Inspector

        [Header("Crystal Model")]
        [Tooltip("Crystal GameObject. If empty, the initializer searches for the object named Crystal.")]
        [SerializeField] private GameObject crystalModel;

        [Tooltip("Profile used when Scene2 is opened directly without crystal selection data.")]
        [SerializeField] private CrystalProfile fallbackProfile;

        [Header("Rendering")]
        [Tooltip("RenderTexture size")]
        [SerializeField] private int renderTextureSize = 512;

        [Tooltip("Conoscopic field of view")]
        [SerializeField] [Range(1f, 120f)] private float conoscopicFOV = 10f;

        [Tooltip("Phase scale for showing more rings without increasing FOV")]
        [SerializeField] [Range(0.01f, 5f)] private float conoscopicPhaseScale = 1f;

        [Tooltip("Laser color")]
        [SerializeField] private Color laserColor = Color.red;

        [Tooltip("Display gamma applied after physical intensity calculation")]
        [SerializeField] [Range(0.2f, 3f)] private float displayGamma = 1.15f;

        [Tooltip("Low intensity cutoff for clearer black rings and cross")]
        [SerializeField] [Range(0f, 0.25f)] private float blackCutoff = 0.01f;

        [Tooltip("Controls derivative filtering for ring contrast")]
        [SerializeField] [Range(0.25f, 4f)] private float ringSharpness = 1.6f;

        [Tooltip("Angular width of the black cross")]
        [SerializeField] [Range(0.01f, 0.35f)] private float crossWidth = 0.08f;

        [Tooltip("Initial black cross center offset in normalized conoscopic view coordinates")]
        [SerializeField] private Vector2 initialMelatopeOffset = new Vector2(0.035f, -0.025f);

        [Tooltip("Jones screen distance used for the Scene2 conoscopic preview")]
        [SerializeField] [Range(0.01f, 10f)] private float screenDistanceM = 1.26f;

        [Tooltip("Jones screen half size used for the Scene2 conoscopic preview")]
        [SerializeField] [Range(0.005f, 0.3f)] private float screenHalfSizeM = 0.08f;

        [Tooltip("Derivative anti-aliasing strength for Jones phase fringes")]
        [SerializeField] [Range(0f, 4f)] private float phaseAntiAliasStrength = 1f;

        [Tooltip("Override profile wavelength/thickness in Raw Jones mode to match the additional experiment preset")]
        [SerializeField] private bool overrideRawJonesPhysicalParameters = false;

        [Tooltip("Raw Jones wavelength in nanometers")]
        [SerializeField] [Range(200f, 2000f)] private float rawJonesWavelengthNm = 589.3f;

        [Tooltip("Raw Jones crystal thickness in millimeters")]
        [SerializeField] [Range(0.001f, 200f)] private float rawJonesThicknessMm = 0.915f;

        [Tooltip("Display gain applied only to Raw Jones output so weak physical intensity remains visible")]
        [SerializeField] [Range(0f, 20f)] private float rawJonesDisplayGain = 1.6f;

        [Tooltip("Display gamma applied only to Raw Jones output")]
        [SerializeField] [Range(0.1f, 5f)] private float rawJonesDisplayGamma = 1.2f;

        [Tooltip("Output cutoff applied only to Raw Jones output")]
        [SerializeField] [Range(0f, 0.95f)] private float rawJonesOutputBlackCutoff = 0f;

        [Tooltip("Output white point applied only to Raw Jones output")]
        [SerializeField] [Range(0.001f, 1f)] private float rawJonesOutputWhitePoint = 1f;

        [Tooltip("In-plane crystal axis angle applied only to Raw Jones output")]
        [SerializeField] [Range(0f, 180f)] private float rawJonesCrystalAxisAngleDeg = 45f;

        [Tooltip("Reserved for the later pseudo-3D relief display pass")]
        [SerializeField] private bool enableReliefShading = false;

        [Tooltip("Reserved for the later pseudo-3D relief display pass")]
        [SerializeField] [Range(0f, 8f)] private float reliefStrength = 0f;

        [Tooltip("Biaxial display mode used by the Scene2 conoscopic preview")]
        [SerializeField] private Scene2BiaxialDisplayMode biaxialDisplayMode = Scene2BiaxialDisplayMode.PaperKtpPreset;

        [Tooltip("Use the Paper KTP preset when the selected biaxial profile is KTP")]
        [SerializeField] private bool usePaperKtpPresetForKtp = true;

        [Header("Optical Path")]
        [Tooltip("Laser emitter Transform. If empty, searches for LaserEmitter in the scene.")]
        [SerializeField] private Transform lightDirectionSource;

        #endregion

        #region Private Fields

        private CrystalControllerWrapper _controller;
        private ConoscopicTextureRenderer _textureRenderer;
        private CrystalPhysicalCore _physicalCore;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            Debug.Log("[CrystalComponentInitializer] Initializing...");

            FindCrystalModel();

            if (crystalModel == null)
            {
                Debug.LogError("[CrystalComponentInitializer] Crystal model was not found.");
                enabled = false;
                return;
            }

            Debug.Log($"[CrystalComponentInitializer] Found crystal model: {crystalModel.name}");

            SetupCrystalComponents();
            SetupLightDirectionSource();
            SetupTextureRenderer();
            RegisterToRuntime();
            LoadSelectedProfile();
            ResetPostProcessLayer();
        }

        private static void ResetPostProcessLayer()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var ppLayer = cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>();
            if (ppLayer != null)
            {
                ppLayer.enabled = false;
                ppLayer.enabled = true;
                Debug.Log("[CrystalComponentInitializer] PostProcessLayer reset");
            }
        }

        #endregion

        #region Initialization

        private void FindCrystalModel()
        {
            if (crystalModel != null)
            {
                return;
            }

            crystalModel = GameObject.Find("\u6676\u4f53");

            if (crystalModel == null)
            {
                crystalModel = GameObject.FindGameObjectWithTag("Crystal");
            }
        }

        private void SetupCrystalComponents()
        {
            _physicalCore = crystalModel.GetComponent<CrystalPhysicalCore>();
            if (_physicalCore == null)
            {
                _physicalCore = crystalModel.AddComponent<CrystalPhysicalCore>();
                Debug.Log("[CrystalComponentInitializer] Added CrystalPhysicalCore component");
            }

            EnsureCollider();

            _controller = crystalModel.GetComponent<CrystalControllerWrapper>();
            if (_controller == null)
            {
                _controller = crystalModel.AddComponent<CrystalControllerWrapper>();
                Debug.Log("[CrystalComponentInitializer] Added CrystalControllerWrapper component");
            }

            _controller.Initialize(_physicalCore);

            var retarder = crystalModel.GetComponent<global::CrystalRetarderPhysics>();
            if (retarder == null)
            {
                retarder = crystalModel.AddComponent<global::CrystalRetarderPhysics>();
                Debug.Log("[CrystalComponentInitializer] Added CrystalRetarderPhysics component");
            }

            global::OpticalComponent opticalComponent = crystalModel.GetComponent<global::OpticalComponent>();
            if (opticalComponent == null) opticalComponent = crystalModel.GetComponentInParent<global::OpticalComponent>();
            if (opticalComponent == null) opticalComponent = crystalModel.GetComponentInChildren<global::OpticalComponent>();
            retarder.Bind(_physicalCore, opticalComponent);
        }

        private void SetupLightDirectionSource()
        {
            if (lightDirectionSource == null)
            {
                global::LaserEmitter laserEmitter = Object.FindFirstObjectByType<global::LaserEmitter>();
                if (laserEmitter != null)
                {
                    lightDirectionSource = laserEmitter.transform;
                }
            }

            if (_controller != null)
            {
                _controller.SetLightDirectionSource(lightDirectionSource);
            }
        }

        private void EnsureCollider()
        {
            var collider = crystalModel.GetComponent<Collider>();
            if (collider != null)
            {
                return;
            }

            var meshFilter = crystalModel.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                var meshCollider = crystalModel.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = true;
                Debug.Log("[CrystalComponentInitializer] Added MeshCollider component");
            }
            else
            {
                crystalModel.AddComponent<BoxCollider>();
                Debug.Log("[CrystalComponentInitializer] Added BoxCollider component");
            }
        }

        private void SetupTextureRenderer()
        {
            _textureRenderer = GetComponent<ConoscopicTextureRenderer>();
            if (_textureRenderer == null)
            {
                _textureRenderer = gameObject.AddComponent<ConoscopicTextureRenderer>();
                Debug.Log("[CrystalComponentInitializer] Added ConoscopicTextureRenderer component");
            }

            _textureRenderer.Initialize(
                _physicalCore,
                renderTextureSize,
                conoscopicFOV,
                laserColor,
                conoscopicPhaseScale,
                displayGamma,
                blackCutoff,
                ringSharpness,
                crossWidth,
                initialMelatopeOffset,
                screenDistanceM,
                screenHalfSizeM,
                phaseAntiAliasStrength,
                enableReliefShading,
                reliefStrength,
                biaxialDisplayMode,
                usePaperKtpPresetForKtp);
            ApplyRendererRuntimeSettings();
        }

        private void ApplyRendererRuntimeSettings()
        {
            if (_textureRenderer == null)
            {
                return;
            }

            _textureRenderer.SetScene2DisplayParameters(
                conoscopicPhaseScale,
                displayGamma,
                blackCutoff,
                ringSharpness,
                crossWidth,
                initialMelatopeOffset,
                screenDistanceM,
                screenHalfSizeM,
                phaseAntiAliasStrength,
                laserColor);
            _textureRenderer.SetRawJonesDisplayMapping(
                rawJonesDisplayGain,
                rawJonesDisplayGamma,
                rawJonesOutputBlackCutoff,
                rawJonesOutputWhitePoint);
            _textureRenderer.SetRawJonesPhysicalParameters(
                overrideRawJonesPhysicalParameters,
                rawJonesWavelengthNm,
                rawJonesThicknessMm);
            _textureRenderer.SetRawJonesCrystalAxisAngle(rawJonesCrystalAxisAngleDeg);
            _textureRenderer.SetReliefShading(enableReliefShading, reliefStrength);
            _textureRenderer.SetBiaxialDisplayMode(biaxialDisplayMode, usePaperKtpPresetForKtp);
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                ApplyRendererRuntimeSettings();
            }
        }

        private void RegisterToRuntime()
        {
            CrystalRuntime.CrystalObject = crystalModel;
            CrystalRuntime.Controller = _controller;
            CrystalRuntime.TextureRenderer = _textureRenderer;
            CrystalRuntime.PhysicalCore = _physicalCore;

            Debug.Log("[CrystalComponentInitializer] Registered to CrystalRuntime");
        }

        private void LoadSelectedProfile()
        {
            CrystalProfile selectedProfile = CrystalSelectionData.SelectedProfile;
            if (selectedProfile == null && fallbackProfile != null)
            {
                selectedProfile = fallbackProfile;
                Debug.LogWarning($"[CrystalComponentInitializer] No crystal selection data; using fallback profile: {selectedProfile.crystalName}");
            }

            if (selectedProfile == null)
            {
                Debug.LogWarning("[CrystalComponentInitializer] Selected profile is null");
                return;
            }

            if (_controller != null && _controller.IsInitialized())
            {
                _controller.SetProfile(selectedProfile);
                Debug.Log($"[CrystalComponentInitializer] Loaded crystal profile: {selectedProfile.crystalName}");
            }
            else
            {
                Debug.LogError("[CrystalComponentInitializer] Controller is not initialized; cannot load profile.");
            }
        }

        #endregion

        #region Public API

        public CrystalControllerWrapper GetController() => _controller;

        public GameObject GetCrystalModel() => crystalModel;

        public ConoscopicTextureRenderer GetTextureRenderer() => _textureRenderer;

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Reinitialize")]
        private void ContextMenuReinitialize()
        {
            Start();
        }

        [ContextMenu("Print Status")]
        private void ContextMenuPrintStatus()
        {
            Debug.Log($"[CrystalComponentInitializer] Status:\n" +
                      $"  - Crystal model: {(crystalModel != null ? crystalModel.name : "null")}\n" +
                      $"  - Controller: {(_controller != null ? "set" : "null")}\n" +
                      $"  - Texture renderer: {(_textureRenderer != null ? "set" : "null")}\n" +
                      $"  - Physical core: {(_physicalCore != null ? "set" : "null")}\n" +
                      $"  - CrystalSelectionData.HasSelection: {CrystalSelectionData.HasSelection}");
        }
#endif

        #endregion
    }
}
