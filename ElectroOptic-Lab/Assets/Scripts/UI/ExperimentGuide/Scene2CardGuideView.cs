using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ElectroOptics.UI.ExperimentGuide
{
    /// <summary>
    /// Scene2 卡片 UI 的场景视图。
    /// 该对象及其子物体可以直接在 Hierarchy/Inspector 中编辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Scene2CardGuideView : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private RectTransform cardRect;

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI stepText;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI closeButtonText;
        [SerializeField] private Button restartButton;

        [Header("Pointer")]
        [SerializeField] private RectTransform lineRect;
        [SerializeField] private RectTransform arrowRect;
        [SerializeField] private RectTransform markerRect;
        [SerializeField] private RectTransform cursorRect;

        public CanvasGroup RootGroup => rootGroup;
        public RectTransform CardRect => cardRect;
        public TextMeshProUGUI StepText => stepText;
        public TextMeshProUGUI TitleText => titleText;
        public TextMeshProUGUI BodyText => bodyText;
        public Button CloseButton => closeButton;
        public TextMeshProUGUI CloseButtonText => closeButtonText;
        public Button RestartButton => restartButton;
        public RectTransform LineRect => lineRect;
        public RectTransform ArrowRect => arrowRect;
        public RectTransform MarkerRect => markerRect;
        public RectTransform CursorRect => cursorRect;

        public bool IsValid =>
            rootGroup != null
            && cardRect != null
            && stepText != null
            && titleText != null
            && bodyText != null
            && closeButton != null
            && closeButtonText != null
            && restartButton != null
            && lineRect != null
            && arrowRect != null
            && markerRect != null
            && cursorRect != null;

        public void Configure(
            CanvasGroup configuredRootGroup,
            RectTransform configuredCardRect,
            TextMeshProUGUI configuredStepText,
            TextMeshProUGUI configuredTitleText,
            TextMeshProUGUI configuredBodyText,
            Button configuredCloseButton,
            TextMeshProUGUI configuredCloseButtonText,
            Button configuredRestartButton,
            RectTransform configuredLineRect,
            RectTransform configuredArrowRect,
            RectTransform configuredMarkerRect,
            RectTransform configuredCursorRect)
        {
            rootGroup = configuredRootGroup;
            cardRect = configuredCardRect;
            stepText = configuredStepText;
            titleText = configuredTitleText;
            bodyText = configuredBodyText;
            closeButton = configuredCloseButton;
            closeButtonText = configuredCloseButtonText;
            restartButton = configuredRestartButton;
            lineRect = configuredLineRect;
            arrowRect = configuredArrowRect;
            markerRect = configuredMarkerRect;
            cursorRect = configuredCursorRect;
        }
    }
}
