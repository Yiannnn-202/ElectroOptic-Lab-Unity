using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ElectroOptics.Mobile
{
    /// <summary>
    /// Mobile-only 16:9 letterbox. Keeps tablet rendering consistent with the desktop composition.
    /// </summary>
    public class MobileAspectRatioEnforcer : MonoBehaviour
    {
        public const float TargetAspect = 16f / 9f;

        private const int BarSortingOrder = -100;
        private static bool _created;
        private Rect _lastViewport = new Rect(0f, 0f, 1f, 1f);
        private RectTransform _leftBar;
        private RectTransform _rightBar;
        private RectTransform _topBar;
        private RectTransform _bottomBar;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_created || !MobileRuntime.IsActive)
            {
                return;
            }

            GameObject root = new GameObject("MobileAspectRatioEnforcer");
            DontDestroyOnLoad(root);
            root.AddComponent<MobileAspectRatioEnforcer>();
            _created = true;
        }

        private void Awake()
        {
            EnsureEventSystem();
            BuildBars();
            ApplyAspect();
        }

        private void LateUpdate()
        {
            ApplyAspect();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem");
            DontDestroyOnLoad(eventSystem);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private void ApplyAspect()
        {
            Rect viewport = CalculateViewport();
            if (viewport != _lastViewport)
            {
                _lastViewport = viewport;
                UpdateBars(viewport);
            }

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera != null && camera.enabled && camera.targetTexture == null)
                {
                    camera.rect = viewport;
                }
            }
        }

        private static Rect CalculateViewport()
        {
            float screenAspect = Screen.width > 0 && Screen.height > 0
                ? (float)Screen.width / Screen.height
                : TargetAspect;

            if (screenAspect > TargetAspect)
            {
                float width = TargetAspect / screenAspect;
                float x = (1f - width) * 0.5f;
                return new Rect(x, 0f, width, 1f);
            }

            if (screenAspect < TargetAspect)
            {
                float height = screenAspect / TargetAspect;
                float y = (1f - height) * 0.5f;
                return new Rect(0f, y, 1f, height);
            }

            return new Rect(0f, 0f, 1f, 1f);
        }

        private void BuildBars()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = BarSortingOrder;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _leftBar = CreateBar("LeftBar");
            _rightBar = CreateBar("RightBar");
            _topBar = CreateBar("TopBar");
            _bottomBar = CreateBar("BottomBar");
        }

        private RectTransform CreateBar(string name)
        {
            GameObject bar = new GameObject(name);
            bar.transform.SetParent(transform, false);
            Image image = bar.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            return bar.GetComponent<RectTransform>();
        }

        private void UpdateBars(Rect viewport)
        {
            SetBar(_leftBar, 0f, 0f, viewport.xMin, 1f);
            SetBar(_rightBar, viewport.xMax, 0f, 1f, 1f);
            SetBar(_bottomBar, viewport.xMin, 0f, viewport.xMax, viewport.yMin);
            SetBar(_topBar, viewport.xMin, viewport.yMax, viewport.xMax, 1f);
        }

        private static void SetBar(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.gameObject.SetActive(xMax > xMin && yMax > yMin);
        }
    }
}
