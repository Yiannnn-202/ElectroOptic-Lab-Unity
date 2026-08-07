using UnityEngine;

namespace ElectroOptics.UI.ComponentInfoCard
{
    /// <summary>
    /// 一类实验元件对应的可复用介绍内容。
    /// </summary>
    [CreateAssetMenu(
        fileName = "ComponentInfoCardContent",
        menuName = "ElectroOptics/UI/Component Info Card Content")]
    public sealed class ComponentInfoCardContent : ScriptableObject
    {
        [SerializeField] private string componentName = string.Empty;
        [SerializeField, TextArea(4, 8)] private string description = string.Empty;
        [SerializeField] private Sprite previewSprite;
        [SerializeField] private string previewGifFileName = string.Empty;

        public string ComponentName => componentName;
        public string Description => description;
        public Sprite PreviewSprite => previewSprite;
        public string PreviewGifFileName => previewGifFileName;

        public bool IsValid => !string.IsNullOrWhiteSpace(componentName)
                               && !string.IsNullOrWhiteSpace(description);
    }
}
