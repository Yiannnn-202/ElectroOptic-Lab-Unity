using ElectroOptics;

namespace ElectroOptics.DataTransfer
{
    /// <summary>
    /// 晶体选择数据静态类
    /// 用于在场景间传递用户选择的晶体 Profile
    /// </summary>
    public static class CrystalSelectionData
    {
        /// <summary>
        /// 当前选中的晶体 Profile
        /// </summary>
        public static CrystalProfile SelectedProfile { get; set; }

        /// <summary>
        /// 是否有选中的晶体
        /// </summary>
        public static bool HasSelection => SelectedProfile != null;

        /// <summary>
        /// 清除选择数据
        /// 可在加载完成后调用，避免数据残留
        /// </summary>
        public static void Clear()
        {
            SelectedProfile = null;
        }
    }
}
