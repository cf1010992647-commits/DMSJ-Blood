using Blood_Alcohol.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Blood_Alcohol.ViewModels
{
    /// <summary>
    /// 气缸控制页视图模型，负责按气缸维度展示手动原位和工位控制。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 页面显示的地址使用项目 M 地址，底层 PLC 通信自动补齐 4096 偏移。
    /// </remarks>
    public sealed class CylinderControlViewModel : BaseViewModel
    {
        private static readonly TimeSpan PulseWidth = TimeSpan.FromMilliseconds(100);

        private readonly SemaphoreSlim _plcLock = CommunicationManager.PlcAccessLock;
        private string _statusMessage = "气缸控制已就绪，页面显示 M102-M145，底层通信自动补齐 4096 偏移。";

        /// <summary>
        /// 获取气缸控制项集合。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由构造函数按固定地址表一次性生成，界面按气缸一行展示。
        /// </remarks>
        public ObservableCollection<CylinderControlItemViewModel> Cylinders { get; } = new();

        /// <summary>
        /// 获取或设置页面状态提示文本。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由脉冲下发流程更新，用于提示 PLC 连接状态与控制结果。
        /// </remarks>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 初始化气缸控制页视图模型。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 CylinderControlView 创建为 DataContext 时调用，构建固定气缸列表。
        /// </remarks>
        public CylinderControlViewModel()
        {
            AddCylinder(1, "Z轴_夹爪开合气缸", "放松", "夹紧");
            AddCylinder(2, "Z轴_夹爪上下气缸", "上升", "下降");
            AddCylinder(3, "移液枪上下气缸", "上升", "下降");
            AddCylinder(4, "采血管_管身夹爪开合气缸", "放松", "夹紧");
            AddCylinder(5, "采血管_管身移栽前后气缸", "后退", "前进");
            AddCylinder(6, "采血管_盖子夹爪开合气缸", "放松", "夹紧");
            AddCylinder(7, "采血管_盖子夹爪上下气缸", "上升", "下降");
            AddCylinder(8, "叔丁醇_加液左右气缸", "左位", "右位");
            AddCylinder(9, "叔丁醇_加液柱塞泵气缸", "打液", "吸液");
            AddCylinder(10, "叔丁醇_原液开关气缸", "关闭", "打开");
            AddCylinder(11, "叔丁醇_给A开关气缸", "关闭", "打开");
            AddCylinder(12, "叔丁醇_给B开关气缸", "关闭", "打开");
            AddCylinder(13, "顶空合盖_前后气缸", "后退", "前进");
            AddCylinder(14, "顶空合盖_上下气缸", "上升", "下降");
            AddCylinder(15, "顶空合盖_钳盖气缸", "打开", "关闭");
            AddCylinder(16, "摇匀_摇晃气缸", "竖直", "倾斜");
            AddCylinder(17, "顶空进样器_瓶子AB切换气缸", "前进", "后退");
            AddCylinder(18, "顶空进样器_进针上下气缸", "下降", "上升");
            AddCylinder(19, "顶空进样器_退针上下气缸", "上升", "下降");
            AddCylinder(20, "顶空进样器_加压阀", "原位", "工位");
            AddCylinder(21, "顶空进样器_排空阀", "原位", "工位");
            AddCylinder(22, "移液枪吸打液气缸", "打液", "吸液");
        }

        /// <summary>
        /// 添加一个气缸控制项到集合。
        /// </summary>
        /// By:ChengLei
        /// <param name="index">气缸序号。</param>
        /// <param name="displayName">气缸显示名称。</param>
        /// <param name="homeActionText">原位动作说明。</param>
        /// <param name="workActionText">工位动作说明。</param>
        /// <remarks>
        /// 地址按 M102-M145 顺序自动推导，信号名按 CxGoHome_ManXX 和 CxGoWork_ManXX 生成。
        /// </remarks>
        private void AddCylinder(int index, string displayName, string homeActionText, string workActionText)
        {
            ushort homeAddress = (ushort)(102 + (index - 1) * 2);
            ushort workAddress = (ushort)(103 + (index - 1) * 2);
            string code = $"C{index:00}";

            Cylinders.Add(new CylinderControlItemViewModel(
                code,
                displayName,
                $"CxGoHome_Man{index:00}",
                homeAddress,
                homeActionText,
                $"CxGoWork_Man{index:00}",
                workAddress,
                workActionText,
                () => PulseCylinderAsync(code, displayName, homeAddress, $"原位（{homeActionText}）"),
                () => PulseCylinderAsync(code, displayName, workAddress, $"工位（{workActionText}）")));
        }

        /// <summary>
        /// 向指定气缸地址发送手动控制脉冲。
        /// </summary>
        /// By:ChengLei
        /// <param name="cylinderCode">气缸编号。</param>
        /// <param name="displayName">气缸显示名称。</param>
        /// <param name="displayAddress">页面显示地址。</param>
        /// <param name="actionText">动作说明文本。</param>
        /// <returns>返回异步脉冲发送任务。</returns>
        /// <remarks>
        /// 先写入 1，再延时 100ms 后回写 0，适配 PLC 手动命令脉冲触发方式。
        /// </remarks>
        private async Task PulseCylinderAsync(string cylinderCode, string displayName, ushort displayAddress, string actionText)
        {
            if (!CommunicationManager.Is485Open)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} {cylinderCode} {displayName} {actionText}失败：PLC 未连接。";
                return;
            }

            await _plcLock.WaitAsync().ConfigureAwait(true);
            try
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} {cylinderCode} {displayName} {actionText}，写入 M{displayAddress}=1。";

                (bool Success, string Error) writeHigh = await CommunicationManager.Plc
                    .TryWriteSingleCoilAsync(displayAddress, true)
                    .ConfigureAwait(true);
                if (!writeHigh.Success)
                {
                    throw new InvalidOperationException(writeHigh.Error);
                }

                await Task.Delay(PulseWidth).ConfigureAwait(true);

                (bool Success, string Error) writeLow = await CommunicationManager.Plc
                    .TryWriteSingleCoilAsync(displayAddress, false)
                    .ConfigureAwait(true);
                if (!writeLow.Success)
                {
                    throw new InvalidOperationException(writeLow.Error);
                }

                StatusMessage = $"{DateTime.Now:HH:mm:ss} {cylinderCode} {displayName} {actionText}完成，M{displayAddress} 脉冲已发送。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} {cylinderCode} {displayName} {actionText}失败：{ex.Message}";
            }
            finally
            {
                _plcLock.Release();
            }
        }
    }

    /// <summary>
    /// 单个气缸控制项视图模型，承载名称、地址和原位工位命令。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 由 CylinderControlViewModel 构建并绑定到列表，一行代表一个气缸。
    /// </remarks>
    public sealed class CylinderControlItemViewModel : BaseViewModel
    {
        /// <summary>
        /// 初始化单个气缸控制项。
        /// </summary>
        /// By:ChengLei
        /// <param name="cylinderCode">气缸编号。</param>
        /// <param name="displayName">气缸显示名称。</param>
        /// <param name="homeSignalName">原位信号名。</param>
        /// <param name="homeAddress">原位显示地址。</param>
        /// <param name="homeActionText">原位动作说明。</param>
        /// <param name="workSignalName">工位信号名。</param>
        /// <param name="workAddress">工位显示地址。</param>
        /// <param name="workActionText">工位动作说明。</param>
        /// <param name="goHomeAsync">原位异步命令委托。</param>
        /// <param name="goWorkAsync">工位异步命令委托。</param>
        /// <remarks>
        /// 由 CylinderControlViewModel 按固定配置构造。
        /// </remarks>
        public CylinderControlItemViewModel(
            string cylinderCode,
            string displayName,
            string homeSignalName,
            ushort homeAddress,
            string homeActionText,
            string workSignalName,
            ushort workAddress,
            string workActionText,
            Func<Task> goHomeAsync,
            Func<Task> goWorkAsync)
        {
            CylinderCode = cylinderCode;
            DisplayName = displayName;
            HomeSignalName = homeSignalName;
            HomeAddress = $"M{homeAddress}";
            HomeActionText = homeActionText;
            WorkSignalName = workSignalName;
            WorkAddress = $"M{workAddress}";
            WorkActionText = workActionText;
            GoHomeCommand = new AsyncRelayCommand(goHomeAsync);
            GoWorkCommand = new AsyncRelayCommand(goWorkAsync);
        }

        /// <summary>
        /// 获取气缸编号。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 用于首列显示 C01-C22。
        /// </remarks>
        public string CylinderCode { get; }

        /// <summary>
        /// 获取气缸显示名称。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 使用设备中文名称描述当前气缸用途。
        /// </remarks>
        public string DisplayName { get; }

        /// <summary>
        /// 获取原位信号名称。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 用于显示 PLC 原位手动控制点位名。
        /// </remarks>
        public string HomeSignalName { get; }

        /// <summary>
        /// 获取原位显示地址。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 页面显示为项目 M 地址，底层通信自动补齐 4096 偏移。
        /// </remarks>
        public string HomeAddress { get; }

        /// <summary>
        /// 获取原位动作说明。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 例如 放松、上升、关闭 等，用于按钮与说明显示。
        /// </remarks>
        public string HomeActionText { get; }

        /// <summary>
        /// 获取工位信号名称。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 用于显示 PLC 工位手动控制点位名。
        /// </remarks>
        public string WorkSignalName { get; }

        /// <summary>
        /// 获取工位显示地址。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 页面显示为项目 M 地址，底层通信自动补齐 4096 偏移。
        /// </remarks>
        public string WorkAddress { get; }

        /// <summary>
        /// 获取工位动作说明。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 例如 夹紧、下降、打开 等，用于按钮与说明显示。
        /// </remarks>
        public string WorkActionText { get; }

        /// <summary>
        /// 获取原位命令。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 点击后向原位线圈发送短脉冲。
        /// </remarks>
        public ICommand GoHomeCommand { get; }

        /// <summary>
        /// 获取工位命令。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 点击后向工位线圈发送短脉冲。
        /// </remarks>
        public ICommand GoWorkCommand { get; }
    }
}
