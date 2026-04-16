using Blood_Alcohol.Models;
using System;

namespace Blood_Alcohol.Services
{
    /// <summary>
    /// 流程运行时配置快照。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 WorkflowEngine 在每次 Start 时创建，用于保证同一检测批次内配置不漂移。
    /// </remarks>
    public sealed class WorkflowRuntimeSnapshot
    {
        /// <summary>
        /// 初始化流程运行时配置快照。
        /// </summary>
        /// By:ChengLei
        /// <param name="signals">流程信号配置。</param>
        /// <param name="parameters">工艺参数配置。</param>
        /// <param name="weightToZ">重量到 Z 坐标标定配置。</param>
        /// <param name="loadedAt">配置加载时间。</param>
        /// <remarks>
        /// 由 WorkflowEngine.Start 调用，空配置会回退为默认配置对象。
        /// </remarks>
        public WorkflowRuntimeSnapshot(
            WorkflowSignalConfig signals,
            ProcessParameterConfig parameters,
            WeightToZCalibrationConfig weightToZ,
            DateTime loadedAt)
        {
            Signals = signals ?? new WorkflowSignalConfig();
            Parameters = parameters ?? new ProcessParameterConfig();
            WeightToZ = weightToZ ?? new WeightToZCalibrationConfig();
            LoadedAt = loadedAt;
        }

        /// <summary>
        /// 流程信号配置。
        /// </summary>
        /// By:ChengLei
        public WorkflowSignalConfig Signals { get; }

        /// <summary>
        /// 工艺参数配置。
        /// </summary>
        /// By:ChengLei
        public ProcessParameterConfig Parameters { get; }

        /// <summary>
        /// 重量到 Z 坐标标定配置。
        /// </summary>
        /// By:ChengLei
        public WeightToZCalibrationConfig WeightToZ { get; }

        /// <summary>
        /// 配置加载时间。
        /// </summary>
        /// By:ChengLei
        public DateTime LoadedAt { get; }
    }
}
