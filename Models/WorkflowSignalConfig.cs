using System.Collections.Generic;

namespace Blood_Alcohol.Models
{
    /// <summary>
    /// 流程状态机 PLC 信号配置。
    /// </summary>
    public class WorkflowSignalConfig
    {
        /// <summary>
        /// 扫码允许信号（M位）。
        /// </summary>
        public ushort AllowScanCoil { get; set; } = 400;

        /// <summary>
        /// 扫码完成确认信号（M位）。
        /// </summary>
        public ushort ScanOkCoil { get; set; } = 401;

        /// <summary>
        /// 顶空1放置后允许称重（M位）。
        /// </summary>
        public ushort AllowHs1PlaceWeightCoil { get; set; } = 412;

        /// <summary>
        /// 顶空1放置称重OK确认（M位）。
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
        /// </summary>
        public ushort AllowTubePlaceWeightCoil { get; set; } = 416;

        /// <summary>
        /// 采血管放置称重OK确认（M位）。
        /// </summary>
        public ushort TubePlaceWeightOkCoil { get; set; } = 417;

        /// <summary>
        /// 采血管吸液后允许称重（M位）。
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
        /// </summary>
        public ushort ZAbsolutePositionLowRegister { get; set; } = 1212;

        /// <summary>
        /// Z轴缩放系数。
        /// </summary>
        public ushort ZAbsolutePositionScale { get; set; } = 100;

        /// <summary>
        /// 等待信号超时（秒）。
        /// </summary>
        public int SignalWaitTimeoutSeconds { get; set; } = 180;

        /// <summary>
        /// 脉冲宽度（毫秒）。
        /// </summary>
        public int PulseMilliseconds { get; set; } = 100;

        /// <summary>
        /// 重量缩放系数。
        /// </summary>
        public ushort WeightScaleForPlc { get; set; } = 100;

        /// <summary>
        /// 校验流程信号配置是否合法。
        /// </summary>
        /// <returns>返回配置错误列表，列表为空表示校验通过。</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            AddDuplicateAddressErrors(
                errors,
                new (string Name, ushort Address)[]
                {
                    ("扫码允许信号", AllowScanCoil),
                    ("扫码完成确认信号", ScanOkCoil),
                    ("顶空1放置后允许称重", AllowHs1PlaceWeightCoil),
                    ("顶空1放置称重OK确认", Hs1PlaceWeightOkCoil),
                    ("顶空2放置后允许称重", AllowHs2PlaceWeightCoil),
                    ("顶空2放置称重OK确认", Hs2PlaceWeightOkCoil),
                    ("采血管放置后允许称重", AllowTubePlaceWeightCoil),
                    ("采血管放置称重OK确认", TubePlaceWeightOkCoil),
                    ("采血管吸液后允许称重", AllowTubeAfterAspirateWeightCoil),
                    ("采血管吸液后称重OK确认", TubeAfterAspirateWeightOkCoil),
                    ("顶空1加血液后允许称重", AllowHs1AfterBloodWeightCoil),
                    ("顶空1加血液后称重OK确认", Hs1AfterBloodWeightOkCoil),
                    ("顶空2加血液后允许称重", AllowHs2AfterBloodWeightCoil),
                    ("顶空2加血液后称重OK确认", Hs2AfterBloodWeightOkCoil),
                    ("顶空1加叔丁醇后允许称重", AllowHs1AfterButanolWeightCoil),
                    ("顶空1加叔丁醇后称重OK确认", Hs1AfterButanolWeightOkCoil),
                    ("顶空2加叔丁醇后允许称重", AllowHs2AfterButanolWeightCoil),
                    ("顶空2加叔丁醇后称重OK确认", Hs2AfterButanolWeightOkCoil)
                },
                "流程M位地址");

            if (ZAbsolutePositionLowRegister == ushort.MaxValue)
            {
                errors.Add("Z轴绝对位置低16位地址不能为 65535，因为还需要写入高16位地址。");
            }

            if (ZAbsolutePositionScale <= 0)
            {
                errors.Add("Z轴缩放系数必须大于 0。");
            }

            if (SignalWaitTimeoutSeconds <= 0)
            {
                errors.Add("等待信号超时秒数必须大于 0。");
            }

            if (PulseMilliseconds < 0)
            {
                errors.Add("脉冲宽度毫秒数不能为负数。");
            }

            if (WeightScaleForPlc <= 0)
            {
                errors.Add("重量缩放系数必须大于 0。");
            }

            return errors;
        }

        /// <summary>
        /// 收集重复地址错误。
        /// </summary>
        /// <param name="errors">用于收集错误信息的列表。</param>
        /// <param name="addresses">待校验的地址集合。</param>
        /// <param name="addressType">地址类型说明。</param>
        private static void AddDuplicateAddressErrors(
            List<string> errors,
            IEnumerable<(string Name, ushort Address)> addresses,
            string addressType)
        {
            var usedAddresses = new Dictionary<ushort, string>();
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
}
