using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ElectroOptics.DataTransfer;
using ElectroOptics.Experiment.Interfaces;

namespace ElectroOptics.UI.CrystalSelector
{
    /// <summary>
    /// 晶体卡片选择器
    /// 挂载在 Scene2-preview 的卡片对象上，处理卡片点击和场景切换
    /// </summary>
    public class CrystalCardSelector : MonoBehaviour, ICrystalSelectable
    {
        #region Inspector 配置

        [Header("晶体配置")]
        [Tooltip("绑定的晶体 Profile 资源")]
        [SerializeField] private CrystalProfile crystalProfile;

        [Header("显示信息")]
        [Tooltip("卡片显示名称")]
        [SerializeField] private string displayName = "KDP 晶体";

        [Tooltip("卡片描述文本")]
        [SerializeField] [TextArea(2, 4)] private string description = "磷酸二氢钾晶体";

        [Header("场景配置")]
        [Tooltip("目标场景名称")]
        [SerializeField] private string targetSceneName = "Scene2.The Lab";

        #endregion

        #region 选中状态（静态，跨卡片协调）

        /// <summary>当前选中的晶体卡片</summary>
        private static CrystalCardSelector _currentlySelected;

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

        #region ICrystalSelectable 实现

        /// <inheritdoc/>
        public CrystalProfile GetCrystalProfile() => crystalProfile;

        /// <inheritdoc/>
        public string GetDisplayName() => displayName;

        /// <inheritdoc/>
        public string GetDescription() => description;

        /// <inheritdoc/>
        public void OnSelected()
        {
            OnCardClick();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 卡片点击事件处理
        /// 绑定到 Button 组件的 OnClick 事件
        /// 点击选中卡片（高亮变灰），不直接跳转场景
        /// </summary>
        public void OnCardClick()
        {
            // 1. 验证 Profile 是否配置
            if (crystalProfile == null)
            {
                Debug.LogWarning($"[CrystalCardSelector] {gameObject.name}: crystalProfile 未配置，无法选择晶体");
                return;
            }

            // 2. 取消其他卡片选中
            DeselectAll();

            // 3. 选中当前卡片（视觉高亮）
            _currentlySelected = this;
            if (_cardImage != null)
                _cardImage.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            string profileName = !string.IsNullOrEmpty(crystalProfile.crystalName)
                ? crystalProfile.crystalName
                : displayName;
            Debug.Log($"[CrystalCardSelector] 已选中晶体: {profileName}，请点击「选择」按钮确认");
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 加载目标场景
        /// 优先使用 CrystalSelectionData.TargetSceneName（由 ExperimentNavigator 设定），
        /// 未设置时 fallback 到 Inspector 配置的 targetSceneName
        /// 当 Scene2 还活着时使用 additive 模式加载以保持 Scene2 状态
        /// </summary>
        private void LoadTargetScene()
        {
            string destination = ElectroOptics.DataTransfer.CrystalSelectionData.HasTargetScene
                ? ElectroOptics.DataTransfer.CrystalSelectionData.TargetSceneName
                : targetSceneName;

            if (string.IsNullOrEmpty(destination))
            {
                Debug.LogError("[CrystalCardSelector] 目标场景未配置，请在 Inspector 设置 targetSceneName 或通过 ExperimentNavigator 预设");
                return;
            }

            Debug.Log($"[CrystalCardSelector] 正在加载场景: {destination}");

            try
            {
                // Keep Scene2 alive if it's loaded (additive experiment flow)
                Scene labScene = SceneManager.GetSceneByName(Scene2AdditionalSceneNavigator.LabSceneName);
                if (labScene.isLoaded)
                {
                    Scene2AdditionalSceneNavigator.GoToExperimentFromPreviewAdditive(destination);
                }
                else
                {
                    SceneManager.LoadScene(destination);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CrystalCardSelector] 场景加载失败: {e.Message}");
            }
        }

        #endregion

        #region 选中管理（静态方法，供 Btn选择 调用）

        /// <summary>
        /// 取消当前卡片的选中高亮
        /// </summary>
        public void Deselect()
        {
            if (_cardImage != null)
                _cardImage.color = _originalColor;
        }

        /// <summary>
        /// 取消所有卡片的选中状态
        /// </summary>
        public static void DeselectAll()
        {
            if (_currentlySelected != null)
            {
                _currentlySelected.Deselect();
                _currentlySelected = null;
            }
            CustomCrystalCard.DeselectCurrent();
        }

        /// <summary>
        /// 确认当前选中（由 Btn选择 按钮调用）。
        /// 优先处理普通晶体卡片，其次处理自定义晶体卡片。
        /// </summary>
        public static void ConfirmSelection()
        {
            if (_currentlySelected != null)
            {
                Debug.Log($"[CrystalCardSelector] 确认选择: {_currentlySelected.displayName}");
                _currentlySelected.ExecuteSelection();
                return;
            }
            CustomCrystalCard.ConfirmCurrentSelection();
        }

        /// <summary>
        /// 执行选中后的操作：保存 Profile 并加载场景
        /// </summary>
        private void ExecuteSelection()
        {
            if (crystalProfile == null)
            {
                Debug.LogWarning($"[CrystalCardSelector] crystalProfile 为空，无法执行选择");
                return;
            }

            CrystalSelectionData.SelectedProfile = crystalProfile;

            string profileName = !string.IsNullOrEmpty(crystalProfile.crystalName)
                ? crystalProfile.crystalName
                : displayName;
            Debug.Log($"[CrystalCardSelector] 执行选择: {profileName}");

            LoadTargetScene();
        }

        #endregion

        #region 编辑器验证

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (crystalProfile == null)
            {
                Debug.LogWarning($"[CrystalCardSelector] {gameObject.name}: crystalProfile 未设置，请在 Inspector 中配置");
            }

            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogWarning($"[CrystalCardSelector] {gameObject.name}: targetSceneName 未设置");
            }
        }
#endif

        #endregion
    }
}
