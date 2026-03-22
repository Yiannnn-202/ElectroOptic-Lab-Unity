namespace ElectroOptics.UI.ScreenDisplay
{
    /// <summary>
    /// 光屏显示模式
    /// </summary>
    public enum ScreenMode
    {
        /// <summary>
        /// 红点追踪模式（晶体未上导轨）
        /// </summary>
        Direct = 0,

        /// <summary>
        /// 锥光干涉模式（晶体已上导轨）
        /// </summary>
        Conoscopic = 1
    }
}
