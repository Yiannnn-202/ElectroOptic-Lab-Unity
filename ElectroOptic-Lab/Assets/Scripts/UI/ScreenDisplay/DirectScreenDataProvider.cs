using UnityEngine;

namespace ElectroOptics.UI.ScreenDisplay
{
    /// <summary>
    /// 红点追踪数据提供者
    /// 从 DirectScreenController 获取 Texture2D
    /// </summary>
    public class DirectScreenDataProvider : IScreenDataProvider
    {
        private readonly DirectScreenController _controller;

        public DirectScreenDataProvider(DirectScreenController controller)
        {
            _controller = controller;
        }

        public Texture GetTexture()
        {
            return _controller != null ? _controller.SharedTexture : null;
        }

        public void PreRender()
        {
            // DirectScreenController 在自身 Update() 中已持续更新 sharedTexture，无需额外预渲染
        }

        public bool IsAvailable => _controller != null && _controller.SharedTexture != null;

        public string ModeName => "RedDot";
    }
}
