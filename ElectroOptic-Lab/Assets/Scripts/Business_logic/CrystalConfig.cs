using System;
using UnityEngine;

namespace ElectroOptics
{
    /// <summary>
    /// 纯物理配置包：描述晶体在特定时刻的物理状态
    /// </summary>
    [Serializable]
    public struct CrystalConfig
    {
        // --- 静态资产 ---
        public CrystalProfile profile;

        // --- 物理状态 ---
        // 1. 晶体姿态 (World -> Crystal Geometry)
        //    UI 上的欧拉角会转为这个四元数
        public Quaternion crystalRotation;

        // 2. 本地电场矢量 (V/m)
        //    包含了方向和大小 (E = V/d * direction)
        //    这是实际用于渲染的场
        public Vector3 localEField;

        // 3. 探测电场方向 (单位向量)
        //    用于计算灵敏度 S_eff (V_pi 计算用)
        public Vector3 probeFieldDirection;

        // 4. 世界光路方向 (单位向量)
        //    通常是 (0,0,1)，但允许配置
        public Vector3 worldLightDirection;

        // --- 脏检查逻辑 ---
        public bool IsGeometryDifferent(CrystalConfig other)
        {
            // 如果旋转、光路或 Profile 变了，需要重算几何关系 (Probe Pass)
            return profile != other.profile ||
                   crystalRotation != other.crystalRotation ||
                   worldLightDirection != other.worldLightDirection ||
                   probeFieldDirection != other.probeFieldDirection;
        }
    }
}