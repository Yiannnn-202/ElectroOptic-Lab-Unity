using UnityEngine;

namespace ElectroOptics.UI.CrystalSelector
{
    /// <summary>
    /// 自定义晶体卡片占位。当前为视觉占位状态，后续可扩展为自定义晶体参数输入面板。
    /// </summary>
    public class CustomCrystalCard : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("点击后跳转的目标场景（未来使用）")]
        private string targetSceneName = "Scene2.The Lab";

        /// <summary>
        /// 卡片点击回调。当前为占位，不执行场景跳转。
        /// </summary>
        public void OnCardClick()
        {
            // 占位：目前不做晶体选择与场景跳转
            // 后续可在此打开自定义晶体配置弹窗，手动输入 nx/ny/nz/r系数 等参数
            Debug.Log("[CustomCrystalCard] 自定义晶体功能尚未开放");
        }
    }
}
