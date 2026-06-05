using UnityEngine;
using UnityEngine.UI;

namespace ElectroOptics.UI.CrystalSelector
{
    /// <summary>
    /// 自定义晶体卡片。
    /// 点击选中卡片（高亮变灰），点击「选择」按钮后弹出 CustomCrystalPanel，
    /// 用户填写参数确认后构造运行时 CrystalProfile 并跳转到目标场景。
    /// </summary>
    public class CustomCrystalCard : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("确认后跳转的目标场景名")]
        private string targetSceneName = "Scene2.The Lab";

        #region 选中状态（静态，跨卡片协调）

        /// <summary>当前选中的自定义晶体卡片</summary>
        private static CustomCrystalCard _currentlySelected;

        /// <summary>卡片背景 Image，用于选中高亮</summary>
        private Image _cardImage;

        /// <summary>卡片原始背景色，用于取消选中时恢复</summary>
        private Color _originalColor;

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            _cardImage = GetComponent<Image>();
            if (_cardImage != null)
                _originalColor = _cardImage.color;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 卡片点击回调。选中卡片（高亮变灰），不直接打开面板。
        /// </summary>
        public void OnCardClick()
        {
            // 取消其他卡片选中，选中当前卡片（视觉高亮）
            CrystalCardSelector.DeselectAll();
            _currentlySelected = this;
            if (_cardImage != null)
                _cardImage.color = new Color(0.5f, 0.5f, 0.58f, 1f);

            // 直接打开自定义晶体参数面板
            Debug.Log("[CustomCrystalCard] 已选中自定义晶体，打开参数面板");
            CustomCrystalPanel.Show(targetSceneName);
        }

        /// <summary>
        /// 取消当前卡片的选中高亮
        /// </summary>
        public void Deselect()
        {
            if (_cardImage != null)
                _cardImage.color = _originalColor;
        }

        #endregion

        #region 选中管理（供 CrystalCardSelector 调用）

        /// <summary>
        /// 取消自定义卡片选中（由 CrystalCardSelector.DeselectAll 调用）
        /// </summary>
        public static void DeselectCurrent()
        {
            if (_currentlySelected != null)
            {
                _currentlySelected.Deselect();
                _currentlySelected = null;
            }
        }

        /// <summary>
        /// 确认自定义卡片选中（由 CrystalCardSelector.ConfirmSelection 调用）
        /// </summary>
        public static void ConfirmCurrentSelection()
        {
            if (_currentlySelected != null)
            {
                Debug.Log("[CustomCrystalCard] 确认选择，打开自定义晶体参数面板");
                CustomCrystalPanel.Show(_currentlySelected.targetSceneName);
            }
        }

        #endregion
    }
}
