using UnityEngine;
using ElectroOptics.DataTransfer;

namespace ElectroOptics.UI.ScreenDisplay
{
    /// <summary>
    /// 锥光干涉数据提供者
    /// 从 CrystalRuntime.TextureRenderer 获取 RenderTexture
    /// </summary>
    public class ConoscopicScreenDataProvider : IScreenDataProvider
    {
        public Texture GetTexture()
        {
            if (CrystalRuntime.IsInitialized && CrystalRuntime.TextureRenderer != null)
            {
                return CrystalRuntime.TextureRenderer.RenderTexture;
            }
            return null;
        }

        public void PreRender()
        {
            if (CrystalRuntime.IsInitialized
                && CrystalRuntime.TextureRenderer != null
                && CrystalRuntime.TextureRenderer.IsInitialized)
            {
                CrystalRuntime.TextureRenderer.UpdateAndRender();
            }
        }

        public bool IsAvailable =>
            CrystalRuntime.IsInitialized
            && CrystalRuntime.TextureRenderer != null
            && CrystalRuntime.TextureRenderer.IsInitialized;

        public string ModeName => "Conoscopic";
    }
}
