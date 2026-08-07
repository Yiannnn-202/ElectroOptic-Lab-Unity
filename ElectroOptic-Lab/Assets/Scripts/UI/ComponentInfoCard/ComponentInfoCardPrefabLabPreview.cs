using UnityEngine;

namespace ElectroOptics.UI.ComponentInfoCard
{
    /// <summary>Play-mode-only preview driver used by the dedicated Prefab Lab scene.</summary>
    [DisallowMultipleComponent]
    public sealed class ComponentInfoCardPrefabLabPreview : MonoBehaviour
    {
        [SerializeField] private ComponentInfoCardView cardView;
        [SerializeField] private ComponentInfoCardGifPlayer gifPlayer;
        [SerializeField] private string gifFileName = "Laser.gif";

        public string GifFileName => gifFileName;
        public bool IsValid => cardView != null
                               && cardView.IsValid
                               && gifPlayer != null
                               && gifPlayer.IsValid
                               && ComponentInfoCardGifPlayer.IsSafeGifFileName(gifFileName);

        public void Configure(
            ComponentInfoCardView view,
            ComponentInfoCardGifPlayer player,
            string fileName)
        {
            cardView = view;
            gifPlayer = player;
            gifFileName = fileName;
        }

        private void Start()
        {
            if (!IsValid)
            {
                Debug.LogWarning("[ComponentInfoCard] Prefab Lab GIF 预览驱动引用不完整。", this);
                return;
            }

            cardView.gameObject.SetActive(true);
            gifPlayer.Play(gifFileName);
        }

        private void OnDisable()
        {
            gifPlayer?.Stop();
        }
    }
}
