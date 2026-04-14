namespace Blood_Alcohol.Models
{
    /// <summary>
    /// 轴调试地址映射配置
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 AxisDebugViewModel 加载并用于构建四轴控制卡参数
    /// </remarks>
    public class AxisDebugAddressConfig
    {
        /// <summary>
        /// M1 轴地址映射
        /// </summary>
        /// By:ChengLei
        public AxisAddressProfile Axis1 { get; set; } = new AxisAddressProfile
        {
            AxisName = "M1 X轴伺服",
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
        /// M2 轴地址映射
        /// </summary>
        /// By:ChengLei
        public AxisAddressProfile Axis2 { get; set; } = new AxisAddressProfile
        {
            AxisName = "M2 Y轴伺服",
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
        /// M3 轴地址映射
        /// </summary>
        /// By:ChengLei
        public AxisAddressProfile Axis3 { get; set; } = new AxisAddressProfile
        {
            AxisName = "M3 Z轴伺服",
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
        /// M4 轴地址映射
        /// </summary>
        /// By:ChengLei
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
    }

    /// <summary>
    /// 单轴地址映射模型
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 描述 AxisDebugViewModel 单轴卡片所需的线圈与寄存器地址
    /// </remarks>
    public class AxisAddressProfile
    {
        /// <summary>
        /// 轴名称
        /// </summary>
        /// By:ChengLei
        public string AxisName { get; set; } = string.Empty;

        /// <summary>
        /// 点动正向线圈地址
        /// </summary>
        /// By:ChengLei
        public ushort JogPlusCoil { get; set; }

        /// <summary>
        /// 点动反向线圈地址
        /// </summary>
        /// By:ChengLei
        public ushort JogMinusCoil { get; set; }

        /// <summary>
        /// 回原点线圈地址
        /// </summary>
        /// By:ChengLei
        public ushort GoHomeCoil { get; set; }

        /// <summary>
        /// 回原点完成信号地址
        /// </summary>
        /// By:ChengLei
        public ushort HomeDoneCoil { get; set; }

        /// <summary>
        /// 正限位信号地址
        /// </summary>
        /// By:ChengLei
        public ushort PositiveLimitCoil { get; set; }

        /// <summary>
        /// 负限位信号地址
        /// </summary>
        /// By:ChengLei
        public ushort NegativeLimitCoil { get; set; }

        /// <summary>
        /// 原点信号地址
        /// </summary>
        /// By:ChengLei
        public ushort HomeSensorCoil { get; set; }

        /// <summary>
        /// 手动定位触发线圈地址
        /// </summary>
        /// By:ChengLei
        public ushort ManualLocateTriggerCoil { get; set; }

        /// <summary>
        /// 当前位置低 16 位地址
        /// </summary>
        /// By:ChengLei
        public ushort CurrentPositionLowRegister { get; set; }

        /// <summary>
        /// 当前位置高 16 位地址
        /// </summary>
        /// By:ChengLei
        public ushort CurrentPositionHighRegister { get; set; }

        /// <summary>
        /// 手动速度寄存器地址
        /// </summary>
        /// By:ChengLei
        public ushort ManualSpeedRegister { get; set; }

        /// <summary>
        /// 自动速度寄存器地址
        /// </summary>
        /// By:ChengLei
        public ushort AutoSpeedRegister { get; set; }

        /// <summary>
        /// 手动定位目标低 16 位地址
        /// </summary>
        /// By:ChengLei
        public ushort ManualTargetLowRegister { get; set; }

        /// <summary>
        /// 手动定位目标高 16 位地址
        /// </summary>
        /// By:ChengLei
        public ushort ManualTargetHighRegister { get; set; }
    }
}
