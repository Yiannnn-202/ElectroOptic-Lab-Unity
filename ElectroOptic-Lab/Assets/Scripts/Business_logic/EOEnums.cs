namespace ElectroOptics
{
    /// <summary>
    /// 光传播方向 (通光轴)
    /// </summary>
    public enum PropagationAxis
    {
        X_Axis = 0,
        Y_Axis = 1,
        Z_Axis = 2
    }

    /// <summary>
    /// 电场施加方向 (晶体切型/电极方向)
    /// </summary>
    public enum ElectricFieldAxis
    {
        X_Axis = 0,
        Y_Axis = 1,
        Z_Axis = 2
    }

    /// <summary>
    /// 调制模式
    /// </summary>
    public enum ModulationMode
    {
        /// <summary>
        /// 横向调制 (Transverse): 电场垂直于光路 (E = V/d)
        /// </summary>
        Transverse = 0,

        /// <summary>
        /// 纵向调制 (Longitudinal): 电场平行于光路 (E = V/L)
        /// </summary>
        Longitudinal = 1
    }
}