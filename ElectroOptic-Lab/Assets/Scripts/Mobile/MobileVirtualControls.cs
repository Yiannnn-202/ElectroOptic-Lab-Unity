using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElectroOptics.Mobile
{
    /// <summary>
    /// Runtime-created controls for mobile builds so keyboard-only lab actions remain reachable.
    /// </summary>
    public class MobileVirtualControls : MonoBehaviour
    {
        private const int SortingOrder = 1000;
        private static bool _created;
        private Canvas _canvas;
        private GameObject _wButton;
        private GameObject _aButton;
        private GameObject _sButton;
        private GameObject _dButton;
        private GameObject _dropButton;
        private GameObject _enterButton;
        private string _lastSceneName = "";
        private float _lastAspect = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_created || !MobileRuntime.IsActive)
            {
                return;
            }

            GameObject root = new GameObject("MobileVirtualControls");
            DontDestroyOnLoad(root);
            root.AddComponent<MobileVirtualControls>();
            _created = true;
        }

        private void Awake()
        {
            EnsureEventSystem();
            BuildUi();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            RefreshVisibility(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            MobileVirtualInput.ReleaseAll();
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            MobileVirtualInput.ReleaseAll();
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            RefreshVisibility(nextScene);
        }

        private void LateUpdate()
        {
            float aspect = Screen.width > 0 && Screen.height > 0
                ? (float)Screen.width / Screen.height
                : MobileAspectRatioEnforcer.TargetAspect;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != _lastSceneName || !Mathf.Approximately(aspect, _lastAspect))
            {
                RefreshVisibility(scene);
            }
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

        private void BuildUi()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            RectTransform root = gameObject.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            float leftInset = GetSixteenNineLeftInset();
            float bottomInset = GetSixteenNineBottomInset();
            float actionY = bottomInset + 100f;
            float rowY = bottomInset + 210f;
            float topY = bottomInset + 320f;

            _wButton = CreateKeyButton("W", KeyCode.W, new Vector2(leftInset + 190f, topY), new Vector2(92f, 92f));
            _aButton = CreateKeyButton("A", KeyCode.A, new Vector2(leftInset + 85f, rowY), new Vector2(92f, 92f));
            _sButton = CreateKeyButton("S", KeyCode.S, new Vector2(leftInset + 190f, rowY), new Vector2(92f, 92f));
            _dButton = CreateKeyButton("D", KeyCode.D, new Vector2(leftInset + 295f, rowY), new Vector2(92f, 92f));
            _dropButton = CreateKeyButton("Drop", KeyCode.Space, new Vector2(leftInset + 115f, actionY), new Vector2(150f, 86f));
            _enterButton = CreateKeyButton("Enter", KeyCode.Return, new Vector2(leftInset + 290f, actionY), new Vector2(150f, 86f));
        }

        private static float GetSixteenNineLeftInset()
        {
            float screenAspect = Screen.width > 0 && Screen.height > 0
                ? (float)Screen.width / Screen.height
                : 16f / 9f;
            float targetAspect = MobileAspectRatioEnforcer.TargetAspect;
            if (screenAspect <= targetAspect)
            {
                return 0f;
            }

            float viewportWidth = targetAspect / screenAspect;
            return (1f - viewportWidth) * 0.5f * 1920f;
        }

        private static float GetSixteenNineBottomInset()
        {
            float screenAspect = Screen.width > 0 && Screen.height > 0
                ? (float)Screen.width / Screen.height
                : 16f / 9f;
            float targetAspect = MobileAspectRatioEnforcer.TargetAspect;
            if (screenAspect >= targetAspect)
            {
                return 0f;
            }

            float viewportHeight = screenAspect / targetAspect;
            return (1f - viewportHeight) * 0.5f * 1080f;
        }

        private void RefreshVisibility(Scene scene)
        {
            if (_canvas == null)
            {
                return;
            }

            string sceneName = scene.name;
            _lastSceneName = sceneName;
            _lastAspect = Screen.width > 0 && Screen.height > 0
                ? (float)Screen.width / Screen.height
                : MobileAspectRatioEnforcer.TargetAspect;

            bool shouldShow = sceneName.Contains("Scene2.The Lab") || sceneName.Contains("Scene4");
            _canvas.enabled = shouldShow;
            if (!shouldShow)
            {
                MobileVirtualInput.ReleaseAll();
            }

            if (sceneName.Contains("Scene4"))
            {
                ApplyScene4Controls();
            }
            else
            {
                ApplyLabControls();
            }
        }

        private void ApplyLabControls()
        {
            float leftInset = GetSixteenNineLeftInset();
            float bottomInset = GetSixteenNineBottomInset();
            SetButton(_wButton, true, new Vector2(leftInset + 190f, bottomInset + 320f), new Vector2(92f, 92f));
            SetButton(_aButton, true, new Vector2(leftInset + 85f, bottomInset + 210f), new Vector2(92f, 92f));
            SetButton(_sButton, true, new Vector2(leftInset + 190f, bottomInset + 210f), new Vector2(92f, 92f));
            SetButton(_dButton, true, new Vector2(leftInset + 295f, bottomInset + 210f), new Vector2(92f, 92f));
            SetButton(_dropButton, true, new Vector2(leftInset + 115f, bottomInset + 100f), new Vector2(150f, 86f));
            SetButton(_enterButton, true, new Vector2(leftInset + 290f, bottomInset + 100f), new Vector2(150f, 86f));
            SetButtonLabel(_aButton, "A");
            SetButtonLabel(_dButton, "D");
        }

        private void ApplyScene4Controls()
        {
            float leftInset = GetSixteenNineLeftInset();
            float bottomInset = GetSixteenNineBottomInset();
            MobileVirtualInput.SetKey(KeyCode.W, false);
            MobileVirtualInput.SetKey(KeyCode.S, false);
            MobileVirtualInput.SetKey(KeyCode.Space, false);
            MobileVirtualInput.SetKey(KeyCode.Return, false);
            SetButton(_wButton, false, Vector2.zero, Vector2.zero);
            SetButton(_sButton, false, Vector2.zero, Vector2.zero);
            SetButton(_dropButton, false, Vector2.zero, Vector2.zero);
            SetButton(_enterButton, false, Vector2.zero, Vector2.zero);
            SetButton(_aButton, true, new Vector2(leftInset + 118f, bottomInset + 763f), new Vector2(92f, 72f));
            SetButton(_dButton, true, new Vector2(leftInset + 118f, bottomInset + 678f), new Vector2(92f, 72f));
            SetButtonLabel(_aButton, "↑");
            SetButtonLabel(_dButton, "↓");
        }

        private static void SetButton(GameObject buttonObject, bool active, Vector2 anchoredPosition, Vector2 size)
        {
            if (buttonObject == null)
            {
                return;
            }

            buttonObject.SetActive(active);
            if (!active)
            {
                return;
            }

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void SetButtonLabel(GameObject buttonObject, string label)
        {
            if (buttonObject == null)
            {
                return;
            }

            Text text = buttonObject.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
                text.fontSize = label.Length == 1 ? 42 : 26;
            }
        }

        private GameObject CreateKeyButton(string label, KeyCode key, Vector2 anchoredPosition, Vector2 size, bool anchorRight = false)
        {
            GameObject buttonObject = new GameObject("MobileKey_" + label);
            buttonObject.transform.SetParent(transform, false);

            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            Vector2 anchor = anchorRight ? new Vector2(1f, 0f) : Vector2.zero;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.45f);

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.55f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.75f);
            colors.pressedColor = new Color(0.4f, 0.8f, 1f, 0.85f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            EventTrigger trigger = buttonObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerDown, _ => MobileVirtualInput.SetKey(key, true));
            AddTrigger(trigger, EventTriggerType.PointerUp, _ => MobileVirtualInput.SetKey(key, false));
            AddTrigger(trigger, EventTriggerType.PointerExit, _ => MobileVirtualInput.SetKey(key, false));
            AddTrigger(trigger, EventTriggerType.EndDrag, _ => MobileVirtualInput.SetKey(key, false));

            CreateLabel(buttonObject.transform, label);
            return buttonObject;
        }

        private static void CreateLabel(Transform parent, string label)
        {
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);

            RectTransform rect = labelObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = labelObject.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = label.Length > 1 ? 26 : 38;
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
            text.font = ResolveDefaultFont();
        }

        private static Font ResolveDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }
    }
}
