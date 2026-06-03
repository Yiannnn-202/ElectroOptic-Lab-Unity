using System.Collections.Generic;
using ElectroOptics.DataTransfer;
using ElectroOptics.UI.ScreenDisplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Opens the additional experiment without destroying Scene2, then restores Scene2 interaction on return.
/// </summary>
public static class Scene2AdditionalSceneNavigator
{
    public const string LabSceneName = "Scene2.The Lab";
    public const string AdditionalSceneName = "Scene_additional_exp";

    private static readonly List<Behaviour> SuspendedScene2Behaviours = new List<Behaviour>();
    private static bool _isScene2Suspended;
    private static bool _isTransitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        SuspendedScene2Behaviours.Clear();
        _isScene2Suspended = false;
        _isTransitioning = false;
    }

    /// <summary>
    /// Returns true only when Scene2 is loaded in the background AND another scene
    /// is active on top of it — i.e. Scene2 has been suspended by this navigator.
    /// Use this in Update() guards to skip input processing when Scene2 is suspended.
    /// </summary>
    public static bool IsLabSceneSuspended()
    {
        Scene labScene = SceneManager.GetSceneByName(LabSceneName);
        if (!labScene.isLoaded)
            return false; // Scene2 isn't loaded at all — standalone scene, process normally

        Scene active = SceneManager.GetActiveScene();
        return active.IsValid() && active.name != LabSceneName;
    }

    public static void OpenAdditionalExperiment(string sceneName)
    {
        sceneName = NormalizeAdditionalSceneName(sceneName);

        if (_isTransitioning)
        {
            Debug.Log($"[Scene2AdditionalSceneNavigator] Transition already in progress; ignoring open request for {sceneName}.");
            return;
        }

        Scene additionalScene = SceneManager.GetSceneByName(sceneName);
        if (additionalScene.isLoaded)
        {
            SuspendScene2Interaction();
            SceneManager.SetActiveScene(additionalScene);
            return;
        }

        Scene labScene = SceneManager.GetSceneByName(LabSceneName);
        if (!labScene.isLoaded)
        {
            Debug.LogWarning($"[Scene2AdditionalSceneNavigator] {LabSceneName} is not loaded; loading {sceneName} normally.");
            SceneManager.LoadScene(sceneName);
            return;
        }

        // NOTE: Do NOT suspend Scene2 until the async load completes.
        // Suspending before LoadSceneAsync would disable Scene2's camera immediately,
        // creating a gap with zero active cameras → Unity "No camera rendering" warning → Error Pause.
        _isTransitioning = true;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (operation == null)
        {
            _isTransitioning = false;
            Debug.LogError($"[Scene2AdditionalSceneNavigator] Failed to start additive load for {sceneName}.");
            return;
        }

        operation.completed += _ =>
        {
            _isTransitioning = false;
            Scene loadedAdditionalScene = SceneManager.GetSceneByName(sceneName);
            if (loadedAdditionalScene.isLoaded)
            {
                SuspendScene2Interaction();
                SceneManager.SetActiveScene(loadedAdditionalScene);
                Debug.Log($"[Scene2AdditionalSceneNavigator] Opened additive scene: {sceneName}");
            }
        };
    }

    public static void ReturnFromAdditionalExperiment(string sceneName)
    {
        sceneName = NormalizeAdditionalSceneName(sceneName);

        if (_isTransitioning)
        {
            Debug.Log($"[Scene2AdditionalSceneNavigator] Transition already in progress; ignoring return request from {sceneName}.");
            return;
        }

        Scene labScene = SceneManager.GetSceneByName(LabSceneName);
        if (!labScene.isLoaded)
        {
            Debug.LogWarning($"[Scene2AdditionalSceneNavigator] {LabSceneName} is not loaded; falling back to normal load.");
            SceneManager.LoadScene(LabSceneName);
            return;
        }

        SceneManager.SetActiveScene(labScene);

        Scene additionalScene = SceneManager.GetSceneByName(sceneName);
        if (!additionalScene.isLoaded)
        {
            RestoreScene2Interaction();
            return;
        }

        _isTransitioning = true;
        AsyncOperation operation = SceneManager.UnloadSceneAsync(additionalScene);
        if (operation == null)
        {
            _isTransitioning = false;
            RestoreScene2Interaction();
            Debug.LogError($"[Scene2AdditionalSceneNavigator] Failed to unload {sceneName}.");
            return;
        }

        operation.completed += _ =>
        {
            _isTransitioning = false;
            RestoreScene2Interaction();
            Debug.Log($"[Scene2AdditionalSceneNavigator] Returned to {LabSceneName} from {sceneName}.");
        };
    }

    /// <summary>
    /// Loads the crystal preview scene additively while keeping Scene2 alive.
    /// Used when entering an experiment flow from Scene2.
    /// </summary>
    public static void GoToPreviewAdditive(string previewSceneName, string targetExperimentScene)
    {
        Scene labScene = SceneManager.GetSceneByName(LabSceneName);
        if (!labScene.isLoaded)
        {
            Debug.LogWarning($"[Scene2AdditionalSceneNavigator] {LabSceneName} not loaded; loading preview normally.");
            CrystalSelectionData.TargetSceneName = targetExperimentScene;
            SceneManager.LoadScene(previewSceneName);
            return;
        }

        if (_isTransitioning)
        {
            Debug.Log($"[Scene2AdditionalSceneNavigator] Transition already in progress; ignoring request.");
            return;
        }

        CrystalSelectionData.TargetSceneName = targetExperimentScene;
        _isTransitioning = true;

        AsyncOperation operation = SceneManager.LoadSceneAsync(previewSceneName, LoadSceneMode.Additive);
        if (operation == null)
        {
            _isTransitioning = false;
            Debug.LogError($"[Scene2AdditionalSceneNavigator] Failed to start additive load for {previewSceneName}.");
            return;
        }

        operation.completed += _ =>
        {
            _isTransitioning = false;
            Scene previewScene = SceneManager.GetSceneByName(previewSceneName);
            if (previewScene.isLoaded)
            {
                SuspendScene2Interaction();
                SceneManager.SetActiveScene(previewScene);
                Debug.Log($"[Scene2AdditionalSceneNavigator] Opened additive preview: {previewSceneName}");
            }
        };
    }

    /// <summary>
    /// Loads the experiment scene additively and unloads the preview scene.
    /// Called by CrystalCardSelector when Scene2 is alive.
    /// </summary>
    public static void GoToExperimentFromPreviewAdditive(string targetSceneName)
    {
        Scene labScene = SceneManager.GetSceneByName(LabSceneName);
        if (!labScene.isLoaded)
        {
            Debug.LogWarning($"[Scene2AdditionalSceneNavigator] {LabSceneName} not loaded; loading experiment normally.");
            SceneManager.LoadScene(targetSceneName);
            return;
        }

        if (_isTransitioning)
        {
            Debug.Log($"[Scene2AdditionalSceneNavigator] Transition already in progress; ignoring request.");
            return;
        }

        _isTransitioning = true;

        const string previewSceneName = "Scene2-preview";
        Scene previewScene = SceneManager.GetSceneByName(previewSceneName);

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
        if (loadOp == null)
        {
            _isTransitioning = false;
            Debug.LogError($"[Scene2AdditionalSceneNavigator] Failed to start additive load for {targetSceneName}.");
            return;
        }

        loadOp.completed += _ =>
        {
            Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
            if (targetScene.isLoaded)
            {
                SceneManager.SetActiveScene(targetScene);
                Debug.Log($"[Scene2AdditionalSceneNavigator] Loaded experiment additively: {targetSceneName}");
            }

            // Unload the preview scene now that we're in the experiment
            if (previewScene.isLoaded)
                SceneManager.UnloadSceneAsync(previewScene);

            _isTransitioning = false;
        };
    }

    private static string NormalizeAdditionalSceneName(string sceneName)
    {
        return string.IsNullOrWhiteSpace(sceneName) ? AdditionalSceneName : sceneName;
    }

    private static void SuspendScene2Interaction()
    {
        if (_isScene2Suspended)
        {
            return;
        }

        Scene labScene = SceneManager.GetSceneByName(LabSceneName);
        if (!labScene.isLoaded)
        {
            return;
        }

        SuspendedScene2Behaviours.Clear();

        // Types to suspend — ordered so that "leaf" components are disabled before
        // their parent containers (e.g. ScreenPanelInteraction before UnifiedScreenPanel,
        // camera-dependent before Camera).
        // NOTE: we intentionally do NOT disable EventSystem — Unity handles multiple
        // EventSystems by only using the one in the active scene, and toggling it
        // would produce "Multiple EventSystems" warnings on restore.
        foreach (GameObject rootObject in labScene.GetRootGameObjects())
        {
            // UI interaction leaf components
            SuspendEnabledComponents<ScreenPanelInteraction>(rootObject);

            // Input-processing components (prevent global input bleed)
            SuspendEnabledComponents<RecordManager>(rootObject);
            SuspendEnabledComponents<PowerReadoutController>(rootObject);

            // Crystal / optical components
            SuspendEnabledComponents<CrystalPhysicalCore>(rootObject);
            SuspendEnabledComponents<OpticalComponent>(rootObject);
            SuspendEnabledComponents<CrystalInteract>(rootObject);
            SuspendEnabledComponents<ScreenInteract>(rootObject);

            // Laser and light-path (stop laser emission & physics while suspended)
            SuspendEnabledComponents<LaserEmitter>(rootObject);
            SuspendEnabledComponents<PolarizerPhysics>(rootObject);
            SuspendEnabledComponents<CrystalRetarderPhysics>(rootObject);
            SuspendEnabledComponents<DirectScreenController>(rootObject);

            // Camera control
            SuspendEnabledComponents<ExperimentCameraController>(rootObject);
            SuspendEnabledComponents<CameraSwitch>(rootObject);

            // Display panel (disable before its parent Canvas)
            SuspendEnabledComponents<UnifiedScreenPanel>(rootObject);

            // Visual / audio output last
            SuspendEnabledComponents<AudioListener>(rootObject);
            SuspendEnabledComponents<Camera>(rootObject);
            SuspendEnabledComponents<Canvas>(rootObject);
        }

        _isScene2Suspended = true;
        Debug.Log($"[Scene2AdditionalSceneNavigator] Suspended {SuspendedScene2Behaviours.Count} behaviours in Scene2.");
    }

    private static void SuspendEnabledComponents<T>(GameObject rootObject) where T : Behaviour
    {
        T[] components = rootObject.GetComponentsInChildren<T>(true);
        foreach (T component in components)
        {
            if (component == null || !component.enabled)
            {
                continue;
            }

            component.enabled = false;
            SuspendedScene2Behaviours.Add(component);
        }
    }

    private static void RestoreScene2Interaction()
    {
        if (!_isScene2Suspended)
        {
            return;
        }

        // Safety: if Scene2 was unloaded externally, discard stale suspension state
        Scene labScene = SceneManager.GetSceneByName(LabSceneName);
        if (!labScene.isLoaded)
        {
            Debug.LogWarning("[Scene2AdditionalSceneNavigator] Scene2 was unloaded externally; discarding stale suspension state.");
            SuspendedScene2Behaviours.Clear();
            _isScene2Suspended = false;
            return;
        }

        int restored = 0;
        int destroyed = 0;

        for (int i = 0; i < SuspendedScene2Behaviours.Count; i++)
        {
            Behaviour behaviour = SuspendedScene2Behaviours[i];
            if (behaviour == null)
            {
                destroyed++;
                continue;
            }

            // Only re-enable if the GameObject is still in the lab scene
            // (guards against edge cases where the object was moved or destroyed)
            if (behaviour.gameObject.scene != labScene)
            {
                destroyed++;
                continue;
            }

            behaviour.enabled = true;
            restored++;
        }

        SuspendedScene2Behaviours.Clear();
        _isScene2Suspended = false;

        Debug.Log($"[Scene2AdditionalSceneNavigator] Restored {restored} behaviours" +
                  (destroyed > 0 ? $", skipped {destroyed} destroyed/moved." : "."));
    }
}
