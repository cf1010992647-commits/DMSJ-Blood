using System.Collections.Generic;

namespace Blood_Alcohol.Models
{
    /// <summary>
    /// 流程参数配置模型。
    /// </summary>
    public class ProcessParameterConfig
    {
        /// <summary>
        /// 加热箱温度设定值。
        /// </summary>
        public double HeatingBoxTemperature { get; set; } = 60.0;

        /// <summary>
        /// 加热箱温控站号。
        /// </summary>
        public string HeatingBoxTemperatureStation { get; set; } = "01";

        /// <summary>
        /// 定量环温度设定值。
        /// </summary>
        public double QuantitativeLoopTemperature { get; set; } = 80.0;

        /// <summary>
        /// 定量环温控站号。
        /// </summary>
        public string QuantitativeLoopTemperatureStation { get; set; } = "02";

        /// <summary>
        /// 传输线温度设定值。
        /// </summary>
        public double TransferLineTemperature { get; set; } = 120.0;

        /// <summary>
        /// 传输线温控站号。
        /// </summary>
        public string TransferLineTemperatureStation { get; set; } = "03";

        /// <summary>
        /// 预留温控温度设定值。
        /// </summary>
        public double ReservedTemperature { get; set; } = 60.0;

        /// <summary>
        /// 预留温控站号。
        /// </summary>
        public string ReservedTemperatureStation { get; set; } = "04";

        /// <summary>
        /// 摇匀持续时长（秒）。
        /// </summary>
        public int ShakeDurationSeconds { get; set; } = 10;

        /// <summary>
        /// Z轴丢枪头上升慢速速度参数。
        /// </summary>
        public int ZDropNeedleRiseSlowSpeed { get; set; } = 0;

        /// <summary>
        /// 移液枪吸液延时时间（100ms）。
        /// </summary>
        public int PipetteAspirateDelay100ms { get; set; } = 0;

        /// <summary>
        /// 移液枪打液延时时间（100ms）。
        /// </summary>
        public int PipetteDispenseDelay100ms { get; set; } = 0;

        /// <summary>
        /// 采血管摇晃原位延时时间（100ms）。
        /// </summary>
        public int TubeShakeHomeDelay100ms { get; set; } = 0;

        /// <summary>
        /// 采血管摇晃工位延时时间（100ms）。
        /// </summary>
        public int TubeShakeWorkDelay100ms { get; set; } = 0;

        /// <summary>
        /// 采血管摇晃目标次数。
        /// </summary>
        public int TubeShakeTargetCount { get; set; } = 0;

        /// <summary>
        /// 顶空瓶摇晃原位延时时间（100ms）。
        /// </summary>
        public int HeadspaceShakeHomeDelay100ms { get; set; } = 0;

        /// <summary>
        /// 顶空瓶摇晃工位延时时间（100ms）。
        /// </summary>
        public int HeadspaceShakeWorkDelay100ms { get; set; } = 0;

        /// <summary>
        /// 顶空瓶摇晃目标次数。
        /// </summary>
        public int HeadspaceShakeTargetCount { get; set; } = 0;

        /// <summary>
        /// 叔丁醇吸液延时时间（100ms）。
        /// </summary>
        public int ButanolAspirateDelay100ms { get; set; } = 0;

        /// <summary>
        /// 叔丁醇打液延时时间（100ms）。
        /// </summary>
        public int ButanolDispenseDelay100ms { get; set; } = 0;

        /// <summary>
        /// 样品瓶加压时间（100ms）。
        /// </summary>
        public int SampleBottlePressureTime100ms { get; set; } = 0;

        /// <summary>
        /// 定量环平衡时间（100ms）。
        /// </summary>
        public int QuantitativeLoopBalanceTime100ms { get; set; } = 0;

        /// <summary>
        /// 进样时间（100ms）。
        /// </summary>
        public int InjectionTime100ms { get; set; } = 0;

        /// <summary>
        /// 样品瓶加压位置。
        /// </summary>
        public int SampleBottlePressurePosition { get; set; } = 0;

        /// <summary>
        /// 定量环平衡位置。
        /// </summary>
        public int QuantitativeLoopBalancePosition { get; set; } = 0;

        /// <summary>
        /// 进样位置。
        /// </summary>
        public int InjectionPosition { get; set; } = 0;

        /// <summary>
        /// 校验工艺参数配置是否合法。
        /// </summary>
        /// <returns>返回配置错误列表，列表为空表示校验通过。</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            ValidateDoubleRange(errors, nameof(HeatingBoxTemperature), "加热箱温度", HeatingBoxTemperature, 0, 300);
            ValidateDoubleRange(errors, nameof(QuantitativeLoopTemperature), "定量环温度", QuantitativeLoopTemperature, 0, 300);
            ValidateDoubleRange(errors, nameof(TransferLineTemperature), "传输线温度", TransferLineTemperature, 0, 300);
            ValidateDoubleRange(errors, nameof(ReservedTemperature), "预留温控温度", ReservedTemperature, 0, 300);
            ValidateStation(errors, nameof(HeatingBoxTemperatureStation), "加热箱温控站号", HeatingBoxTemperatureStation);
            ValidateStation(errors, nameof(QuantitativeLoopTemperatureStation), "定量环温控站号", QuantitativeLoopTemperatureStation);
            ValidateStation(errors, nameof(TransferLineTemperatureStation), "传输线温控站号", TransferLineTemperatureStation);
            ValidateStation(errors, nameof(ReservedTemperatureStation), "预留温控站号", ReservedTemperatureStation);
            ValidateDistinctStations(
                errors,
                ("加热箱温控站号", HeatingBoxTemperatureStation),
                ("定量环温控站号", QuantitativeLoopTemperatureStation),
                ("传输线温控站号", TransferLineTemperatureStation),
                ("预留温控站号", ReservedTemperatureStation));

            ValidateIntRange(errors, nameof(ShakeDurationSeconds), "摇匀持续时长", ShakeDurationSeconds, 0, 3600);
            ValidateIntRange(errors, nameof(ZDropNeedleRiseSlowSpeed), "Z轴丢枪头上升慢速速度", ZDropNeedleRiseSlowSpeed, 0, 1_000_000);

            ValidateDelay100ms(errors, nameof(PipetteAspirateDelay100ms), "移液枪吸液延时", PipetteAspirateDelay100ms);
            ValidateDelay100ms(errors, nameof(PipetteDispenseDelay100ms), "移液枪打液延时", PipetteDispenseDelay100ms);
            ValidateDelay100ms(errors, nameof(TubeShakeHomeDelay100ms), "采血管摇晃原位延时", TubeShakeHomeDelay100ms);
            ValidateDelay100ms(errors, nameof(TubeShakeWorkDelay100ms), "采血管摇晃工位延时", TubeShakeWorkDelay100ms);
            ValidateDelay100ms(errors, nameof(HeadspaceShakeHomeDelay100ms), "顶空瓶摇晃原位延时", HeadspaceShakeHomeDelay100ms);
            ValidateDelay100ms(errors, nameof(HeadspaceShakeWorkDelay100ms), "顶空瓶摇晃工位延时", HeadspaceShakeWorkDelay100ms);
            ValidateDelay100ms(errors, nameof(ButanolAspirateDelay100ms), "叔丁醇吸液延时", ButanolAspirateDelay100ms);
            ValidateDelay100ms(errors, nameof(ButanolDispenseDelay100ms), "叔丁醇打液延时", ButanolDispenseDelay100ms);
            ValidateDelay100ms(errors, nameof(SampleBottlePressureTime100ms), "样品瓶加压时间", SampleBottlePressureTime100ms);
            ValidateDelay100ms(errors, nameof(QuantitativeLoopBalanceTime100ms), "定量环平衡时间", QuantitativeLoopBalanceTime100ms);
            ValidateDelay100ms(errors, nameof(InjectionTime100ms), "进样时间", InjectionTime100ms);

            ValidateCount(errors, nameof(TubeShakeTargetCount), "采血管摇晃目标次数", TubeShakeTargetCount);
            ValidateCount(errors, nameof(HeadspaceShakeTargetCount), "顶空瓶摇晃目标次数", HeadspaceShakeTargetCount);

            ValidatePosition(errors, nameof(SampleBottlePressurePosition), "样品瓶加压位置", SampleBottlePressurePosition);
            ValidatePosition(errors, nameof(QuantitativeLoopBalancePosition), "定量环平衡位置", QuantitativeLoopBalancePosition);
            ValidatePosition(errors, nameof(InjectionPosition), "进样位置", InjectionPosition);

            return errors;
        }

        /// <summary>
        /// 校验 100ms 单位的延时参数。
        /// </summary>
        /// <param name="errors">用于收集错误信息的列表。</param>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="displayName">显示名称。</param>
        /// <param name="value">参数值。</param>
        private static void ValidateDelay100ms(List<string> errors, string propertyName, string displayName, int value)
        {
            ValidateIntRange(errors, propertyName, displayName, value, 0, 36_000);
        }

        /// <summary>
        /// 校验次数参数。
        /// </summary>
        /// <param name="errors">用于收集错误信息的列表。</param>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="displayName">显示名称。</param>
        /// <param name="value">参数值。</param>
        private static void ValidateCount(List<string> errors, string propertyName, string displayName, int value)
        {
            ValidateIntRange(errors, propertyName, displayName, value, 0, 1_000_000);
        }

        /// <summary>
        /// 校验位置参数。
        /// </summary>
        /// <param name="errors">用于收集错误信息的列表。</param>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="displayName">显示名称。</param>
        /// <param name="value">参数值。</param>
        private static void ValidatePosition(List<string> errors, string propertyName, string displayName, int value)
        {
            ValidateIntRange(errors, propertyName, displayName, value, 0, 10_000_000);
        }

        /// <summary>
        /// 校验整数范围。
        /// </summary>
        /// <param name="errors">用于收集错误信息的列表。</param>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="displayName">显示名称。</param>
        /// <param name="value">参数值。</param>
        /// <param name="min">最小允许值。</param>
        /// <param name="max">最大允许值。</param>
        private static void ValidateIntRange(
            List<string> errors,
            string propertyName,
            string displayName,
            int value,
            int min,
            int max)
        {
            if (value < min || value > max)
            {
                errors.Add($"{displayName}（{propertyName}）必须在 {min}-{max} 范围内，当前值：{value}。");
            }
        }

        /// <summary>
        /// 校验浮点数范围。
        /// </summary>
        /// <param name="errors">用于收集错误信息的列表。</param>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="displayName">显示名称。</param>
        /// <param name="value">参数值。</param>
        /// <param name="min">最小允许值。</param>
        /// <param name="max">最大允许值。</param>
        private static void ValidateDoubleRange(
            List<string> errors,
            string propertyName,
            string displayName,
            double value,
            double min,
            double max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
            {
                errors.Add($"{displayName}（{propertyName}）必须在 {min}-{max} 范围内，当前值：{value}。");
            }
        }

        /// <summary>
        /// 校验温控站号文本。
        /// </summary>
        /// <param name="errors">用于收集错误信息的列表。</param>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="displayName">显示名称。</param>
        /// <param name="station">站号文本。</param>
        private static void ValidateStation(List<string> errors, string propertyName, string displayName, string? station)
        {
            if (string.IsNullOrWhiteSpace(station))
            {
                errors.Add($"{displayName}（{propertyName}）不能为空。");
                return;
            }

            string normalized = station.Trim();
            if (!int.TryParse(normalized, out int stationValue) || stationValue < 0 || stationValue > 99)
            {
                errors.Add($"{displayName}（{propertyName}）必须是 0-99 之间的数字，当前值：{station}。");
            }
        }

        /// <summary>
        /// 校验多路温控站号不能重复。
        /// </summary>
        /// <param name="errors">用于收集错误信息的列表。</param>
        /// <param name="items">需要参与重复校验的显示名称和站号集合。</param>
        private static void ValidateDistinctStations(
            List<string> errors,
            params (string DisplayName, string? Station)[] items)
        {
            Dictionary<string, string> seenStations = new Dictionary<string, string>();
            foreach ((string DisplayName, string? Station) item in items)
            {
                if (!TryNormalizeStation(item.Station, out string normalizedStation))
                {
                    continue;
                }

                if (seenStations.TryGetValue(normalizedStation, out string? existingDisplayName))
                {
                    errors.Add($"{item.DisplayName}与{existingDisplayName}不能使用相同站号：{normalizedStation}。");
                    continue;
                }

                seenStations[normalizedStation] = item.DisplayName;
            }
        }

        /// <summary>
        /// 尝试把站号文本规范化为两位数字文本。
        /// </summary>
        /// <param name="station">原始站号文本。</param>
        /// <param name="normalizedStation">规范化后的两位站号文本。</param>
        /// <returns>返回站号是否可用于重复校验。</returns>
        private static bool TryNormalizeStation(string? station, out string normalizedStation)
        {
            normalizedStation = string.Empty;
            if (string.IsNullOrWhiteSpace(station))
            {
                return false;
            }

            string trimmedStation = station.Trim();
            if (!int.TryParse(trimmedStation, out int stationValue) || stationValue < 0 || stationValue > 99)
            {
                return false;
            }

            normalizedStation = stationValue.ToString("D2");
            return true;
        }
    }
}
