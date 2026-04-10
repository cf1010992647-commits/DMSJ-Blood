namespace Blood_Alcohol.Models
{
    /// <summary>
    /// DMSJ：流程状态机PLC信号配置。
    /// 说明：
    /// 1. 所有地址默认值均为“占位地址”，用于联调；上线前替换为电气确认的地址。
    /// 2. Coil 对应 M 位（布尔量），Register 对应 D 位（16位无符号整型）。
    /// 3. 当前 WorkflowEngine 采用“读取确认”模式：允许位/OK位都由PC读取是否为1，不做OK脉冲回写。
    /// </summary>
    public class WorkflowSignalConfig
    {
        /// <summary>
        /// 扫码允许信号（M位）。
        /// PLC置1表示“当前可扫码”，PC在此上升沿触发被动接收扫码枪数据。
        /// </summary>
        public ushort AllowScanCoil { get; set; } = 5200;

        /// <summary>
        /// 扫码完成确认信号（M位）。
        /// PLC置1表示“扫码步骤确认完成”。
        /// </summary>
        public ushort ScanOkCoil { get; set; } = 5201;

        /// <summary>
        /// 摇匀时长设定值（D位）。
        /// PC把参数配置页面中的摇匀秒数写入该地址，供PLC后续流程使用。
        /// </summary>
        public ushort ShakeDurationRegister { get; set; } = 5300;

        /// <summary>
        /// 采血管摇匀允许信号（M位）。
        /// PLC置1表示采血管正在/可进入摇匀过程。
        /// </summary>
        public ushort AllowShakeTubeCoil { get; set; } = 5203;

        /// <summary>
        /// 顶空1摇匀允许信号（M位）。
        /// PLC置1表示顶空瓶1正在/可进入摇匀过程。
        /// </summary>
        public ushort AllowShakeHs1Coil { get; set; } = 5204;

        /// <summary>
        /// 顶空2摇匀允许信号（M位）。
        /// PLC置1表示顶空瓶2正在/可进入摇匀过程。
        /// </summary>
        public ushort AllowShakeHs2Coil { get; set; } = 5205;

        /// <summary>
        /// 采血管摇匀当前时长（D位）。
        /// PC周期读取该值用于日志/界面展示。
        /// </summary>
        public ushort ShakeTubeTimeRegister { get; set; } = 5301;

        /// <summary>
        /// 顶空1摇匀当前时长（D位）。
        /// PC周期读取该值用于日志/界面展示。
        /// </summary>
        public ushort ShakeHs1TimeRegister { get; set; } = 5302;

        /// <summary>
        /// 顶空2摇匀当前时长（D位）。
        /// PC周期读取该值用于日志/界面展示。
        /// </summary>
        public ushort ShakeHs2TimeRegister { get; set; } = 5303;

        /// <summary>
        /// 顶空1放置后允许称重（M位）。
        /// PLC置1后，PC读取天平并写入顶空1放置重量寄存器。
        /// </summary>
        public ushort AllowHs1PlaceWeightCoil { get; set; } = 5210;

        /// <summary>
        /// 顶空1放置称重OK确认（M位）。
        /// PLC置1表示该步骤确认完成，PC据此结束等待。
        /// </summary>
        public ushort Hs1PlaceWeightOkCoil { get; set; } = 5211;

        /// <summary>
        /// 顶空1放置重量数据（D位）。
        /// PC将称重值按 WeightScaleForPlc 缩放后写入。
        /// </summary>
        public ushort Hs1PlaceWeightRegister { get; set; } = 5400;

        /// <summary>
        /// 顶空2放置后允许称重（M位）。
        /// </summary>
        public ushort AllowHs2PlaceWeightCoil { get; set; } = 5212;

        /// <summary>
        /// 顶空2放置称重OK确认（M位）。
        /// </summary>
        public ushort Hs2PlaceWeightOkCoil { get; set; } = 5213;

        /// <summary>
        /// 顶空2放置重量数据（D位）。
        /// </summary>
        public ushort Hs2PlaceWeightRegister { get; set; } = 5402;

        /// <summary>
        /// 采血管放置后允许称重（M位）。
        /// 该称重结果会参与第一次“重量->Z坐标”换算。
        /// </summary>
        public ushort AllowTubePlaceWeightCoil { get; set; } = 5220;

        /// <summary>
        /// 采血管放置称重OK确认（M位）。
        /// </summary>
        public ushort TubePlaceWeightOkCoil { get; set; } = 5221;

        /// <summary>
        /// 采血管放置重量数据（D位）。
        /// </summary>
        public ushort TubePlaceWeightRegister { get; set; } = 5410;

        /// <summary>
        /// 采血管吸液后允许称重（M位）。
        /// 该称重结果会参与第二次“重量->Z坐标”换算。
        /// </summary>
        public ushort AllowTubeAfterAspirateWeightCoil { get; set; } = 5222;

        /// <summary>
        /// 采血管吸液后称重OK确认（M位）。
        /// </summary>
        public ushort TubeAfterAspirateWeightOkCoil { get; set; } = 5223;

        /// <summary>
        /// 采血管吸液后重量数据（D位）。
        /// </summary>
        public ushort TubeAfterAspirateWeightRegister { get; set; } = 5412;

        /// <summary>
        /// 顶空1加血液后允许称重（M位）。
        /// </summary>
        public ushort AllowHs1AfterBloodWeightCoil { get; set; } = 5230;

        /// <summary>
        /// 顶空1加血液后称重OK确认（M位）。
        /// </summary>
        public ushort Hs1AfterBloodWeightOkCoil { get; set; } = 5231;

        /// <summary>
        /// 顶空1加血液后重量数据（D位）。
        /// </summary>
        public ushort Hs1AfterBloodWeightRegister { get; set; } = 5420;

        /// <summary>
        /// 顶空2加血液后允许称重（M位）。
        /// </summary>
        public ushort AllowHs2AfterBloodWeightCoil { get; set; } = 5232;

        /// <summary>
        /// 顶空2加血液后称重OK确认（M位）。
        /// </summary>
        public ushort Hs2AfterBloodWeightOkCoil { get; set; } = 5233;

        /// <summary>
        /// 顶空2加血液后重量数据（D位）。
        /// </summary>
        public ushort Hs2AfterBloodWeightRegister { get; set; } = 5422;

        /// <summary>
        /// 顶空1加叔丁醇后允许称重（M位）。
        /// </summary>
        public ushort AllowHs1AfterButanolWeightCoil { get; set; } = 5240;

        /// <summary>
        /// 顶空1加叔丁醇后称重OK确认（M位）。
        /// </summary>
        public ushort Hs1AfterButanolWeightOkCoil { get; set; } = 5241;

        /// <summary>
        /// 顶空1加叔丁醇后重量数据（D位）。
        /// </summary>
        public ushort Hs1AfterButanolWeightRegister { get; set; } = 5430;

        /// <summary>
        /// 顶空2加叔丁醇后允许称重（M位）。
        /// </summary>
        public ushort AllowHs2AfterButanolWeightCoil { get; set; } = 5242;

        /// <summary>
        /// 顶空2加叔丁醇后称重OK确认（M位）。
        /// </summary>
        public ushort Hs2AfterButanolWeightOkCoil { get; set; } = 5243;

        /// <summary>
        /// 顶空2加叔丁醇后重量数据（D位）。
        /// </summary>
        public ushort Hs2AfterButanolWeightRegister { get; set; } = 5432;

        /// <summary>
        /// Z轴绝对位置低16位地址（D位）。
        /// PC写入32位目标值时，会同时写该地址和该地址+1（高16位）。
        /// </summary>
        public ushort ZAbsolutePositionLowRegister { get; set; } = 5500;

        /// <summary>
        /// Z轴缩放系数。
        /// 用于将工程量(mm)换算到PLC整型值：raw = round(mm * ZAbsolutePositionScale)。
        /// </summary>
        public ushort ZAbsolutePositionScale { get; set; } = 100;

        /// <summary>
        /// 等待信号超时（秒）。
        /// 适用于等待允许位/OK位为1的超时控制。
        /// </summary>
        public int SignalWaitTimeoutSeconds { get; set; } = 180;

        /// <summary>
        /// 脉冲宽度（毫秒）。
        /// 当前读取确认模式一般不使用；保留供后续切回脉冲回写时使用。
        /// </summary>
        public int PulseMilliseconds { get; set; } = 100;

        /// <summary>
        /// 摇匀进度轮询间隔（毫秒）。
        /// PC读取摇匀时间寄存器的周期。
        /// </summary>
        public int ShakeMonitorIntervalMilliseconds { get; set; } = 500;

        /// <summary>
        /// 重量缩放系数。
        /// PC写入称重值到D寄存器时使用：plcWeight = round(weight * WeightScaleForPlc)。
        /// </summary>
        public ushort WeightScaleForPlc { get; set; } = 100;
    }
}
