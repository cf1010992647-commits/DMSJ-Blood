namespace Blood_Alcohol.ViewModels
{
    /// <summary>
    /// 监控页面生命周期接口。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由监控型视图在可见性变化时调用，用于区分临时停用和最终释放。
    /// </remarks>
    public interface IMonitoringLifecycle
    {
        /// <summary>
        /// 激活页面监控任务。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由视图 Loaded 时调用，要求实现方保证重复调用不会创建多个监控循环。
        /// </remarks>
        void ActivateMonitoring();

        /// <summary>
        /// 停用页面监控任务。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由视图 Unloaded 时调用，仅停止监控，不释放页面配置和状态对象。
        /// </remarks>
        void DeactivateMonitoring();
    }
}
