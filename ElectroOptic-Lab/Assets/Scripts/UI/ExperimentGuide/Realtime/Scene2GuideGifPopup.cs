using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ElectroOptics.UI.ExperimentGuide
{
    [DisallowMultipleComponent]
    public sealed class Scene2GuideGifPopup : MonoBehaviour
    {
        private const float DefaultMediaAspectRatio = 16f / 9f;
        private const float MediaViewportHeight = 482f;
        private static readonly Color AccentColor = new Color(0.25f, 0.86f, 1f, 0.98f);
        private static readonly Color PanelColor = new Color(0.025f, 0.10f, 0.16f, 0.98f);

        private Scene2GuideInteractionLock interactionLock;
        private Scene2GuideGifPlayer player;
        private IDisposable lockToken;
        private GameObject root;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI messageText;
        private RawImage mediaImage;
        private AspectRatioFitter mediaFitter;
        private Button closeButton;
        private bool initialized;

        public bool IsOpen { get; private set; }
        public event Action<bool> OpenStateChanged;

        public void Initialize(
            Canvas canvas,
            TMP_FontAsset font,
            Scene2GuideInteractionLock modalInteractionLock,
            int frameBufferCapacity)
        {
            if (initialized)
                return;

            interactionLock = modalInteractionLock;
            player = gameObject.GetComponent<Scene2GuideGifPlayer>();
            if (player == null)
                player = gameObject.AddComponent<Scene2GuideGifPlayer>();
            player.Configure(frameBufferCapacity);
            player.FrameChanged += OnFrameChanged;
            player.StateChanged += OnPlayerStateChanged;

            BuildUi(canvas.transform, font);
            root.SetActive(false);
            initialized = true;
        }

        public void Open(Scene2GuideStageDefinition definition)
        {
            if (!initialized || definition == null || IsOpen)
                return;

            IsOpen = true;
            titleText.text = definition.title + "｜视频指引";
            messageText.text = "正在加载视频指引……";
            messageText.gameObject.SetActive(true);
            mediaImage.texture = null;
            mediaImage.gameObject.SetActive(false);
            root.SetActive(true);
            root.transform.SetAsLastSibling();

            lockToken = interactionLock != null ? interactionLock.Acquire() : null;
            Input.ResetInputAxes();
            OpenStateChanged?.Invoke(true);
            player.Open(definition.id, definition.gifFileName);
        }

        public void Close()
        {
            if (!initialized)
                return;

            bool wasOpen = IsOpen;
            IsOpen = false;
            if (player != null)
                player.Close();
            if (mediaImage != null)
            {
                mediaImage.texture = null;
                mediaImage.gameObject.SetActive(false);
            }
            if (root != null)
                root.SetActive(false);

            try
            {
                lockToken?.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogError("[Scene2RealtimeGuide] 关闭视频弹窗时恢复交互失败：" + exception, this);
                interactionLock?.ForceReleaseAll();
            }
            finally
            {
                lockToken = null;
                Input.ResetInputAxes();
            }

            if (wasOpen)
                OpenStateChanged?.Invoke(false);
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        private void OnFrameChanged(Texture texture)
        {
            if (!IsOpen || texture == null)
                return;

            mediaImage.texture = texture;
            mediaFitter.aspectRatio = texture.height > 0
                ? (float)texture.width / texture.height
                : DefaultMediaAspectRatio;
            mediaImage.gameObject.SetActive(true);
            messageText.gameObject.SetActive(false);
        }

        private void OnPlayerStateChanged(GuideGifLoadState state, string detail)
        {
            if (!IsOpen)
                return;

            switch (state)
            {
                case GuideGifLoadState.Loading:
                    ShowMessage("正在加载视频指引……");
                    break;
                case GuideGifLoadState.Missing:
                    ShowMessage("该阶段视频指引待补充");
                    break;
                case GuideGifLoadState.Failed:
                    ShowMessage("视频加载失败：" + detail);
                    break;
                case GuideGifLoadState.Playing:
                    messageText.gameObject.SetActive(false);
                    break;
            }
        }

        private void ShowMessage(string message)
        {
            mediaImage.gameObject.SetActive(false);
            messageText.text = message;
            messageText.gameObject.SetActive(true);
        }

        private void BuildUi(Transform parent, TMP_FontAsset font)
        {
            root = CreateRectObject("Scene2GuideGifModalRoot", parent);
            Stretch(root.GetComponent<RectTransform>());
            Image dimmer = root.AddComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.68f);
            dimmer.raycastTarget = true;

            GameObject panel = CreateRectObject("ModalPanel", root.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(960f, 640f);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = true;
            UnityEngine.UI.Outline outline = panel.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.55f);
            outline.effectDistance = new Vector2(2f, -2f);

            titleText = CreateText(
                "TitleText",
                panel.transform,
                "视频指引",
                24,
                Color.white,
                TextAlignmentOptions.MidlineLeft,
                font);
            SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -62f), new Vector2(-28f, -16f));
            titleText.fontStyle = FontStyles.Bold;

            GameObject viewport = CreateRectObject("MediaViewport", panel.transform);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.sizeDelta = new Vector2(MediaViewportHeight * DefaultMediaAspectRatio, MediaViewportHeight);
            viewportRect.anchoredPosition = new Vector2(0f, 3f);
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = PanelColor;
            viewportImage.raycastTarget = false;

            GameObject media = CreateRectObject("GifImage", viewport.transform);
            mediaImage = media.AddComponent<RawImage>();
            mediaImage.color = Color.white;
            mediaImage.raycastTarget = false;
            mediaFitter = media.AddComponent<AspectRatioFitter>();
            mediaFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            mediaFitter.aspectRatio = DefaultMediaAspectRatio;

            messageText = CreateText(
                "MessageText",
                viewport.transform,
                "正在加载视频指引……",
                20,
                new Color(0.80f, 0.92f, 1f, 1f),
                TextAlignmentOptions.Center,
                font);
            Stretch(messageText.rectTransform);

            closeButton = CreateButton("CloseButton", panel.transform, "关闭", new Vector2(132f, 42f), AccentColor, font);
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 22f);
            closeButton.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            Close();
            if (player != null)
            {
                player.FrameChanged -= OnFrameChanged;
                player.StateChanged -= OnPlayerStateChanged;
            }
        }

        private static Button CreateButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 size,
            Color color,
            TMP_FontAsset font)
        {
            GameObject obj = CreateRectObject(objectName, parent);
            obj.GetComponent<RectTransform>().sizeDelta = size;
            Image image = obj.AddComponent<Image>();
            image.color = color;
            Button button = obj.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.15f);
            button.colors = colors;
            TextMeshProUGUI text = CreateText("Label", obj.transform, label, 17, Color.white, TextAlignmentOptions.Center, font);
            Stretch(text.rectTransform);
            return button;
        }

        private static TextMeshProUGUI CreateText(
            string objectName,
            Transform parent,
            string value,
            int size,
            Color color,
            TextAlignmentOptions alignment,
            TMP_FontAsset font)
        {
            GameObject obj = CreateRectObject(objectName, parent);
            TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
    }
}
