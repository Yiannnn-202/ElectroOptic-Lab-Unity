using System.Collections;
using ElectroOptics;
using ElectroOptics.ConoscopicAnalysis;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class AdditionalExperimentSceneDispatcher : MonoBehaviour
{
    private const string LogPrefix = "[AdditionalExperimentSceneDispatcher]";
    private const string LiNbO3ProfilePath = "Assets/LiNbO3_Profile.asset";
    private const string KtpProfilePath = "Assets/KTP_Profile.asset";

    [Header("Scene References")]
    [SerializeField] private AdditionalExperimentUiVisualController uiController;
    [SerializeField] private AdditionalConoscopicExperimentApi experimentApi;
    [SerializeField] private ConoscopicJonesGpuCore core;
    [SerializeField] private AdditionalConoscopicSurfaceView surfaceView;
    [SerializeField] private AdditionalConoscopicVisualizationSettings visualizationSettings;
    [SerializeField] private RawImage renderOutput;

    [Header("Profiles")]
    [SerializeField] private CrystalProfile liNbO3Profile;
    [SerializeField] private CrystalProfile ktpProfile;

    [Header("Runtime")]
    [SerializeField] private bool autoCreateRuntimeObjects = true;
    [SerializeField] private float autoRecalculateDelaySeconds = 0.15f;
    [SerializeField] private float interactiveRecalculateIntervalSeconds = 0.06f;
    [SerializeField] private bool logDiagnostics = true;

    private AdditionalConoscopicMode _pendingMode = AdditionalConoscopicMode.Uniaxial;
    private AdditionalConoscopicUserParameters _pendingParameters = AdditionalConoscopicUserParameters.Defaults;
    private Coroutine _recalculateCoroutine;
    private bool _hasPendingRecalculate;
    private float _lastRecalculateTime = -1000f;

    private void Awake()
    {
        AutoBindMissingReferences();
        EnsureRuntimeObjects();
        ConfigureRuntimeObjects();
        SubscribeUi();
    }

    private void Start()
    {
        CaptureUiSnapshot();
        ApplyPendingStateNow();
    }

    private void OnDestroy()
    {
        UnsubscribeUi();
    }

    private void HandleModeChanged(AdditionalConoscopicMode mode)
    {
        _pendingMode = mode;
        CaptureUiSnapshot();
        ScheduleRecalculate();
    }

    private void HandleParametersChanged(AdditionalConoscopicUserParameters parameters)
    {
        _pendingParameters = parameters;
        ScheduleRecalculate();
    }

    private void HandleRecalculateRequested()
    {
        CaptureUiSnapshot();
        ApplyPendingStateNow();
    }

    private void CaptureUiSnapshot()
    {
        if (uiController == null)
        {
            return;
        }

        _pendingMode = uiController.ActiveMode;
        _pendingParameters = uiController.GetUserParameters();
    }

    private void ScheduleRecalculate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        _hasPendingRecalculate = true;
        if (interactiveRecalculateIntervalSeconds > 0f
            && Time.unscaledTime - _lastRecalculateTime >= interactiveRecalculateIntervalSeconds)
        {
            ApplyPendingStateNow();
        }

        if (_recalculateCoroutine != null)
        {
            StopCoroutine(_recalculateCoroutine);
            _recalculateCoroutine = null;
        }

        if (autoRecalculateDelaySeconds <= 0f)
        {
            if (_hasPendingRecalculate)
            {
                ApplyPendingStateNow();
            }

            return;
        }

        _recalculateCoroutine = StartCoroutine(DelayedRecalculate());
    }

    private IEnumerator DelayedRecalculate()
    {
        yield return new WaitForSeconds(autoRecalculateDelaySeconds);
        _recalculateCoroutine = null;
        if (_hasPendingRecalculate)
        {
            ApplyPendingStateNow();
        }
    }

    private void ApplyPendingStateNow()
    {
        _hasPendingRecalculate = false;
        _lastRecalculateTime = Time.unscaledTime;
        EnsureRuntimeObjects();
        ConfigureRuntimeObjects();

        if (experimentApi == null)
        {
            Debug.LogError($"{LogPrefix} Experiment API is missing.");
            return;
        }

        experimentApi.ApplyAndRecalculate(_pendingMode, _pendingParameters);
    }

    private void SubscribeUi()
    {
        if (uiController == null)
        {
            return;
        }

        uiController.ModeChanged -= HandleModeChanged;
        uiController.ParametersChanged -= HandleParametersChanged;
        uiController.RecalculateRequested -= HandleRecalculateRequested;

        uiController.ModeChanged += HandleModeChanged;
        uiController.ParametersChanged += HandleParametersChanged;
        uiController.RecalculateRequested += HandleRecalculateRequested;
    }

    private void UnsubscribeUi()
    {
        if (uiController == null)
        {
            return;
        }

        uiController.ModeChanged -= HandleModeChanged;
        uiController.ParametersChanged -= HandleParametersChanged;
        uiController.RecalculateRequested -= HandleRecalculateRequested;
    }

    private void AutoBindMissingReferences()
    {
        if (uiController == null)
        {
            uiController = FindFirstObjectByType<AdditionalExperimentUiVisualController>();
        }

        if (renderOutput == null)
        {
            renderOutput = FindNamedComponent<RawImage>("RenderOutput");
        }

        if (surfaceView == null)
        {
            surfaceView = FindFirstObjectByType<AdditionalConoscopicSurfaceView>();
        }

        BindVisualizationSettings();

        if (experimentApi == null)
        {
            experimentApi = FindFirstObjectByType<AdditionalConoscopicExperimentApi>();
        }

        if (core == null)
        {
            core = FindFirstObjectByType<ConoscopicJonesGpuCore>();
        }

#if UNITY_EDITOR
        if (liNbO3Profile == null)
        {
            liNbO3Profile = AssetDatabase.LoadAssetAtPath<CrystalProfile>(LiNbO3ProfilePath);
        }

        if (ktpProfile == null)
        {
            ktpProfile = AssetDatabase.LoadAssetAtPath<CrystalProfile>(KtpProfilePath);
        }
#endif
    }

    private void EnsureRuntimeObjects()
    {
        if (!autoCreateRuntimeObjects)
        {
            return;
        }

        if (experimentApi == null || core == null)
        {
            GameObject runtimeObject = GameObject.Find("Additional Conoscopic Experiment Runtime");
            if (runtimeObject == null)
            {
                runtimeObject = new GameObject("Additional Conoscopic Experiment Runtime");
            }

            core = runtimeObject.GetComponent<ConoscopicJonesGpuCore>();
            if (core == null)
            {
                core = runtimeObject.AddComponent<ConoscopicJonesGpuCore>();
            }

            experimentApi = runtimeObject.GetComponent<AdditionalConoscopicExperimentApi>();
            if (experimentApi == null)
            {
                experimentApi = runtimeObject.AddComponent<AdditionalConoscopicExperimentApi>();
            }
        }

        if (surfaceView == null)
        {
            GameObject surfaceObject = GameObject.Find("Additional Conoscopic Jones Surface");
            if (surfaceObject == null)
            {
                surfaceObject = new GameObject("Additional Conoscopic Jones Surface");
                surfaceObject.transform.position = Vector3.zero;
            }

            surfaceView = surfaceObject.GetComponent<AdditionalConoscopicSurfaceView>();
            if (surfaceView == null)
            {
                surfaceView = surfaceObject.AddComponent<AdditionalConoscopicSurfaceView>();
            }
        }

        BindVisualizationSettings();
        if (visualizationSettings == null && autoCreateRuntimeObjects && surfaceView != null)
        {
            visualizationSettings = surfaceView.gameObject.AddComponent<AdditionalConoscopicVisualizationSettings>();
        }
    }

    private void ConfigureRuntimeObjects()
    {
        BindVisualizationSettings();

        if (experimentApi != null)
        {
            experimentApi.Configure(core, liNbO3Profile, ktpProfile, visualizationSettings);
        }

        if (surfaceView != null)
        {
            surfaceView.Configure(core, renderOutput, null, visualizationSettings);
        }

        if (logDiagnostics)
        {
            Debug.Log($"{LogPrefix} Bound ui={uiController != null}, api={experimentApi != null}, " +
                      $"core={core != null}, surface={surfaceView != null}, settings={visualizationSettings != null}, " +
                      $"output={renderOutput != null}, " +
                      $"LiNbO3={liNbO3Profile != null}, KTP={ktpProfile != null}");
            logDiagnostics = false;
        }
    }

    private void BindVisualizationSettings()
    {
        if (visualizationSettings == null && surfaceView != null)
        {
            visualizationSettings = surfaceView.GetComponent<AdditionalConoscopicVisualizationSettings>();
        }

        if (visualizationSettings == null)
        {
            visualizationSettings = FindFirstObjectByType<AdditionalConoscopicVisualizationSettings>();
        }
    }

    private static T FindNamedComponent<T>(string objectName) where T : Component
    {
        T[] components = FindObjectsOfType<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].gameObject.name == objectName)
            {
                return components[i];
            }
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        autoRecalculateDelaySeconds = Mathf.Max(0f, autoRecalculateDelaySeconds);
        interactiveRecalculateIntervalSeconds = Mathf.Max(0f, interactiveRecalculateIntervalSeconds);
        if (liNbO3Profile == null)
        {
            liNbO3Profile = AssetDatabase.LoadAssetAtPath<CrystalProfile>(LiNbO3ProfilePath);
        }

        if (ktpProfile == null)
        {
            ktpProfile = AssetDatabase.LoadAssetAtPath<CrystalProfile>(KtpProfilePath);
        }
    }
#endif
}
