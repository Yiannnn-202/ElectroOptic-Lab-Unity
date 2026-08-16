using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ElectroOptics.DataTransfer;
using ElectroOptics.UI.ExperimentGuide;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ElectroOptics.UI.ProgramSettings
{
    /// <summary>
    /// Scene2-only Escape menu. The UI is built in a persistent overlay canvas so the
    /// Scene2 interaction suspension does not disable the menu that requested it.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class Scene2ProgramSettingsCard : MonoBehaviour
    {
        private const string CanvasName = "Scene2ProgramSettingsCanvas";
        private const string SimheiResourcePath = "Fonts/SIMHEI SDF";

        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.70f);
        private static readonly Color PanelColor = new Color(0.025f, 0.10f, 0.16f, 0.99f);
        private static readonly Color AccentColor = new Color(0.25f, 0.86f, 1f, 1f);
        private static readonly Color DangerColor = new Color(0.82f, 0.25f, 0.26f, 1f);

        private GameObject canvasObject;
        private GameObject root;
        private bool isOpen;
        private bool suspendedScene2;
        private bool timeScaleCaptured;
        private float previousTimeScale = 1f;

        public bool IsOpen => isOpen;

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != Scene2AdditionalSceneNavigator.LabSceneName)
                return;

            if (!Input.GetKeyDown(KeyCode.Escape))
                return;

            if (isOpen)
            {
                Close();
                return;
            }

            // This component executes before the existing guide popups. If one of
            // them is open, it remains the sole owner of this Escape press.
            if (!HasHigherPriorityModal())
                Open();
        }

        public void Open()
        {
            if (isOpen)
                return;

            EnsureUi();
            if (root == null)
                return;

            previousTimeScale = Time.timeScale;
            timeScaleCaptured = true;
            Time.timeScale = 0f;

            suspendedScene2 = Scene2AdditionalSceneNavigator.TrySuspendScene2InteractionForModal();
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            isOpen = true;
            Input.ResetInputAxes();
        }

        public void Close()
        {
            if (!isOpen && !timeScaleCaptured && !suspendedScene2)
                return;

            if (root != null)
                root.SetActive(false);

            if (suspendedScene2)
            {
                Scene2AdditionalSceneNavigator.RestoreScene2InteractionForModal();
                suspendedScene2 = false;
            }

            if (timeScaleCaptured)
            {
                Time.timeScale = previousTimeScale;
                timeScaleCaptured = false;
            }

            isOpen = false;
            Input.ResetInputAxes();
        }

        private void ReturnToCrystalSelection()
        {
            Close();
            CrystalSelectionData.TargetSceneName = ExperimentNavigator.DefaultLabSceneName;
            SceneManager.LoadScene(ExperimentNavigator.PreviewSceneName, LoadSceneMode.Single);
        }

        private void QuitApplication()
        {
            Close();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private bool HasHigherPriorityModal()
        {
            Scene2CardGuide[] cards = FindObjectsOfType<Scene2CardGuide>(true);
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null && cards[i].IsModalOpen)
                    return true;
            }

            ExperimentGuidePopup[] guidePopups = FindObjectsOfType<ExperimentGuidePopup>(true);
            for (int i = 0; i < guidePopups.Length; i++)
            {
                if (guidePopups[i] != null && guidePopups[i].IsOpen)
                    return true;
            }

            Scene2GuideGifPopup[] gifPopups = FindObjectsOfType<Scene2GuideGifPopup>(true);
            for (int i = 0; i < gifPopups.Length; i++)
            {
                if (gifPopups[i] != null && gifPopups[i].IsOpen)
                    return true;
            }

            return false;
        }

        private void EnsureUi()
        {
            if (canvasObject != null)
                return;

            EnsureEventSystem();

            canvasObject = new GameObject(CanvasName, typeof(RectTransform));
            DontDestroyOnLoad(canvasObject);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            TMP_FontAsset font = ResolveFont();
            root = CreateRectObject("Scene2ProgramSettingsRoot", canvasObject.transform);
            Stretch(root.GetComponent<RectTransform>());
            Image dimmer = root.AddComponent<Image>();
            dimmer.color = OverlayColor;
            dimmer.raycastTarget = true;

            GameObject panel = CreateRectObject("ProgramSettingsCard", root.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(580f, 350f);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = true;
            UnityEngine.UI.Outline outline = panel.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.55f);
            outline.effectDistance = new Vector2(2f, -2f);

            TextMeshProUGUI title = CreateText(
                "Title",
                panel.transform,
                "程序设置",
                30,
                Color.white,
                TextAlignmentOptions.Center,
                font);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -82f), new Vector2(-30f, -24f));
            title.fontStyle = FontStyles.Bold;

            TextMeshProUGUI hint = CreateText(
                "Hint",
                panel.transform,
                "当前实验已暂停。按 Esc 可返回实验。",
                19,
                new Color(0.80f, 0.92f, 1f, 1f),
                TextAlignmentOptions.Center,
                font);
            SetRect(hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(38f, -20f), new Vector2(-38f, 32f));

            Button returnButton = CreateButton(
                "ReturnToCrystalSelectionButton",
                panel.transform,
                "返回晶体选择",
                AccentColor,
                font);
            SetButtonRect(returnButton, new Vector2(-122f, -104f));
            returnButton.onClick.AddListener(ReturnToCrystalSelection);

            Button quitButton = CreateButton(
                "QuitApplicationButton",
                panel.transform,
                "退出程序",
                DangerColor,
                font);
            SetButtonRect(quitButton, new Vector2(122f, -104f));
            quitButton.onClick.AddListener(QuitApplication);

            root.SetActive(false);
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static Button CreateButton(
            string objectName,
            Transform parent,
            string label,
            Color color,
            TMP_FontAsset font)
        {
            GameObject buttonObject = CreateRectObject(objectName, parent);
            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            TextMeshProUGUI text = CreateText(
                "Label",
                buttonObject.transform,
                label,
                19,
                Color.white,
                TextAlignmentOptions.Center,
                font);
            Stretch(text.rectTransform);
            return button;
        }

        private static TextMeshProUGUI CreateText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            Color color,
            TextAlignmentOptions alignment,
            TMP_FontAsset font)
        {
            GameObject textObject = CreateRectObject(objectName, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            if (font != null)
                text.font = font;
            return text;
        }

        private static GameObject CreateRectObject(string objectName, Transform parent)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void SetButtonRect(Button button, Vector2 anchoredPosition)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(212f, 54f);
            rect.anchoredPosition = anchoredPosition;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TMP_FontAsset ResolveFont()
        {
            TMP_FontAsset resourceFont = Resources.Load<TMP_FontAsset>(SimheiResourcePath);
            if (resourceFont != null)
                return resourceFont;

            TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font != null && texts[i].font.name.Contains("SIMHEI"))
                    return texts[i].font;
            }

            return TMP_Settings.defaultFontAsset;
        }

        private void OnDestroy()
        {
            Close();
            if (canvasObject == null)
                return;

            if (Application.isPlaying)
                Destroy(canvasObject);
            else
                DestroyImmediate(canvasObject);
        }
    }
}
