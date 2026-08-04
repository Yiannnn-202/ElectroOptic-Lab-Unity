using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ElectroOptics.UI.ExperimentGuide
{
    /// <summary>
    /// Scene2 实时指引卡片视图。只负责显示和展开/收起动画。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Scene2CardGuideView : MonoBehaviour
    {
        private static readonly Color AccentColor = new Color(0.25f, 0.86f, 1f, 0.95f);
        private static readonly Color CardColor = new Color(0.03f, 0.10f, 0.16f, 0.86f);
        private static readonly Color HeaderColor = new Color(0.04f, 0.18f, 0.26f, 0.96f);
        private static readonly Color FeedbackColor = new Color(0.20f, 0.88f, 0.58f, 0.98f);

        [Header("Root")]
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private Image cardImage;
        [SerializeField] private UnityEngine.UI.Outline cardOutline;

        [Header("Header")]
        [SerializeField] private Button headerButton;
        [SerializeField] private Image headerImage;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI toggleIconText;

        [Header("Expanded content")]
        [SerializeField] private CanvasGroup expandedContentGroup;
        [SerializeField] private RectTransform expandedContentRect;
        [SerializeField] private TextMeshProUGUI stageTitleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button videoButton;

        private float expandedHeight = 240f;
        private float collapsedHeight = 52f;
        private float animationSeconds = 0.2f;
        private bool targetExpanded = true;
        private bool visible = true;

        public Button HeaderButton => headerButton;
        public Button VideoButton => videoButton;
        public bool IsExpanded => targetExpanded;
        public bool IsValid => rootGroup != null
                               && cardRect != null
                               && cardImage != null
                               && cardOutline != null
                               && headerButton != null
                               && headerImage != null
                               && headerText != null
                               && toggleIconText != null
                               && expandedContentGroup != null
                               && expandedContentRect != null
                               && stageTitleText != null
                               && bodyText != null
                               && statusText != null
                               && videoButton != null;

        private void Update()
        {
            if (!IsValid)
                return;

            float targetHeight = targetExpanded ? expandedHeight : collapsedHeight;
            float currentHeight = cardRect.sizeDelta.y;
            float distance = Mathf.Abs(expandedHeight - collapsedHeight);
            float speed = animationSeconds <= 0f ? float.MaxValue : distance / animationSeconds;
            float nextHeight = Mathf.MoveTowards(currentHeight, targetHeight, speed * Time.unscaledDeltaTime);
            cardRect.sizeDelta = new Vector2(cardRect.sizeDelta.x, nextHeight);

            float expansion = distance <= 0.001f
                ? (targetExpanded ? 1f : 0f)
                : Mathf.InverseLerp(collapsedHeight, expandedHeight, nextHeight);
            expandedContentGroup.alpha = expansion;
            bool contentInteractive = targetExpanded && expansion >= 0.99f && visible;
            expandedContentGroup.interactable = contentInteractive;
            expandedContentGroup.blocksRaycasts = contentInteractive;
        }

        public void ConfigureAnimation(float seconds)
        {
            animationSeconds = Mathf.Max(0f, seconds);
        }

        public void SetVisible(bool shouldShow)
        {
            visible = shouldShow;
            if (rootGroup == null)
                return;
            rootGroup.alpha = shouldShow ? 1f : 0f;
            rootGroup.interactable = shouldShow;
            rootGroup.blocksRaycasts = shouldShow;
            if (!shouldShow && expandedContentGroup != null)
            {
                expandedContentGroup.interactable = false;
                expandedContentGroup.blocksRaycasts = false;
            }
        }

        public void SetExpanded(bool expanded, bool immediate)
        {
            targetExpanded = expanded;
            if (toggleIconText != null)
                toggleIconText.text = expanded ? "▲" : "▼";

            if (!immediate || cardRect == null || expandedContentGroup == null)
                return;

            cardRect.sizeDelta = new Vector2(cardRect.sizeDelta.x, expanded ? expandedHeight : collapsedHeight);
            expandedContentGroup.alpha = expanded ? 1f : 0f;
            expandedContentGroup.interactable = expanded && visible;
            expandedContentGroup.blocksRaycasts = expanded && visible;
        }

        public void SetStage(
            int oneBasedIndex,
            int total,
            string title,
            string body,
            string status,
            bool finished)
        {
            if (!IsValid)
                return;

            if (finished)
            {
                headerText.text = "实时操作指引｜已完成";
                stageTitleText.text = "实验操作指引已完成";
                bodyText.text = "已完成全部装置搭建与探头切换，可以继续后续实验操作。";
                statusText.text = string.Empty;
                videoButton.gameObject.SetActive(false);
                return;
            }

            headerText.text = $"实时操作指引｜阶段 {oneBasedIndex}/{total}｜{title}";
            stageTitleText.text = title ?? string.Empty;
            bodyText.text = body ?? string.Empty;
            statusText.text = status ?? string.Empty;
            videoButton.gameObject.SetActive(true);
        }

        public void SetCompletionFeedback(bool active)
        {
            if (!IsValid)
                return;
            Color accent = active ? FeedbackColor : AccentColor;
            headerImage.color = active ? new Color(0.06f, 0.36f, 0.28f, 0.98f) : HeaderColor;
            cardOutline.effectColor = new Color(accent.r, accent.g, accent.b, active ? 0.90f : 0.35f);
            toggleIconText.color = accent;
        }

        public static Scene2CardGuideView Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = CreateRectObject("Scene2CardGuideRoot", parent);
            Stretch(root.GetComponent<RectTransform>());
            CanvasGroup rootCanvasGroup = root.AddComponent<CanvasGroup>();

            Scene2CardGuideView view = root.AddComponent<Scene2CardGuideView>();
            view.rootGroup = rootCanvasGroup;

            GameObject cardObject = CreateRectObject("GuideCard", root.transform);
            RectTransform card = cardObject.GetComponent<RectTransform>();
            card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = new Vector2(32f, -32f);
            card.sizeDelta = new Vector2(460f, 240f);
            view.cardRect = card;
            view.cardImage = cardObject.AddComponent<Image>();
            view.cardImage.color = CardColor;
            view.cardImage.raycastTarget = true;
            view.cardOutline = cardObject.AddComponent<UnityEngine.UI.Outline>();
            view.cardOutline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.35f);
            view.cardOutline.effectDistance = new Vector2(1f, -1f);

            GameObject headerObject = CreateRectObject("Header", card);
            RectTransform headerRect = headerObject.GetComponent<RectTransform>();
            SetRect(headerRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -52f), Vector2.zero);
            view.headerImage = headerObject.AddComponent<Image>();
            view.headerImage.color = HeaderColor;
            view.headerButton = headerObject.AddComponent<Button>();
            view.headerButton.targetGraphic = view.headerImage;

            view.headerText = CreateText(
                "HeaderText",
                headerObject.transform,
                "实时操作指引｜阶段 1/6｜放置光屏",
                17,
                Color.white,
                TextAlignmentOptions.MidlineLeft,
                font);
            SetRect(view.headerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-54f, 0f));

            view.toggleIconText = CreateText(
                "ToggleIcon",
                headerObject.transform,
                "▲",
                20,
                AccentColor,
                TextAlignmentOptions.Center,
                font);
            SetRect(view.toggleIconText.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-50f, 0f), Vector2.zero);

            GameObject contentObject = CreateRectObject("ExpandedContent", card);
            view.expandedContentRect = contentObject.GetComponent<RectTransform>();
            SetRect(view.expandedContentRect, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -52f));
            view.expandedContentGroup = contentObject.AddComponent<CanvasGroup>();

            view.stageTitleText = CreateText(
                "StageTitle",
                contentObject.transform,
                "放置光屏",
                24,
                Color.white,
                TextAlignmentOptions.MidlineLeft,
                font);
            view.stageTitleText.fontStyle = FontStyles.Bold;
            SetRect(view.stageTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -48f), new Vector2(-22f, -10f));

            view.bodyText = CreateText(
                "BodyText",
                contentObject.transform,
                string.Empty,
                17,
                new Color(0.92f, 0.97f, 1f, 1f),
                TextAlignmentOptions.TopLeft,
                font);
            view.bodyText.lineSpacing = 1.5f;
            SetRect(view.bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -126f), new Vector2(-22f, -50f));

            view.statusText = CreateText(
                "StatusText",
                contentObject.transform,
                string.Empty,
                14,
                new Color(1f, 0.82f, 0.36f, 1f),
                TextAlignmentOptions.MidlineLeft,
                font);
            SetRect(view.statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(22f, 47f), new Vector2(-160f, 75f));

            view.videoButton = CreateButton(
                "VideoButton",
                contentObject.transform,
                "视频指引",
                new Vector2(124f, 36f),
                AccentColor,
                font);
            RectTransform videoRect = view.videoButton.GetComponent<RectTransform>();
            SetRect(videoRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-146f, 17f), new Vector2(-22f, 53f));

            view.SetVisible(true);
            view.SetExpanded(true, true);
            return view;
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

            TextMeshProUGUI labelText = CreateText(
                "Label",
                obj.transform,
                label,
                16,
                Color.white,
                TextAlignmentOptions.Center,
                font);
            Stretch(labelText.rectTransform);
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
            rect.pivot = new Vector2(0.5f, 0.5f);
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
