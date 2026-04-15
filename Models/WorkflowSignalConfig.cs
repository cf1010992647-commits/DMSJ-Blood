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
        public ushort AllowScanCoil { get; set; } = 400;

        /// <summary>
        /// 扫码完成确认信号（M位）。
        /// PLC置1表示“扫码步骤确认完成”。
        /// </summary>
        public ushort ScanOkCoil { get; set; } = 401;

        /// <summary>
        /// 顶空1放置后允许称重（M位）。
        /// PLC置1后，PC读取天平并写入顶空1放置重量寄存器。
        /// </summary>
        public ushort AllowHs1PlaceWeightCoil { get; set; } = 412;

        /// <summary>
        /// 顶空1放置称重OK确认（M位）。
        /// PLC置1表示该步骤确认完成，PC据此结束等待。
        /// </summary>
        public ushort Hs1PlaceWeightOkCoil { get; set; } = 413;

        

        /// <summary>
        /// 顶空2放置后允许称重（M位）。
        /// </summary>
        public ushort AllowHs2PlaceWeightCoil { get; set; } = 414;

        /// <summary>
        /// 顶空2放置称重OK确认（M位）。
        /// </summary>
        public ushort Hs2PlaceWeightOkCoil { get; set; } = 415;

        
        /// <summary>
        /// 采血管放置后允许称重（M位）。
        /// 该称重结果会参与第一次“重量->Z坐标”换算。
        /// </summary>
        public ushort AllowTubePlaceWeightCoil { get; set; } = 416;

        /// <summary>
        /// 采血管放置称重OK确认（M位）。
        /// </summary>
        public ushort TubePlaceWeightOkCoil { get; set; } = 417;

        

        /// <summary>
        /// 采血管吸液后允许称重（M位）。
        /// 该称重结果会参与第二次“重量->Z坐标”换算。
        /// </summary>
        public ushort AllowTubeAfterAspirateWeightCoil { get; set; } = 418;

        /// <summary>
        /// 采血管吸液后称重OK确认（M位）。
        /// </summary>
        public ushort TubeAfterAspirateWeightOkCoil { get; set; } = 419;

       

        /// <summary>
        /// 顶空1加血液后允许称重（M位）。
        /// </summary>
        public ushort AllowHs1AfterBloodWeightCoil { get; set; } = 420;

        /// <summary>
        /// 顶空1加血液后称重OK确认（M位）。
        /// </summary>
        public ushort Hs1AfterBloodWeightOkCoil { get; set; } = 421;

        

        /// <summary>
        /// 顶空2加血液后允许称重（M位）。
        /// </summary>
        public ushort AllowHs2AfterBloodWeightCoil { get; set; } = 422;

        /// <summary>
        /// 顶空2加血液后称重OK确认（M位）。
        /// </summary>
        public ushort Hs2AfterBloodWeightOkCoil { get; set; } = 423;

        

        /// <summary>
        /// 顶空1加叔丁醇后允许称重（M位）。
        /// </summary>
        public ushort AllowHs1AfterButanolWeightCoil { get; set; } = 424;

        /// <summary>
        /// 顶空1加叔丁醇后称重OK确认（M位）。
        /// </summary>
        public ushort Hs1AfterButanolWeightOkCoil { get; set; } = 425;

        

        /// <summary>
        /// 顶空2加叔丁醇后允许称重（M位）。
        /// </summary>
        public ushort AllowHs2AfterButanolWeightCoil { get; set; } = 426;

        /// <summary>
        /// 顶空2加叔丁醇后称重OK确认（M位）。
        /// </summary>
        public ushort Hs2AfterButanolWeightOkCoil { get; set; } = 427;

        

        /// <summary>
        /// Z轴绝对位置低16位地址（D位）。
        /// PC写入32位目标值时，会同时写该地址和该地址+1（高16位）。
        /// </summary>
        public ushort ZAbsolutePositionLowRegister { get; set; } = 1212;

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
        /// 重量缩放系数。
        /// PC写入称重值到D寄存器时使用：plcWeight = round(weight * WeightScaleForPlc)。
        /// </summary>
        public ushort WeightScaleForPlc { get; set; } = 100;
    }
}
