using System.Collections.Generic;
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

        SuspendScene2Interaction();
        _isTransitioning = true;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (operation == null)
        {
            _isTransitioning = false;
            RestoreScene2Interaction();
            Debug.LogError($"[Scene2AdditionalSceneNavigator] Failed to start additive load for {sceneName}.");
            return;
        }

        operation.completed += _ =>
        {
            _isTransitioning = false;
            Scene loadedAdditionalScene = SceneManager.GetSceneByName(sceneName);
            if (loadedAdditionalScene.isLoaded)
            {
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
        foreach (GameObject rootObject in labScene.GetRootGameObjects())
        {
            SuspendEnabledComponents<Canvas>(rootObject);
            SuspendEnabledComponents<Camera>(rootObject);
            SuspendEnabledComponents<AudioListener>(rootObject);
            SuspendEnabledComponents<EventSystem>(rootObject);
            SuspendEnabledComponents<OpticalComponent>(rootObject);
            SuspendEnabledComponents<CrystalInteract>(rootObject);
            SuspendEnabledComponents<ScreenInteract>(rootObject);
            SuspendEnabledComponents<ExperimentCameraController>(rootObject);
            SuspendEnabledComponents<ScreenPanelInteraction>(rootObject);
            SuspendEnabledComponents<UnifiedScreenPanel>(rootObject);
        }

        _isScene2Suspended = true;
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

        for (int i = 0; i < SuspendedScene2Behaviours.Count; i++)
        {
            Behaviour behaviour = SuspendedScene2Behaviours[i];
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        SuspendedScene2Behaviours.Clear();
        _isScene2Suspended = false;
    }
}
