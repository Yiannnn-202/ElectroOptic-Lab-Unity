using UnityEngine;

namespace ElectroOptics.UI.ScreenDisplay
{
    /// <summary>
    /// 光屏数据提供者接口
    /// 用于解耦数据源和显示面板
    /// </summary>
    public interface IScreenDataProvider
    {
        /// <summary>
        /// 获取显示纹理
        /// </summary>
        Texture GetTexture();

        /// <summary>
        /// 预渲染一帧（用于切换前准备）
        /// </summary>
        void PreRender();

        /// <summary>
        /// 数据提供者是否可用
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// 模式名称（用于调试）
        /// </summary>
        string ModeName { get; }
    }
}
