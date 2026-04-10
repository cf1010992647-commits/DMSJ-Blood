using Blood_Alcohol.Communication.Serial;
using Blood_Alcohol.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Blood_Alcohol.ViewModels
{
    public class CoordinateDebugViewModel : BaseViewModel
    {
        private const string CoordinateConfigFileName = "CoordinateDebugConfig.json";
        private static readonly string[] OtherPositionNameSuffixes =
        {
            "待机位",
            "摇匀_采血管位置",
            "摇匀_顶空位置",
            "顶空盖子暂放台_盖子1位置",
            "顶空盖子暂放台_盖子2位置",
            "扫码_采血管位置",
            "顶空合盖_顶空瓶位置",
            "采血管开合盖_采血管位置",
            "天平_顶空瓶1位置",
            "天平_顶空瓶2位置",
            "天平_采血管位置",
            "枪头_丢弃位置",
            "顶空进样器_放料位1",
            "顶空进样器_放料位2"
        };

        private readonly ConfigService<CoordinateDebugConfig> _configService;
        private string _statusMessage = "坐标调试已加载。";

        public CoordinateProfileViewModel BloodTubeProfile { get; }
        public CoordinateProfileViewModel HeadspaceVialProfile { get; }
        public CoordinateProfileViewModel OtherPositionProfile { get; }
        public CoordinateProfileViewModel PipetteTipProfile { get; }
        public ZCoordinateProfileViewModel ZAxisProfile { get; }

        public ICommand SaveConfigCommand { get; }
        public ICommand ReloadConfigCommand { get; }

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

        public CoordinateDebugViewModel()
        {
            _configService = new ConfigService<CoordinateDebugConfig>(CoordinateConfigFileName);

            BloodTubeProfile = new CoordinateProfileViewModel(
                "采血管XY",
                CommunicationManager.Plc,
                CommunicationManager.PlcAccessLock,
                msg => StatusMessage = $"{DateTime.Now:HH:mm:ss} {msg}",
                index => $"M1XP{index}采血管料盘NO{index}");

            HeadspaceVialProfile = new CoordinateProfileViewModel(
                "顶空瓶XY",
                CommunicationManager.Plc,
                CommunicationManager.PlcAccessLock,
                msg => StatusMessage = $"{DateTime.Now:HH:mm:ss} {msg}",
                index => $"M1XP{100 + index}顶空瓶料盘NO{index}");

            OtherPositionProfile = new CoordinateProfileViewModel(
                "其他工位XY",
                CommunicationManager.Plc,
                CommunicationManager.PlcAccessLock,
                msg => StatusMessage = $"{DateTime.Now:HH:mm:ss} {msg}",
                BuildOtherPositionDescription);

            PipetteTipProfile = new CoordinateProfileViewModel(
                "枪头XY",
                CommunicationManager.Plc,
                CommunicationManager.PlcAccessLock,
                msg => StatusMessage = $"{DateTime.Now:HH:mm:ss} {msg}",
                BuildPipetteTipDescription);

            ZAxisProfile = new ZCoordinateProfileViewModel(
                "Z轴坐标",
                CommunicationManager.Plc,
                CommunicationManager.PlcAccessLock,
                msg => StatusMessage = $"{DateTime.Now:HH:mm:ss} {msg}");

            SaveConfigCommand = new RelayCommand(_ => SaveConfig());
            ReloadConfigCommand = new RelayCommand(_ => LoadConfig());

            LoadConfig();
        }

        private void LoadConfig()
        {
            try
            {
                CoordinateDebugConfig config = _configService.Load() ?? new CoordinateDebugConfig();

                BloodTubeProfile.ApplySettings(config.BloodTube);
                HeadspaceVialProfile.ApplySettings(config.HeadspaceVial);
                OtherPositionProfile.ApplySettings(config.OtherPosition);
                PipetteTipProfile.ApplySettings(config.PipetteTip);
                ZAxisProfile.ApplySettings(config.ZAxis);

                StatusMessage = $"{DateTime.Now:HH:mm:ss} 坐标配置已加载。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 坐标配置加载失败: {ex.Message}";
            }
        }

        private void SaveConfig()
        {
            try
            {
                CoordinateDebugConfig config = new()
                {
                    BloodTube = BloodTubeProfile.ExportSettings(),
                    HeadspaceVial = HeadspaceVialProfile.ExportSettings(),
                    OtherPosition = OtherPositionProfile.ExportSettings(),
                    PipetteTip = PipetteTipProfile.ExportSettings(),
                    ZAxis = ZAxisProfile.ExportSettings()
                };

                _configService.Save(config);
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 坐标配置已保存。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 坐标配置保存失败: {ex.Message}";
            }
        }

        private static string BuildOtherPositionDescription(int index)
        {
            int xp = 300 + index - 1;
            string suffix = index <= OtherPositionNameSuffixes.Length
                ? OtherPositionNameSuffixes[index - 1]
                : "预留";

            return $"M1XP{xp}{suffix}";
        }

        private static string BuildPipetteTipDescription(int index)
        {
            int xp = 400 + index - 1;
            if (index == 1)
            {
                return $"M1XP{xp}占空";
            }

            return $"M1XP{xp}枪头NO{index - 1}";
        }
    }

    public class CoordinateProfileViewModel : BaseViewModel
    {
        private readonly Lx5vPlc _plc;
        private readonly SemaphoreSlim _plcLock;
        private readonly Action<string> _statusCallback;
        private readonly Func<int, string> _descriptionFactory;

        private string _name;
        private int _rows;
        private int _columns;
        private int _xStartAddress;
        private int _yStartAddress;
        private int _registerStridePerPoint;
        private int _currentXAddress;
        private int _currentYAddress;
        private double _baseX;
        private double _baseY;
        private double _stepX;
        private double _stepY;
        private double _scale;
        private bool _isBusy;
        private string _profileStatusMessage = "未执行操作。";
        private CoordinatePointItemViewModel? _selectedPoint;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Rows
        {
            get => _rows;
            set
            {
                if (_rows != value)
                {
                    _rows = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPoints));
                }
            }
        }

        public int Columns
        {
            get => _columns;
            set
            {
                if (_columns != value)
                {
                    _columns = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPoints));
                }
            }
        }

        public int XStartAddress
        {
            get => _xStartAddress;
            set
            {
                if (_xStartAddress != value)
                {
                    _xStartAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public int YStartAddress
        {
            get => _yStartAddress;
            set
            {
                if (_yStartAddress != value)
                {
                    _yStartAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public int RegisterStridePerPoint
        {
            get => _registerStridePerPoint;
            set
            {
                if (_registerStridePerPoint != value)
                {
                    _registerStridePerPoint = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CurrentXAddress
        {
            get => _currentXAddress;
            set
            {
                if (_currentXAddress != value)
                {
                    _currentXAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CurrentYAddress
        {
            get => _currentYAddress;
            set
            {
                if (_currentYAddress != value)
                {
                    _currentYAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public double BaseX
        {
            get => _baseX;
            set
            {
                if (Math.Abs(_baseX - value) > 0.000001d)
                {
                    _baseX = value;
                    OnPropertyChanged();
                }
            }
        }

        public double BaseY
        {
            get => _baseY;
            set
            {
                if (Math.Abs(_baseY - value) > 0.000001d)
                {
                    _baseY = value;
                    OnPropertyChanged();
                }
            }
        }

        public double StepX
        {
            get => _stepX;
            set
            {
                if (Math.Abs(_stepX - value) > 0.000001d)
                {
                    _stepX = value;
                    OnPropertyChanged();
                }
            }
        }

        public double StepY
        {
            get => _stepY;
            set
            {
                if (Math.Abs(_stepY - value) > 0.000001d)
                {
                    _stepY = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Scale
        {
            get => _scale;
            set
            {
                if (Math.Abs(_scale - value) > 0.000001d)
                {
                    _scale = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ProfileStatusMessage
        {
            get => _profileStatusMessage;
            set
            {
                if (_profileStatusMessage != value)
                {
                    _profileStatusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public int TotalPoints => Points.Count;

        public ObservableCollection<CoordinatePointItemViewModel> Points { get; } = new();

        public CoordinatePointItemViewModel? SelectedPoint
        {
            get => _selectedPoint;
            set
            {
                if (!ReferenceEquals(_selectedPoint, value))
                {
                    _selectedPoint = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand BuildGridCommand { get; }
        public ICommand GenerateFromBaseCommand { get; }
        public ICommand ReadCurrentToBaseCommand { get; }
        public ICommand ReadPointsFromPlcCommand { get; }
        public ICommand WriteAllPointsToPlcCommand { get; }
        public ICommand WriteSelectedPointToPlcCommand { get; }
        public ICommand SetBaseFromSelectedPointCommand { get; }

        public CoordinateProfileViewModel(
            string name,
            Lx5vPlc plc,
            SemaphoreSlim plcLock,
            Action<string> statusCallback,
            Func<int, string>? descriptionFactory = null)
        {
            _name = name;
            _plc = plc;
            _plcLock = plcLock;
            _statusCallback = statusCallback;
            _descriptionFactory = descriptionFactory ?? (index => $"P{index:000}");

            Rows = 5;
            Columns = 10;
            XStartAddress = 5100;
            YStartAddress = 5200;
            RegisterStridePerPoint = 2;
            CurrentXAddress = 5100;
            CurrentYAddress = 5200;
            BaseX = 0;
            BaseY = 0;
            StepX = 0;
            StepY = 0;
            Scale = 100;

            BuildGridCommand = new RelayCommand(_ => BuildGrid(), _ => !IsBusy);
            GenerateFromBaseCommand = new RelayCommand(_ => GenerateFromBase(), _ => !IsBusy);
            ReadCurrentToBaseCommand = new RelayCommand(_ => _ = ReadCurrentToBaseAsync(), _ => !IsBusy);
            ReadPointsFromPlcCommand = new RelayCommand(_ => _ = ReadPointsFromPlcAsync(), _ => !IsBusy);
            WriteAllPointsToPlcCommand = new RelayCommand(_ => _ = WriteAllPointsToPlcAsync(), _ => !IsBusy && Points.Count > 0);
            WriteSelectedPointToPlcCommand = new RelayCommand(_ => _ = WriteSelectedPointToPlcAsync(), _ => !IsBusy && SelectedPoint != null);
            SetBaseFromSelectedPointCommand = new RelayCommand(_ => SetBaseFromSelectedPoint(), _ => !IsBusy && SelectedPoint != null);

            BuildGrid();
        }

        public void ApplySettings(CoordinateProfileSettings settings)
        {
            CoordinateProfileSettings safeSettings = settings ?? new CoordinateProfileSettings();
            bool hasNewAddressSettings = safeSettings.XStartAddress > 0 || safeSettings.YStartAddress > 0;

            Rows = Math.Max(1, safeSettings.Rows);
            Columns = Math.Max(1, safeSettings.Columns);
            RegisterStridePerPoint = hasNewAddressSettings
                ? Math.Max(2, safeSettings.RegisterStridePerPoint)
                : 2;

            XStartAddress = hasNewAddressSettings
                ? Math.Max(0, safeSettings.XStartAddress)
                : XStartAddress;
            YStartAddress = hasNewAddressSettings
                ? Math.Max(0, safeSettings.YStartAddress)
                : YStartAddress;

            CurrentXAddress = hasNewAddressSettings
                ? Math.Max(0, safeSettings.CurrentXAddress)
                : CurrentXAddress;
            CurrentYAddress = hasNewAddressSettings
                ? Math.Max(0, safeSettings.CurrentYAddress)
                : CurrentYAddress;
            BaseX = safeSettings.BaseX;
            BaseY = safeSettings.BaseY;
            StepX = safeSettings.StepX;
            StepY = safeSettings.StepY;
            Scale = safeSettings.Scale <= 0 ? 100 : safeSettings.Scale;

            if (safeSettings.Points is { Count: > 0 })
            {
                LoadPointsFromSettings(safeSettings.Points);
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已从配置加载 {Points.Count} 个独立点位。";
            }
            else
            {
                BuildGrid();
            }
        }

        public CoordinateProfileSettings ExportSettings()
        {
            return new CoordinateProfileSettings
            {
                Rows = Rows,
                Columns = Columns,
                XStartAddress = XStartAddress,
                YStartAddress = YStartAddress,
                RegisterStridePerPoint = RegisterStridePerPoint,
                CurrentXAddress = CurrentXAddress,
                CurrentYAddress = CurrentYAddress,
                BaseX = BaseX,
                BaseY = BaseY,
                StepX = StepX,
                StepY = StepY,
                Scale = Scale,
                Points = Points.Select(point => new CoordinatePointSetting
                {
                    Index = point.Index,
                    Row = point.Row,
                    Column = point.Column,
                    Description = point.Description,
                    XAddress = point.XAddress,
                    YAddress = point.YAddress,
                    X = point.X,
                    Y = point.Y
                }).ToList(),

                // DMSJ：兼容历史字段，避免旧版本读取时出现默认值误判。
                TableStartAddress = XStartAddress
            };
        }

        private async Task ExecuteBusyAsync(Func<Task> action, string successMessage)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                ValidateProfileSettings();
                await action();
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} {successMessage}";
                _statusCallback($"{Name}: {successMessage}");
            }
            catch (Exception ex)
            {
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 操作失败: {ex.Message}";
                _statusCallback($"{Name}: 操作失败 - {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ValidateProfileSettings()
        {
            if (!CommunicationManager.Is485Open)
            {
                throw new InvalidOperationException("RS485未连接，请先连接通讯。");
            }

            if (Rows <= 0 || Columns <= 0)
            {
                throw new InvalidOperationException("行列数必须大于0。");
            }

            if (RegisterStridePerPoint < 2)
            {
                throw new InvalidOperationException("每点寄存器步长至少为2（每轴占低位/高位两个寄存器）。");
            }

            if (Scale <= 0)
            {
                throw new InvalidOperationException("比例系数必须大于0。");
            }

            if (XStartAddress < 0 || YStartAddress < 0)
            {
                throw new InvalidOperationException("起始地址不能为负数。");
            }
        }

        private static ushort EnsureUShortAddress(int value, string fieldName)
        {
            if (value < 0 || value > ushort.MaxValue)
            {
                throw new InvalidOperationException($"{fieldName}超出PLC地址范围(0-65535): {value}");
            }

            return (ushort)value;
        }

        private void BuildGrid()
        {
            int rows = Math.Max(1, Rows);
            int columns = Math.Max(1, Columns);
            int stride = Math.Max(2, RegisterStridePerPoint);

            Points.Clear();

            int index = 1;
            for (int row = 1; row <= rows; row++)
            {
                for (int col = 1; col <= columns; col++)
                {
                    int offsetIndex = (row - 1) * columns + (col - 1);

                    int xAddress = XStartAddress + offsetIndex * stride;
                    int yAddress = YStartAddress + offsetIndex * stride;

                    Points.Add(new CoordinatePointItemViewModel
                    {
                        Index = index,
                        Row = row,
                        Column = col,
                        Description = _descriptionFactory(index),
                        XAddress = xAddress,
                        YAddress = yAddress
                    });

                    index++;
                }
            }

            OnPropertyChanged(nameof(TotalPoints));
            GenerateFromBase();
            ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已重建点位列表，共 {Points.Count} 点。";
        }

        private void LoadPointsFromSettings(IReadOnlyList<CoordinatePointSetting> pointSettings)
        {
            Points.Clear();

            int fallbackIndex = 1;
            foreach (CoordinatePointSetting item in pointSettings.OrderBy(p => p.Index <= 0 ? int.MaxValue : p.Index))
            {
                int index = item.Index > 0 ? item.Index : fallbackIndex;
                int row = item.Row > 0 ? item.Row : 1;
                int column = item.Column > 0 ? item.Column : index;

                Points.Add(new CoordinatePointItemViewModel
                {
                    Index = index,
                    Row = row,
                    Column = column,
                    Description = string.IsNullOrWhiteSpace(item.Description) ? _descriptionFactory(index) : item.Description,
                    XAddress = item.XAddress,
                    YAddress = item.YAddress,
                    X = item.X,
                    Y = item.Y,
                    IsGenerated = false
                });

                fallbackIndex++;
            }

            if (Points.Count > 0)
            {
                Rows = Math.Max(1, Points.Max(point => point.Row));
                Columns = Math.Max(1, Points.Max(point => point.Column));
            }

            OnPropertyChanged(nameof(TotalPoints));
        }

        private void GenerateFromBase()
        {
            if (Points.Count == 0)
            {
                return;
            }

            foreach (CoordinatePointItemViewModel point in Points)
            {
                point.X = BaseX + (point.Column - 1) * StepX;
                point.Y = BaseY + (point.Row - 1) * StepY;
                point.IsGenerated = true;
            }

            ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已按起点/步距推导 {Points.Count} 个点位。";
        }

        private async Task ReadCurrentToBaseAsync()
        {
            await ExecuteBusyAsync(async () =>
            {
                ushort xAddr = EnsureUShortAddress(CurrentXAddress, "当前X低位地址");
                ushort yAddr = EnsureUShortAddress(CurrentYAddress, "当前Y低位地址");

                BaseX = FromInt32(await ReadInt32AtAddressAsync(xAddr));
                BaseY = FromInt32(await ReadInt32AtAddressAsync(yAddr));

                GenerateFromBase();
            }, "已读取当前坐标并更新起点。");
        }

        private async Task ReadPointsFromPlcAsync()
        {
            await ExecuteBusyAsync(async () =>
            {
                foreach (CoordinatePointItemViewModel point in Points)
                {
                    EnsureAxisAddressRange(point.XAddress, "X轴地址");
                    EnsureAxisAddressRange(point.YAddress, "Y轴地址");

                    ushort xAddr = EnsureUShortAddress(point.XAddress, "X低位地址");
                    ushort yAddr = EnsureUShortAddress(point.YAddress, "Y低位地址");

                    point.X = FromInt32(await ReadInt32AtAddressAsync(xAddr));
                    point.Y = FromInt32(await ReadInt32AtAddressAsync(yAddr));
                    point.IsGenerated = false;
                }
            }, $"已从PLC读取 {Points.Count} 个点位。");
        }

        private async Task WriteAllPointsToPlcAsync()
        {
            await ExecuteBusyAsync(async () =>
            {
                foreach (CoordinatePointItemViewModel point in Points)
                {
                    await WritePointAsync(point);
                }
            }, $"已写入PLC {Points.Count} 个点位。");
        }

        private async Task WriteSelectedPointToPlcAsync()
        {
            if (SelectedPoint == null)
            {
                return;
            }

            await ExecuteBusyAsync(
                async () => await WritePointAsync(SelectedPoint),
                $"已写入选中点 P{SelectedPoint.Index:000}。");
        }

        private void SetBaseFromSelectedPoint()
        {
            if (SelectedPoint == null)
            {
                return;
            }

            BaseX = SelectedPoint.X;
            BaseY = SelectedPoint.Y;

            ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已将 P{SelectedPoint.Index:000} 设为起点。";
            _statusCallback($"{Name}: 已将选中点设为起点。");
        }

        private async Task WritePointAsync(CoordinatePointItemViewModel point)
        {
            EnsureAxisAddressRange(point.XAddress, "X轴地址");
            EnsureAxisAddressRange(point.YAddress, "Y轴地址");

            ushort xAddr = EnsureUShortAddress(point.XAddress, "X低位地址");
            ushort yAddr = EnsureUShortAddress(point.YAddress, "Y低位地址");

            int xRaw = ToInt32(point.X);
            int yRaw = ToInt32(point.Y);

            await WriteInt32AtAddressAsync(xAddr, xRaw);
            await WriteInt32AtAddressAsync(yAddr, yRaw);
        }

        private static void EnsureAxisAddressRange(int lowAddress, string fieldName)
        {
            if (lowAddress < 0 || lowAddress > ushort.MaxValue - 1)
            {
                throw new InvalidOperationException($"{fieldName}越界，双寄存器写入要求低位地址范围为 0-65534: {lowAddress}");
            }
        }

        private double FromInt32(int raw)
        {
            return raw / Scale;
        }

        private int ToInt32(double value)
        {
            double scaled = Math.Round(value * Scale, MidpointRounding.AwayFromZero);
            if (scaled < int.MinValue || scaled > int.MaxValue)
            {
                string text = value.ToString("F3", CultureInfo.InvariantCulture);
                throw new InvalidOperationException($"坐标值超出32位寄存器范围: {text}");
            }

            return (int)scaled;
        }

        private static int ComposeInt32(ushort lowWord, ushort highWord)
        {
            uint raw = ((uint)highWord << 16) | lowWord;
            return unchecked((int)raw);
        }

        private static void SplitInt32(int value, out ushort lowWord, out ushort highWord)
        {
            unchecked
            {
                uint raw = (uint)value;
                lowWord = (ushort)(raw & 0xFFFF);
                highWord = (ushort)((raw >> 16) & 0xFFFF);
            }
        }

        private async Task<int> ReadInt32AtAddressAsync(ushort lowAddress)
        {
            ushort[] regs = await ReadHoldingRegistersAsync(lowAddress, 2);
            if (regs.Length < 2)
            {
                throw new InvalidOperationException($"读取地址 {lowAddress} 失败，返回长度不足2。");
            }

            return ComposeInt32(regs[0], regs[1]);
        }

        private async Task WriteInt32AtAddressAsync(ushort lowAddress, int value)
        {
            SplitInt32(value, out ushort lowWord, out ushort highWord);

            await _plcLock.WaitAsync();
            try
            {
                var writeLow = await _plc.TryWriteSingleRegisterAsync(lowAddress, lowWord);
                if (!writeLow.Success)
                {
                    throw new InvalidOperationException(writeLow.Error);
                }

                var writeHigh = await _plc.TryWriteSingleRegisterAsync((ushort)(lowAddress + 1), highWord);
                if (!writeHigh.Success)
                {
                    throw new InvalidOperationException(writeHigh.Error);
                }
            }
            finally
            {
                _plcLock.Release();
            }
        }

        private async Task<ushort[]> ReadHoldingRegistersAsync(ushort address, ushort length)
        {
            await _plcLock.WaitAsync();
            try
            {
                var read = await _plc.TryReadHoldingRegistersAsync(address, length);
                if (!read.Success)
                {
                    throw new InvalidOperationException(read.Error);
                }

                return read.Values;
            }
            finally
            {
                _plcLock.Release();
            }
        }
    }

    public class CoordinatePointItemViewModel : BaseViewModel
    {
        private int _index;
        private int _row;
        private int _column;
        private string _description = string.Empty;
        private int _xAddress;
        private int _yAddress;
        private double _x;
        private double _y;
        private bool _isGenerated;

        public int Index
        {
            get => _index;
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Row
        {
            get => _row;
            set
            {
                if (_row != value)
                {
                    _row = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Column
        {
            get => _column;
            set
            {
                if (_column != value)
                {
                    _column = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        public int XAddress
        {
            get => _xAddress;
            set
            {
                if (_xAddress != value)
                {
                    _xAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public int YAddress
        {
            get => _yAddress;
            set
            {
                if (_yAddress != value)
                {
                    _yAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public double X
        {
            get => _x;
            set
            {
                if (Math.Abs(_x - value) > 0.000001d)
                {
                    _x = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Y
        {
            get => _y;
            set
            {
                if (Math.Abs(_y - value) > 0.000001d)
                {
                    _y = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsGenerated
        {
            get => _isGenerated;
            set
            {
                if (_isGenerated != value)
                {
                    _isGenerated = value;
                    OnPropertyChanged();
                }
            }
        }
    }

    public class ZCoordinateProfileViewModel : BaseViewModel
    {
        private static readonly string[] DefaultZDescriptions =
        {
            "待机位置",
            "占空",
            "摇匀_采血管取放位置",
            "摇匀_顶空瓶位置",
            "采血管料盘_采血管取放位置",
            "顶空瓶料盘_盖子取放位置",
            "顶空瓶料盘_顶空瓶取放位置",
            "顶空瓶盖子暂放台_盖子取放位置",
            "扫码_采血管取放位置",
            "顶空瓶合盖_顶空瓶位置",
            "采血管开合盖_采血管位置",
            "天平_顶空瓶取放位置",
            "天平_采血管取放位置",
            "枪头_取料位置",
            "枪头_丢弃位置",
            "天平_顶空打液位置",
            "顶空进样器_取放料位置",
            "顶空瓶合盖_顶空盖子放置位置"
        };

        private readonly Lx5vPlc _plc;
        private readonly SemaphoreSlim _plcLock;
        private readonly Action<string> _statusCallback;

        private string _name;
        private int _pointCount;
        private int _zStartAddress;
        private int _registerStridePerPoint;
        private int _currentZAddress;
        private double _baseZ;
        private double _stepZ;
        private double _scale;
        private bool _isBusy;
        private string _profileStatusMessage = "未执行操作。";
        private ZCoordinatePointItemViewModel? _selectedPoint;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public int PointCount
        {
            get => _pointCount;
            set
            {
                if (_pointCount != value)
                {
                    _pointCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPoints));
                }
            }
        }

        public int ZStartAddress
        {
            get => _zStartAddress;
            set
            {
                if (_zStartAddress != value)
                {
                    _zStartAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public int RegisterStridePerPoint
        {
            get => _registerStridePerPoint;
            set
            {
                if (_registerStridePerPoint != value)
                {
                    _registerStridePerPoint = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CurrentZAddress
        {
            get => _currentZAddress;
            set
            {
                if (_currentZAddress != value)
                {
                    _currentZAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public double BaseZ
        {
            get => _baseZ;
            set
            {
                if (Math.Abs(_baseZ - value) > 0.000001d)
                {
                    _baseZ = value;
                    OnPropertyChanged();
                }
            }
        }

        public double StepZ
        {
            get => _stepZ;
            set
            {
                if (Math.Abs(_stepZ - value) > 0.000001d)
                {
                    _stepZ = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Scale
        {
            get => _scale;
            set
            {
                if (Math.Abs(_scale - value) > 0.000001d)
                {
                    _scale = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ProfileStatusMessage
        {
            get => _profileStatusMessage;
            set
            {
                if (_profileStatusMessage != value)
                {
                    _profileStatusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public int TotalPoints => Points.Count;

        public ObservableCollection<ZCoordinatePointItemViewModel> Points { get; } = new();

        public ZCoordinatePointItemViewModel? SelectedPoint
        {
            get => _selectedPoint;
            set
            {
                if (!ReferenceEquals(_selectedPoint, value))
                {
                    _selectedPoint = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand BuildGridCommand { get; }
        public ICommand ReadCurrentToBaseCommand { get; }
        public ICommand ReadPointsFromPlcCommand { get; }
        public ICommand WriteAllPointsToPlcCommand { get; }
        public ICommand WriteSelectedPointToPlcCommand { get; }
        public ICommand SetBaseFromSelectedPointCommand { get; }

        public ZCoordinateProfileViewModel(
            string name,
            Lx5vPlc plc,
            SemaphoreSlim plcLock,
            Action<string> statusCallback)
        {
            _name = name;
            _plc = plc;
            _plcLock = plcLock;
            _statusCallback = statusCallback;

            PointCount = 18;
            ZStartAddress = 5900;
            RegisterStridePerPoint = 2;
            CurrentZAddress = 5900;
            BaseZ = 0;
            StepZ = 0;
            Scale = 100;

            BuildGridCommand = new RelayCommand(_ => BuildGrid(), _ => !IsBusy);
            ReadCurrentToBaseCommand = new RelayCommand(_ => _ = ReadCurrentToBaseAsync(), _ => !IsBusy);
            ReadPointsFromPlcCommand = new RelayCommand(_ => _ = ReadPointsFromPlcAsync(), _ => !IsBusy);
            WriteAllPointsToPlcCommand = new RelayCommand(_ => _ = WriteAllPointsToPlcAsync(), _ => !IsBusy && Points.Count > 0);
            WriteSelectedPointToPlcCommand = new RelayCommand(_ => _ = WriteSelectedPointToPlcAsync(), _ => !IsBusy && SelectedPoint != null);
            SetBaseFromSelectedPointCommand = new RelayCommand(_ => SetBaseFromSelectedPoint(), _ => !IsBusy && SelectedPoint != null);

            BuildGrid();
        }

        public void ApplySettings(ZCoordinateProfileSettings settings)
        {
            ZCoordinateProfileSettings safeSettings = settings ?? new ZCoordinateProfileSettings();
            bool hasNewAddressSettings = safeSettings.ZStartAddress > 0;
            int legacyPointCount = Math.Max(0, safeSettings.Rows) * Math.Max(0, safeSettings.Columns);

            PointCount = Math.Max(
                1,
                safeSettings.PointCount > 0
                    ? safeSettings.PointCount
                    : legacyPointCount);
            RegisterStridePerPoint = hasNewAddressSettings
                ? Math.Max(2, safeSettings.RegisterStridePerPoint)
                : 2;
            ZStartAddress = hasNewAddressSettings
                ? Math.Max(0, safeSettings.ZStartAddress)
                : ZStartAddress;
            CurrentZAddress = hasNewAddressSettings
                ? Math.Max(0, safeSettings.CurrentZAddress)
                : CurrentZAddress;
            BaseZ = safeSettings.BaseZ;
            StepZ = safeSettings.StepZ;
            Scale = safeSettings.Scale <= 0 ? 100 : safeSettings.Scale;

            if (safeSettings.Points is { Count: > 0 })
            {
                LoadPointsFromSettings(safeSettings.Points);
                PointCount = Points.Count;
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已从配置加载 {Points.Count} 个独立点位。";
            }
            else
            {
                BuildGrid();
            }
        }

        public ZCoordinateProfileSettings ExportSettings()
        {
            return new ZCoordinateProfileSettings
            {
                PointCount = PointCount,
                ZStartAddress = ZStartAddress,
                RegisterStridePerPoint = RegisterStridePerPoint,
                CurrentZAddress = CurrentZAddress,
                BaseZ = BaseZ,
                StepZ = StepZ,
                Scale = Scale,
                Points = Points.Select(point => new ZCoordinatePointSetting
                {
                    Index = point.Index,
                    Description = point.Description,
                    ZAddress = point.ZAddress,
                    Z = point.Z
                }).ToList(),

                // DMSJ：兼容历史字段，保留旧版行列语义。
                Rows = 1,
                Columns = PointCount
            };
        }

        private async Task ExecuteBusyAsync(Func<Task> action, string successMessage)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                ValidateProfileSettings();
                await action();
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} {successMessage}";
                _statusCallback($"{Name}: {successMessage}");
            }
            catch (Exception ex)
            {
                ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 操作失败: {ex.Message}";
                _statusCallback($"{Name}: 操作失败 - {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ValidateProfileSettings()
        {
            if (!CommunicationManager.Is485Open)
            {
                throw new InvalidOperationException("RS485未连接，请先连接通讯。");
            }

            if (PointCount <= 0)
            {
                throw new InvalidOperationException("点位总数必须大于0。");
            }

            if (RegisterStridePerPoint < 2)
            {
                throw new InvalidOperationException("每点寄存器步长至少为2（每轴占低位/高位两个寄存器）。");
            }

            if (Scale <= 0)
            {
                throw new InvalidOperationException("比例系数必须大于0。");
            }

            if (ZStartAddress < 0)
            {
                throw new InvalidOperationException("起始地址不能为负数。");
            }
        }

        private static ushort EnsureUShortAddress(int value, string fieldName)
        {
            if (value < 0 || value > ushort.MaxValue)
            {
                throw new InvalidOperationException($"{fieldName}超出PLC地址范围(0-65535): {value}");
            }

            return (ushort)value;
        }

        private void BuildGrid()
        {
            int pointCount = Math.Max(1, PointCount);
            int stride = Math.Max(2, RegisterStridePerPoint);

            Points.Clear();

            for (int index = 1; index <= pointCount; index++)
            {
                int offsetIndex = index - 1;
                int zAddress = ZStartAddress + offsetIndex * stride;

                Points.Add(new ZCoordinatePointItemViewModel
                {
                    Index = index,
                    Description = GetDefaultDescription(offsetIndex),
                    ZAddress = zAddress
                });
            }

            OnPropertyChanged(nameof(TotalPoints));
            ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已重建点位列表，共 {Points.Count} 点。";
        }

        private void LoadPointsFromSettings(IReadOnlyList<ZCoordinatePointSetting> pointSettings)
        {
            Points.Clear();

            int fallbackIndex = 1;
            foreach (ZCoordinatePointSetting item in pointSettings.OrderBy(p => p.Index <= 0 ? int.MaxValue : p.Index))
            {
                int index = item.Index > 0 ? item.Index : fallbackIndex;

                Points.Add(new ZCoordinatePointItemViewModel
                {
                    Index = index,
                    Description = string.IsNullOrWhiteSpace(item.Description) ? GetDefaultDescription(index - 1) : item.Description,
                    ZAddress = item.ZAddress,
                    Z = item.Z
                });

                fallbackIndex++;
            }

            OnPropertyChanged(nameof(TotalPoints));
        }

        private async Task ReadCurrentToBaseAsync()
        {
            await ExecuteBusyAsync(async () =>
            {
                ushort zAddr = EnsureUShortAddress(CurrentZAddress, "当前Z低位地址");
                BaseZ = FromInt32(await ReadInt32AtAddressAsync(zAddr));
            }, "已读取当前坐标并更新起点。");
        }

        private async Task ReadPointsFromPlcAsync()
        {
            await ExecuteBusyAsync(async () =>
            {
                foreach (ZCoordinatePointItemViewModel point in Points)
                {
                    EnsureAxisAddressRange(point.ZAddress, "Z轴地址");
                    ushort zAddr = EnsureUShortAddress(point.ZAddress, "Z低位地址");
                    point.Z = FromInt32(await ReadInt32AtAddressAsync(zAddr));
                }
            }, $"已从PLC读取 {Points.Count} 个点位。");
        }

        private async Task WriteAllPointsToPlcAsync()
        {
            await ExecuteBusyAsync(async () =>
            {
                foreach (ZCoordinatePointItemViewModel point in Points)
                {
                    await WritePointAsync(point);
                }
            }, $"已写入PLC {Points.Count} 个点位。");
        }

        private async Task WriteSelectedPointToPlcAsync()
        {
            if (SelectedPoint == null)
            {
                return;
            }

            await ExecuteBusyAsync(
                async () => await WritePointAsync(SelectedPoint),
                $"已写入选中点 P{SelectedPoint.Index:000}。");
        }

        private void SetBaseFromSelectedPoint()
        {
            if (SelectedPoint == null)
            {
                return;
            }

            BaseZ = SelectedPoint.Z;
            ProfileStatusMessage = $"{DateTime.Now:HH:mm:ss} 已将 P{SelectedPoint.Index:000} 设为起点。";
            _statusCallback($"{Name}: 已将选中点设为起点。");
        }

        private async Task WritePointAsync(ZCoordinatePointItemViewModel point)
        {
            EnsureAxisAddressRange(point.ZAddress, "Z轴地址");
            ushort zAddr = EnsureUShortAddress(point.ZAddress, "Z低位地址");
            int zRaw = ToInt32(point.Z);
            await WriteInt32AtAddressAsync(zAddr, zRaw);
        }

        private static void EnsureAxisAddressRange(int lowAddress, string fieldName)
        {
            if (lowAddress < 0 || lowAddress > ushort.MaxValue - 1)
            {
                throw new InvalidOperationException($"{fieldName}越界，双寄存器写入要求低位地址范围为 0-65534: {lowAddress}");
            }
        }

        private static string GetDefaultDescription(int zeroBasedIndex)
        {
            return zeroBasedIndex >= 0 && zeroBasedIndex < DefaultZDescriptions.Length
                ? DefaultZDescriptions[zeroBasedIndex]
                : $"未命名点位{zeroBasedIndex + 1}";
        }

        private double FromInt32(int raw)
        {
            return raw / Scale;
        }

        private int ToInt32(double value)
        {
            double scaled = Math.Round(value * Scale, MidpointRounding.AwayFromZero);
            if (scaled < int.MinValue || scaled > int.MaxValue)
            {
                string text = value.ToString("F3", CultureInfo.InvariantCulture);
                throw new InvalidOperationException($"坐标值超出32位寄存器范围: {text}");
            }

            return (int)scaled;
        }

        private static int ComposeInt32(ushort lowWord, ushort highWord)
        {
            uint raw = ((uint)highWord << 16) | lowWord;
            return unchecked((int)raw);
        }

        private static void SplitInt32(int value, out ushort lowWord, out ushort highWord)
        {
            unchecked
            {
                uint raw = (uint)value;
                lowWord = (ushort)(raw & 0xFFFF);
                highWord = (ushort)((raw >> 16) & 0xFFFF);
            }
        }

        private async Task<int> ReadInt32AtAddressAsync(ushort lowAddress)
        {
            ushort[] regs = await ReadHoldingRegistersAsync(lowAddress, 2);
            if (regs.Length < 2)
            {
                throw new InvalidOperationException($"读取地址 {lowAddress} 失败，返回长度不足2。");
            }

            return ComposeInt32(regs[0], regs[1]);
        }

        private async Task WriteInt32AtAddressAsync(ushort lowAddress, int value)
        {
            SplitInt32(value, out ushort lowWord, out ushort highWord);

            await _plcLock.WaitAsync();
            try
            {
                var writeLow = await _plc.TryWriteSingleRegisterAsync(lowAddress, lowWord);
                if (!writeLow.Success)
                {
                    throw new InvalidOperationException(writeLow.Error);
                }

                var writeHigh = await _plc.TryWriteSingleRegisterAsync((ushort)(lowAddress + 1), highWord);
                if (!writeHigh.Success)
                {
                    throw new InvalidOperationException(writeHigh.Error);
                }
            }
            finally
            {
                _plcLock.Release();
            }
        }

        private async Task<ushort[]> ReadHoldingRegistersAsync(ushort address, ushort length)
        {
            await _plcLock.WaitAsync();
            try
            {
                var read = await _plc.TryReadHoldingRegistersAsync(address, length);
                if (!read.Success)
                {
                    throw new InvalidOperationException(read.Error);
                }

                return read.Values;
            }
            finally
            {
                _plcLock.Release();
            }
        }
    }

    public class ZCoordinatePointItemViewModel : BaseViewModel
    {
        private int _index;
        private string _description = string.Empty;
        private int _zAddress;
        private double _z;

        public int Index
        {
            get => _index;
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ZAddress
        {
            get => _zAddress;
            set
            {
                if (_zAddress != value)
                {
                    _zAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Z
        {
            get => _z;
            set
            {
                if (Math.Abs(_z - value) > 0.000001d)
                {
                    _z = value;
                    OnPropertyChanged();
                }
            }
        }

    }

    public class CoordinateDebugConfig
    {
        public CoordinateProfileSettings BloodTube { get; set; } = new CoordinateProfileSettings
        {
            Rows = 5,
            Columns = 10,
            XStartAddress = 5100,
            YStartAddress = 5200,
            RegisterStridePerPoint = 2,
            CurrentXAddress = 5100,
            CurrentYAddress = 5200,
            BaseX = 0,
            BaseY = 0,
            StepX = 0,
            StepY = 0,
            Scale = 100
        };

        public CoordinateProfileSettings HeadspaceVial { get; set; } = new CoordinateProfileSettings
        {
            Rows = 10,
            Columns = 10,
            XStartAddress = 5300,
            YStartAddress = 5500,
            RegisterStridePerPoint = 2,
            CurrentXAddress = 5300,
            CurrentYAddress = 5500,
            BaseX = 0,
            BaseY = 0,
            StepX = 0,
            StepY = 0,
            Scale = 100
        };

        public CoordinateProfileSettings OtherPosition { get; set; } = new CoordinateProfileSettings
        {
            Rows = 5,
            Columns = 10,
            XStartAddress = 5700,
            YStartAddress = 5800,
            RegisterStridePerPoint = 2,
            CurrentXAddress = 5700,
            CurrentYAddress = 5800,
            BaseX = 0,
            BaseY = 0,
            StepX = 0,
            StepY = 0,
            Scale = 100
        };

        public CoordinateProfileSettings PipetteTip { get; set; } = new CoordinateProfileSettings
        {
            Rows = 5,
            Columns = 10,
            XStartAddress = 6100,
            YStartAddress = 6200,
            RegisterStridePerPoint = 2,
            CurrentXAddress = 6100,
            CurrentYAddress = 6200,
            BaseX = 0,
            BaseY = 0,
            StepX = 0,
            StepY = 0,
            Scale = 100
        };

        public ZCoordinateProfileSettings ZAxis { get; set; } = new ZCoordinateProfileSettings
        {
            PointCount = 18,
            ZStartAddress = 5900,
            RegisterStridePerPoint = 2,
            CurrentZAddress = 5900,
            BaseZ = 0,
            StepZ = 0,
            Scale = 100
        };
    }

    public class CoordinateProfileSettings
    {
        public int Rows { get; set; } = 1;
        public int Columns { get; set; } = 1;
        public int XStartAddress { get; set; }
        public int YStartAddress { get; set; }
        public int RegisterStridePerPoint { get; set; } = 2;
        public int CurrentXAddress { get; set; }
        public int CurrentYAddress { get; set; }
        public double BaseX { get; set; }
        public double BaseY { get; set; }
        public double StepX { get; set; }
        public double StepY { get; set; }
        public double Scale { get; set; } = 100;
        public List<CoordinatePointSetting> Points { get; set; } = new();

        // DMSJ：历史字段，保留用于兼容旧版配置文件（已弃用）。
        public int TableStartAddress { get; set; }
        public int CurrentZAddress { get; set; }
        public double BaseZ { get; set; }
        public double StepZ { get; set; }
    }

    public class ZCoordinateProfileSettings
    {
        public int PointCount { get; set; } = 1;
        public int ZStartAddress { get; set; }
        public int RegisterStridePerPoint { get; set; } = 2;
        public int CurrentZAddress { get; set; }
        public double BaseZ { get; set; }
        public double StepZ { get; set; }
        public double Scale { get; set; } = 100;
        public List<ZCoordinatePointSetting> Points { get; set; } = new();

        // DMSJ：历史字段，保留用于兼容旧版配置文件（已弃用）。
        public int Rows { get; set; } = 1;
        public int Columns { get; set; } = 1;
    }

    public class CoordinatePointSetting
    {
        public int Index { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public string Description { get; set; } = string.Empty;
        public int XAddress { get; set; }
        public int YAddress { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class ZCoordinatePointSetting
    {
        public int Index { get; set; }
        public string Description { get; set; } = string.Empty;
        public int ZAddress { get; set; }
        public double Z { get; set; }
    }
}
