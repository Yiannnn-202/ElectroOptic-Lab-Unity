using ElectroOptics;

namespace ElectroOptics.Experiment.Interfaces
{
    /// <summary>
    /// 可被选择进入实验的晶体卡片接口
    /// 实现此接口的组件可以响应选择操作并加载到场景中
    /// </summary>
    public interface ICrystalSelectable
    {
        /// <summary>
        /// 获取绑定的晶体 Profile
        /// </summary>
        /// <returns>晶体配置资源</returns>
        CrystalProfile GetCrystalProfile();

        /// <summary>
        /// 处理选择事件
        /// 当卡片被选中时调用
        /// </summary>
        void OnSelected();

        /// <summary>
        /// 获取卡片显示名称
        /// </summary>
        /// <returns>显示名称字符串</returns>
        string GetDisplayName();

        /// <summary>
        /// 获取卡片描述
        /// </summary>
        /// <returns>描述文本</returns>
        string GetDescription();
    }
}
