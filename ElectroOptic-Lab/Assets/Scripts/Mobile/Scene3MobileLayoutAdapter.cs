using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElectroOptics.Mobile
{
    /// <summary>
    /// Scene3 has a complex desktop-authored UI. This adapter normalizes its main panels
    /// into a stable 1920x1080 two-column layout for mobile/tablet builds.
    /// </summary>
    public class Scene3MobileLayoutAdapter : MonoBehaviour
    {
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;
        private const float LeftWidth = 800f;
        private const float RightStartX = 830f;
        private const float MainTopInset = 100f;

        private static bool _created;
        private UIStateManager _stateManager;
        private int _lastAppliedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_created || !MobileRuntime.IsActive)
            {
                return;
            }

            GameObject root = new GameObject("Scene3MobileLayoutAdapter");
            DontDestroyOnLoad(root);
            root.AddComponent<Scene3MobileLayoutAdapter>();
            _created = true;
        }

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _stateManager = null;
            _lastAppliedFrame = -1;
        }

        private void LateUpdate()
        {
            if (!SceneManager.GetActiveScene().name.Contains("Scene3"))
            {
                return;
            }

            if (_lastAppliedFrame == Time.frameCount)
            {
                return;
            }

            if (_stateManager == null)
            {
                _stateManager = FindObjectOfType<UIStateManager>();
            }

            if (_stateManager == null)
            {
                return;
            }

            ApplyLayout(_stateManager);
            _lastAppliedFrame = Time.frameCount;
        }

        private static void ApplyLayout(UIStateManager manager)
        {
            RectTransform mainArea = ResolveMainArea(manager);
            if (mainArea != null)
            {
                ApplyMainArea(mainArea);
            }

            RectTransform left = manager.leftControlPanel != null ? manager.leftControlPanel.GetComponent<RectTransform>() : null;
            if (left != null)
            {
                ApplyLeftPanel(left);
                EnsureRectMask(left.gameObject);
            }

            ApplyRightPanel(manager.tablePanel);
            ApplyRightPanel(manager.graphPanel);
            ApplyAnalysisPanel(manager.analysisPanel);
        }

        private static RectTransform ResolveMainArea(UIStateManager manager)
        {
            if (manager.leftControlPanel != null && manager.leftControlPanel.transform.parent is RectTransform parent)
            {
                return parent;
            }

            if (manager.tablePanel != null && manager.tablePanel.transform.parent is RectTransform tableParent)
            {
                return tableParent;
            }

            return null;
        }

        private static void ApplyRightPanel(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            RectTransform rect = panel.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            ApplyRightPanelRect(rect);
            EnsureRectMask(panel);
        }

        private static void ApplyAnalysisPanel(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            RectTransform rect = panel.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            ApplyLeftPanel(rect);
            EnsureRectMask(panel);
        }

        private static void ApplyMainArea(RectTransform rect)
        {
            Vector2 safeInset = CalculateSafeInset();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(safeInset.x, safeInset.y);
            rect.offsetMax = new Vector2(-safeInset.x, -(safeInset.y + MainTopInset));
            rect.localScale = Vector3.one;
        }

        private static void ApplyLeftPanel(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(LeftWidth, 0f);
            rect.localScale = Vector3.one;
        }

        private static void ApplyRightPanelRect(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(RightStartX, 0f);
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static Vector2 CalculateSafeInset()
        {
            float aspect = Screen.width > 0 && Screen.height > 0
                ? (float)Screen.width / Screen.height
                : MobileAspectRatioEnforcer.TargetAspect;

            if (aspect < MobileAspectRatioEnforcer.TargetAspect)
            {
                float visibleHeight = DesignWidth / aspect;
                float verticalInset = Mathf.Max(0f, (visibleHeight - DesignHeight) * 0.5f);
                return new Vector2(0f, verticalInset);
            }

            if (aspect > MobileAspectRatioEnforcer.TargetAspect)
            {
                float visibleWidth = DesignHeight * aspect;
                float horizontalInset = Mathf.Max(0f, (visibleWidth - DesignWidth) * 0.5f);
                return new Vector2(horizontalInset, 0f);
            }

            return Vector2.zero;
        }

        private static void EnsureRectMask(GameObject panel)
        {
            if (panel.GetComponent<RectMask2D>() == null)
            {
                panel.AddComponent<RectMask2D>();
            }
        }
    }
}
