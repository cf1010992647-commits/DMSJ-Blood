using System.Collections.Generic;

namespace Blood_Alcohol.Models
{
    /// <summary>
    /// 轴调试地址映射配置。
    /// </summary>
    public class AxisDebugAddressConfig
    {
        /// <summary>
        /// M1 轴地址映射。
        /// </summary>
        public AxisAddressProfile Axis1 { get; set; } = new AxisAddressProfile
        {
            AxisName = "M1 X轴伸缩",
            JogPlusCoil = 1000,
            JogMinusCoil = 1001,
            GoHomeCoil = 1002,
            HomeDoneCoil = 1003,
            PositiveLimitCoil = 1012,
            NegativeLimitCoil = 1013,
            HomeSensorCoil = 1014,
            ManualLocateTriggerCoil = 1019,
            CurrentPositionLowRegister = 1002,
            CurrentPositionHighRegister = 1003,
            ManualSpeedRegister = 1004,
            AutoSpeedRegister = 1008,
            ManualTargetLowRegister = 1016,
            ManualTargetHighRegister = 1017
        };

        /// <summary>
        /// M2 轴地址映射。
        /// </summary>
        public AxisAddressProfile Axis2 { get; set; } = new AxisAddressProfile
        {
            AxisName = "M2 Y轴伸缩",
            JogPlusCoil = 1100,
            JogMinusCoil = 1101,
            GoHomeCoil = 1102,
            HomeDoneCoil = 1103,
            PositiveLimitCoil = 1112,
            NegativeLimitCoil = 1113,
            HomeSensorCoil = 1114,
            ManualLocateTriggerCoil = 1119,
            CurrentPositionLowRegister = 1102,
            CurrentPositionHighRegister = 1103,
            ManualSpeedRegister = 1104,
            AutoSpeedRegister = 1108,
            ManualTargetLowRegister = 1116,
            ManualTargetHighRegister = 1117
        };

        /// <summary>
        /// M3 轴地址映射。
        /// </summary>
        public AxisAddressProfile Axis3 { get; set; } = new AxisAddressProfile
        {
            AxisName = "M3 Z轴伸缩",
            JogPlusCoil = 1200,
            JogMinusCoil = 1201,
            GoHomeCoil = 1202,
            HomeDoneCoil = 1203,
            PositiveLimitCoil = 1212,
            NegativeLimitCoil = 1213,
            HomeSensorCoil = 1214,
            ManualLocateTriggerCoil = 1219,
            CurrentPositionLowRegister = 1202,
            CurrentPositionHighRegister = 1203,
            ManualSpeedRegister = 1204,
            AutoSpeedRegister = 1208,
            ManualTargetLowRegister = 1216,
            ManualTargetHighRegister = 1217
        };

        /// <summary>
        /// M4 轴地址映射。
        /// </summary>
        public AxisAddressProfile Axis4 { get; set; } = new AxisAddressProfile
        {
            AxisName = "M4 摇匀轴",
            JogPlusCoil = 1300,
            JogMinusCoil = 1301,
            GoHomeCoil = 1302,
            HomeDoneCoil = 1303,
            PositiveLimitCoil = 1312,
            NegativeLimitCoil = 1313,
            HomeSensorCoil = 1314,
            ManualLocateTriggerCoil = 1319,
            CurrentPositionLowRegister = 1302,
            CurrentPositionHighRegister = 1303,
            ManualSpeedRegister = 1304,
            AutoSpeedRegister = 1308,
            ManualTargetLowRegister = 1316,
            ManualTargetHighRegister = 1317
        };

        /// <summary>
        /// 校验轴调试地址配置是否合法。
        /// </summary>
        /// <returns>返回配置错误列表，列表为空表示校验通过。</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var usedCoils = new Dictionary<ushort, string>();
            var usedRegisters = new Dictionary<ushort, string>();

            ValidateAxis(errors, usedCoils, usedRegisters, "Axis1", Axis1);
            ValidateAxis(errors, usedCoils, usedRegisters, "Axis2", Axis2);
            ValidateAxis(errors, usedCoils, usedRegisters, "Axis3", Axis3);
            ValidateAxis(errors, usedCoils, usedRegisters, "Axis4", Axis4);

            return errors;
        }

        /// <summary>
        /// 校验单个轴地址映射。
        /// </summary>
        /// <param name="errors">用于收集错误信息的列表。</param>
        /// <param name="usedCoils">已使用的 M 位地址。</param>
        /// <param name="usedRegisters">已使用的 D 寄存器地址。</param>
        /// <param name="axisKey">轴配置键名。</param>
        /// <param name="profile">轴地址配置。</param>
        private static void ValidateAxis(
            List<string> errors,
            Dictionary<ushort, string> usedCoils,
            Dictionary<ushort, string> usedRegisters,
            string axisKey,
            AxisAddressProfile? profile)
        {
            if (profile == null)
            {
                errors.Add($"{axisKey} 轴地址配置不能为空。");
                return;
            }

            string axisName = string.IsNullOrWhiteSpace(profile.AxisName) ? axisKey : profile.AxisName;
            if (string.IsNullOrWhiteSpace(profile.AxisName))
            {
                errors.Add($"{axisKey} 的轴名称不能为空。");
            }

            AddDuplicateAddressErrors(
                errors,
                usedCoils,
                new (string Name, ushort Address)[]
                {
                    ($"{axisName}-点动正向", profile.JogPlusCoil),
                    ($"{axisName}-点动反向", profile.JogMinusCoil),
                    ($"{axisName}-回原点", profile.GoHomeCoil),
                    ($"{axisName}-回原点完成", profile.HomeDoneCoil),
                    ($"{axisName}-正限位", profile.PositiveLimitCoil),
                    ($"{axisName}-负限位", profile.NegativeLimitCoil),
                    ($"{axisName}-原点信号", profile.HomeSensorCoil),
                    ($"{axisName}-手动定位触发", profile.ManualLocateTriggerCoil)
                },
                "轴M位地址");

            AddDuplicateAddressErrors(
                errors,
                usedRegisters,
                new (string Name, ushort Address)[]
                {
                    ($"{axisName}-当前位置低16位", profile.CurrentPositionLowRegister),
                    ($"{axisName}-当前位置高16位", profile.CurrentPositionHighRegister),
                    ($"{axisName}-手动速度", profile.ManualSpeedRegister),
                    ($"{axisName}-自动速度", profile.AutoSpeedRegister),
                    ($"{axisName}-手动目标低16位", profile.ManualTargetLowRegister),
                    ($"{axisName}-手动目标高16位", profile.ManualTargetHighRegister)
                },
                "轴D寄存器地址");

            ValidateRegisterPair(errors, axisName, "当前位置", profile.CurrentPositionLowRegister, profile.CurrentPositionHighRegister);
            ValidateRegisterPair(errors, axisName, "手动目标", profile.ManualTargetLowRegister, profile.ManualTargetHighRegister);
        }

        /// <summary>
        /// 校验高低字寄存器是否相邻且不越界。
        /// </summary>
        /// <param name="errors">用于收集错误信息的列表。</param>
        /// <param name="axisName">轴名称。</param>
        /// <param name="pairName">寄存器对名称。</param>
        /// <param name="lowRegister">低16位地址。</param>
        /// <param name="highRegister">高16位地址。</param>
        private static void ValidateRegisterPair(
            List<string> errors,
            string axisName,
            string pairName,
            ushort lowRegister,
            ushort highRegister)
        {
            if (lowRegister == ushort.MaxValue)
            {
                errors.Add($"{axisName}-{pairName}低16位地址不能为 65535。");
                return;
            }

            if (highRegister != lowRegister + 1)
            {
                errors.Add($"{axisName}-{pairName}高16位地址应等于低16位地址+1。");
            }
        }

        /// <summary>
        /// 收集重复地址错误。
        /// </summary>
        /// <param name="errors">用于收集错误信息的列表。</param>
        /// <param name="usedAddresses">已使用地址字典。</param>
        /// <param name="addresses">待校验地址集合。</param>
        /// <param name="addressType">地址类型说明。</param>
        private static void AddDuplicateAddressErrors(
            List<string> errors,
            Dictionary<ushort, string> usedAddresses,
            IEnumerable<(string Name, ushort Address)> addresses,
            string addressType)
        {
            foreach ((string name, ushort address) in addresses)
            {
                if (usedAddresses.TryGetValue(address, out string? existingName))
                {
                    errors.Add($"{addressType}重复：{address}（{existingName}、{name}）。");
                }
                else
                {
                    usedAddresses.Add(address, name);
                }
            }
        }
    }

    /// <summary>
    /// 单轴地址映射模型。
    /// </summary>
    public class AxisAddressProfile
    {
        /// <summary>
        /// 轴名称。
        /// </summary>
        public string AxisName { get; set; } = string.Empty;

        /// <summary>
        /// 点动正向线圈地址。
        /// </summary>
        public ushort JogPlusCoil { get; set; }

        /// <summary>
        /// 点动反向线圈地址。
        /// </summary>
        public ushort JogMinusCoil { get; set; }

        /// <summary>
        /// 回原点线圈地址。
        /// </summary>
        public ushort GoHomeCoil { get; set; }

        /// <summary>
        /// 回原点完成信号地址。
        /// </summary>
        public ushort HomeDoneCoil { get; set; }

        /// <summary>
        /// 正限位信号地址。
        /// </summary>
        public ushort PositiveLimitCoil { get; set; }

        /// <summary>
        /// 负限位信号地址。
        /// </summary>
        public ushort NegativeLimitCoil { get; set; }

        /// <summary>
        /// 原点信号地址。
        /// </summary>
        public ushort HomeSensorCoil { get; set; }

        /// <summary>
        /// 手动定位触发线圈地址。
        /// </summary>
        public ushort ManualLocateTriggerCoil { get; set; }

        /// <summary>
        /// 当前位置低16位地址。
        /// </summary>
        public ushort CurrentPositionLowRegister { get; set; }

        /// <summary>
        /// 当前位置高16位地址。
        /// </summary>
        public ushort CurrentPositionHighRegister { get; set; }

        /// <summary>
        /// 手动速度寄存器地址。
        /// </summary>
        public ushort ManualSpeedRegister { get; set; }

        /// <summary>
        /// 自动速度寄存器地址。
        /// </summary>
        public ushort AutoSpeedRegister { get; set; }

        /// <summary>
        /// 手动定位目标低16位地址。
        /// </summary>
        public ushort ManualTargetLowRegister { get; set; }

        /// <summary>
        /// 手动定位目标高16位地址。
        /// </summary>
        public ushort ManualTargetHighRegister { get; set; }
    }
}
