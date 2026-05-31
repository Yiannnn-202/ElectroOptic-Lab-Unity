using UnityEngine;

namespace ElectroOptics.UI.CrystalSelector
{
    /// <summary>
    /// 自定义晶体卡片。
    /// 点击后弹出 CustomCrystalPanel 参数输入面板，
    /// 用户填写参数确认后构造运行时 CrystalProfile 并跳转到目标场景。
    /// </summary>
    public class CustomCrystalCard : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("确认后跳转的目标场景名")]
        private string targetSceneName = "Scene2.The Lab";

        /// <summary>
        /// 卡片点击回调。打开自定义晶体参数面板。
        /// </summary>
        public void OnCardClick()
        {
            Debug.Log("[CustomCrystalCard] 打开自定义晶体参数面板");
            CustomCrystalPanel.Show(targetSceneName);
        }
    }
}
