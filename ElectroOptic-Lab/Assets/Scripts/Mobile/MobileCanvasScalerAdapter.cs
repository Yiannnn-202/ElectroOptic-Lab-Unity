using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElectroOptics.Mobile
{
    /// <summary>
    /// Mobile-only CanvasScaler tuning for scenes designed around 16:9 / 1920x1080.
    /// It does not reparent UI objects, so existing scene layouts remain intact.
    /// </summary>
    public class MobileCanvasScalerAdapter : MonoBehaviour
    {
        private static bool _created;
        private float _lastAspect = -1f;
        private string _lastSceneName = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_created || !MobileRuntime.IsActive)
            {
                return;
            }

            GameObject root = new GameObject("MobileCanvasScalerAdapter");
            DontDestroyOnLoad(root);
            root.AddComponent<MobileCanvasScalerAdapter>();
            _created = true;
        }

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Apply();
        }

        private void Apply()
        {
            _lastAspect = GetCurrentAspect();
            _lastSceneName = SceneManager.GetActiveScene().name;

            if (!ShouldAdaptScene(_lastSceneName))
            {
                return;
            }

            float match = ResolveMatchValue(_lastAspect);
            EnsureRootCanvasScalers();

            CanvasScaler[] scalers = FindObjectsOfType<CanvasScaler>();
            for (int i = 0; i < scalers.Length; i++)
            {
                CanvasScaler scaler = scalers[i];
                if (!ShouldAdaptScaler(scaler))
                {
                    continue;
                }

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = match;
            }
        }

        private static void EnsureRootCanvasScalers()
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (!ShouldAdaptCanvas(canvas) || canvas.GetComponent<CanvasScaler>() != null)
                {
                    continue;
                }

                canvas.gameObject.AddComponent<CanvasScaler>();
            }
        }

        private static bool ShouldAdaptScene(string sceneName)
        {
            return sceneName.Contains("Scene0.Open Menu")
                   || sceneName.Contains("Scene1.intro")
                   || sceneName.Contains("Scene2-preview")
                   || sceneName.Contains("Scene2.The Lab")
                   || sceneName.Contains("Scene3")
                   || sceneName.Contains("Scene4")
                   || sceneName.Contains("Scene5")
                   || sceneName.Contains("Scene6")
                   || sceneName.Contains("Scene7")
                   || sceneName.Contains("Scene_additional_exp");
        }

        private static bool ShouldAdaptScaler(CanvasScaler scaler)
        {
            if (scaler == null)
            {
                return false;
            }

            Canvas canvas = scaler.GetComponent<Canvas>();

            return ShouldAdaptCanvas(canvas)
                   && scaler.GetComponent<MobileVirtualControls>() == null
                   && scaler.GetComponent<MobileAspectRatioEnforcer>() == null
                   && scaler.GetComponent<MobileCanvasScalerAdapter>() == null;
        }

        private static bool ShouldAdaptCanvas(Canvas canvas)
        {
            if (canvas == null || !canvas.isRootCanvas)
            {
                return false;
            }

            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                return false;
            }

            return canvas.GetComponent<MobileVirtualControls>() == null
                   && canvas.GetComponent<MobileAspectRatioEnforcer>() == null
                   && canvas.GetComponent<MobileCanvasScalerAdapter>() == null;
        }

        private static float ResolveMatchValue(float aspect)
        {
            if (aspect < MobileAspectRatioEnforcer.TargetAspect)
            {
                return 0f; // Pad / 4:3: preserve designed width, add extra vertical room.
            }

            if (aspect > MobileAspectRatioEnforcer.TargetAspect)
            {
                return 1f; // Ultrawide phones: preserve designed height.
            }

            return 0.5f;
        }

        private static float GetCurrentAspect()
        {
            return Screen.width > 0 && Screen.height > 0
                ? (float)Screen.width / Screen.height
                : MobileAspectRatioEnforcer.TargetAspect;
        }
    }
}
