using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.Experiment.Interfaces
{
    /// <summary>
    /// 可配置的晶体控制器接口
    /// 定义晶体运行时的配置和旋转控制方法
    /// </summary>
    public interface ICrystalConfigurable
    {
        /// <summary>
        /// 设置晶体 Profile
        /// </summary>
        /// <param name="profile">晶体配置资源</param>
        void SetProfile(CrystalProfile profile);

        /// <summary>
        /// 获取当前 Profile
        /// </summary>
        /// <returns>当前晶体配置资源</returns>
        CrystalProfile GetProfile();

        /// <summary>
        /// 设置旋转角度
        /// </summary>
        /// <param name="rotation">X轴(俯仰)和Y轴(偏航)的旋转角度（度）</param>
        void SetRotation(Vector2 rotation);

        /// <summary>
        /// 获取当前旋转角度
        /// </summary>
        /// <returns>X和Y轴的旋转角度</returns>
        Vector2 GetRotation();

        /// <summary>
        /// 增量旋转
        /// </summary>
        /// <param name="delta">X和Y轴的旋转增量（度）</param>
        void AddRotation(Vector2 delta);

        /// <summary>
        /// 重置旋转到零
        /// </summary>
        void ResetRotation();

        /// <summary>
        /// 检查是否已初始化
        /// </summary>
        /// <returns>初始化状态</returns>
        bool IsInitialized();
    }
}
