using UnityEngine;
using ElectroOptics.Experiment.Controller;
using ElectroOptics.Experiment.Renderer;

namespace ElectroOptics.DataTransfer
{
    /// <summary>
    /// 晶体运行时状态和引用
    /// 在 Scene2 中有效，提供晶体相关组件的静态访问
    /// </summary>
    public static class CrystalRuntime
    {
        /// <summary>
        /// 晶体 GameObject
        /// </summary>
        public static GameObject CrystalObject { get; set; }

        /// <summary>
        /// 晶体控制器包装器
        /// </summary>
        public static CrystalControllerWrapper Controller { get; set; }

        /// <summary>
        /// 锥光干涉纹理渲染器
        /// </summary>
        public static ConoscopicTextureRenderer TextureRenderer { get; set; }

        /// <summary>
        /// 晶体物理核心
        /// </summary>
        public static CrystalPhysicalCore PhysicalCore { get; set; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public static bool IsInitialized => CrystalObject != null && Controller != null;

        /// <summary>
        /// 清理所有引用
        /// 在场景切换或晶体销毁时调用
        /// </summary>
        public static void Clear()
        {
            CrystalObject = null;
            Controller = null;
            TextureRenderer = null;
            PhysicalCore = null;
        }

        /// <summary>
        /// 检查晶体是否在导轨上（通过位置检测）
        /// </summary>
        /// <param name="railPosition">导轨位置</param>
        /// <param name="detectionRange">检测范围</param>
        /// <returns>是否在导轨上</returns>
        public static bool IsCrystalOnRail(Vector3 railPosition, float detectionRange)
        {
            if (!IsInitialized || CrystalObject == null)
                return false;

            float distance = Vector3.Distance(CrystalObject.transform.position, railPosition);
            return distance <= detectionRange;
        }
    }
}
