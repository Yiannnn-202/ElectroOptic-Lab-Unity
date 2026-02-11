namespace ElectroOptics
{
    // 保持不变，用于 UI 下拉菜单
    public enum PropagationAxis
    {
        X_Axis = 0,
        Y_Axis = 1,
        Z_Axis = 2,
        Custom = 99 // 预留给未来可能的自定义模式
    }

    // 保持不变，用于 UI 下拉菜单
    public enum ElectricFieldAxis
    {
        X_Axis = 0,
        Y_Axis = 1,
        Z_Axis = 2
    }

    // 保持不变，用于决定 E = V/d 还是 V/L
    public enum ModulationMode
    {
        Transverse = 0,   // 横向调制 (V/d)
        Longitudinal = 1  // 纵向调制 (V/L)
    }
}