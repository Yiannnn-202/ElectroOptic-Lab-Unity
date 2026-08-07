using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ElectroOptics.UI.ComponentInfoCard
{
    /// <summary>
    /// 元件介绍卡的纯显示层。悬停计时、目标识别和显隐策略由后续控制器负责。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComponentInfoCardView : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private Image baseImage;
        [SerializeField] private Image headerImage;

        [Header("Content")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image previewImage;
        [SerializeField] private RawImage animatedPreviewImage;
        [SerializeField] private AspectRatioFitter animatedPreviewFitter;
        [SerializeField] private ComponentInfoCardGridGraphic gridGraphic;

        public CanvasGroup RootGroup => rootGroup;
        public RectTransform CardRect => cardRect;
        public Image BaseImage => baseImage;
        public Image HeaderImage => headerImage;
        public TextMeshProUGUI TitleText => titleText;
        public TextMeshProUGUI DescriptionText => descriptionText;
        public Image PreviewImage => previewImage;
        public RawImage AnimatedPreviewImage => animatedPreviewImage;
        public AspectRatioFitter AnimatedPreviewFitter => animatedPreviewFitter;
        public ComponentInfoCardGridGraphic GridGraphic => gridGraphic;

        public bool IsValid => rootGroup != null
                               && cardRect != null
                               && baseImage != null
                               && headerImage != null
                               && titleText != null
                               && descriptionText != null
                               && previewImage != null
                               && animatedPreviewImage != null
                               && animatedPreviewFitter != null
                               && gridGraphic != null;

        /// <summary>
        /// 更新卡片内容。空预览图会自动隐藏预览 Image，但保留预览插槽。
        /// </summary>
        public void SetContent(string componentName, string description, Sprite previewSprite)
        {
            if (titleText != null)
                titleText.text = componentName ?? string.Empty;

            if (descriptionText != null)
                descriptionText.text = description ?? string.Empty;

            if (previewImage == null)
                return;

            previewImage.sprite = previewSprite;
            ClearAnimatedPreview();
        }

        /// <summary>Displays a runtime-decoded frame while preserving its source aspect ratio.</summary>
        public void SetAnimatedPreview(Texture texture)
        {
            if (texture == null)
            {
                ClearAnimatedPreview();
                return;
            }

            if (animatedPreviewImage != null)
            {
                animatedPreviewImage.texture = texture;
                animatedPreviewImage.enabled = true;
            }

            if (animatedPreviewFitter != null && texture.height > 0)
                animatedPreviewFitter.aspectRatio = (float)texture.width / texture.height;

            if (previewImage != null)
                previewImage.enabled = false;
        }

        /// <summary>Clears the animated frame and restores the configured static fallback.</summary>
        public void ClearAnimatedPreview()
        {
            if (animatedPreviewImage != null)
            {
                animatedPreviewImage.texture = null;
                animatedPreviewImage.enabled = false;
            }

            if (previewImage != null)
                previewImage.enabled = previewImage.sprite != null;
        }
    }
}
