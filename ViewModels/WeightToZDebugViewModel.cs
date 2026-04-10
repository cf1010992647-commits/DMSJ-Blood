using Blood_Alcohol.Communication.Serial;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Blood_Alcohol.ViewModels
{
    public class WeightToZDebugViewModel : BaseViewModel
    {
        private const string WeightToZConfigFileName = "WeightToZCalibrationConfig.json";
        private const string CoordinateConfigFileName = "CoordinateDebugConfig.json";

        // DMSJ：M3(Z轴)手动定位低位地址和触发位，保持和 AxisDebugViewModel 一致。
        private const ushort ZManualPositionLowAddress = 1216;
        private const ushort ZManualLocateTriggerCoilAddress = 1219;

        private readonly ConfigService<WeightToZCalibrationConfig> _weightConfigService;
        private readonly ConfigService<CoordinateDebugConfig> _coordinateConfigService;
        private readonly Lx5vPlc _plc;
        private readonly SemaphoreSlim _plcLock;

        private string _statusMessage = "重量->Z 坐标调试已加载。";
        private int _zAddress = 5900;
        private double _zScale = 100;
        private double _currentWeight;
        private double _queryWeight;
        private double _currentZ;
        private double _zPerWeight;
        private bool _hasCoefficient;
        private string _formulaText = "请先读取当前Z，然后根据当前重量计算系数。";
        private double _predictedZ;
        private int _predictedRawValue;

        public WeightToZDebugViewModel()
        {
            _weightConfigService = new ConfigService<WeightToZCalibrationConfig>(WeightToZConfigFileName);
            _coordinateConfigService = new ConfigService<CoordinateDebugConfig>(CoordinateConfigFileName);
            _plc = CommunicationManager.Plc;
            _plcLock = CommunicationManager.PlcAccessLock;

            ReadCurrentZCommand = new RelayCommand(_ => _ = ReadCurrentZAsync());
            ComputeCoefficientCommand = new RelayCommand(_ => ComputeCoefficientFromCurrent(), _ => CurrentWeight > 0);
            CalculateCommand = new RelayCommand(_ => UpdatePrediction(forceMessage: true), _ => HasCoefficient);
            TestMoveCommand = new RelayCommand(_ => _ = TestMoveAsync(), _ => HasCoefficient);
            SaveConfigCommand = new RelayCommand(_ => SaveConfig());
            LoadConfigCommand = new RelayCommand(_ => LoadConfig());

            ReloadAddressFromCoordinateConfig();
            LoadConfig();
        }

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

        public int ZAddress
        {
            get => _zAddress;
            private set
            {
                if (_zAddress != value)
                {
                    _zAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ZScale
        {
            get => _zScale;
            private set
            {
                if (Math.Abs(_zScale - value) > 0.000001d)
                {
                    _zScale = value;
                    OnPropertyChanged();
                }
            }
        }

        public double CurrentWeight
        {
            get => _currentWeight;
            set
            {
                if (Math.Abs(_currentWeight - value) > 0.000001d)
                {
                    _currentWeight = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public double QueryWeight
        {
            get => _queryWeight;
            set
            {
                if (Math.Abs(_queryWeight - value) > 0.000001d)
                {
                    _queryWeight = value;
                    OnPropertyChanged();
                    UpdatePrediction(forceMessage: false);
                }
            }
        }

        public double CurrentZ
        {
            get => _currentZ;
            private set
            {
                if (Math.Abs(_currentZ - value) > 0.000001d)
                {
                    _currentZ = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ZPerWeight
        {
            get => _zPerWeight;
            private set
            {
                if (Math.Abs(_zPerWeight - value) > 0.000001d)
                {
                    _zPerWeight = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasCoefficient
        {
            get => _hasCoefficient;
            private set
            {
                if (_hasCoefficient != value)
                {
                    _hasCoefficient = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string FormulaText
        {
            get => _formulaText;
            private set
            {
                if (_formulaText != value)
                {
                    _formulaText = value;
                    OnPropertyChanged();
                }
            }
        }

        public double PredictedZ
        {
            get => _predictedZ;
            private set
            {
                if (Math.Abs(_predictedZ - value) > 0.000001d)
                {
                    _predictedZ = value;
                    OnPropertyChanged();
                }
            }
        }

        public int PredictedRawValue
        {
            get => _predictedRawValue;
            private set
            {
                if (_predictedRawValue != value)
                {
                    _predictedRawValue = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand ReadCurrentZCommand { get; }
        public ICommand ComputeCoefficientCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand TestMoveCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand LoadConfigCommand { get; }

        private void ReloadAddressFromCoordinateConfig()
        {
            try
            {
                CoordinateDebugConfig cfg = _coordinateConfigService.Load() ?? new CoordinateDebugConfig();
                ZCoordinateProfileSettings zCfg = cfg.ZAxis ?? new ZCoordinateProfileSettings();

                ZAddress = zCfg.CurrentZAddress > 0 ? zCfg.CurrentZAddress : 5900;
                ZScale = zCfg.Scale > 0 ? zCfg.Scale : 100;
            }
            catch
            {
                ZAddress = 5900;
                ZScale = 100;
            }
        }

        private async Task ReadCurrentZAsync()
        {
            try
            {
                ReloadAddressFromCoordinateConfig();
                if (!CommunicationManager.Is485Open)
                {
                    StatusMessage = $"{DateTime.Now:HH:mm:ss} 读取失败：RS485 未连接。";
                    return;
                }

                ushort address = EnsureLowAddress(ZAddress, "Z地址");
                int raw = await ReadInt32AtAddressAsync(address);
                CurrentZ = FromPlcRaw(raw);
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 读取成功：D{ZAddress}/D{ZAddress + 1} -> Z={CurrentZ:F3} mm";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 读取当前Z失败：{ex.Message}";
            }
        }

        private void ComputeCoefficientFromCurrent()
        {
            if (CurrentWeight <= 0)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 当前重量必须大于 0。";
                return;
            }

            ZPerWeight = CurrentZ / CurrentWeight;
            HasCoefficient = true;
            FormulaText = $"Z(mm) = 重量 * {ZPerWeight:F6}";
            UpdatePrediction(forceMessage: false);
            StatusMessage = $"{DateTime.Now:HH:mm:ss} 系数已更新：重量={CurrentWeight:F3}，当前Z={CurrentZ:F3}";
        }

        private void UpdatePrediction(bool forceMessage)
        {
            if (!HasCoefficient)
            {
                PredictedZ = 0;
                PredictedRawValue = 0;
                if (forceMessage)
                {
                    StatusMessage = $"{DateTime.Now:HH:mm:ss} 请先计算系数。";
                }

                return;
            }

            PredictedZ = QueryWeight * ZPerWeight;
            PredictedRawValue = ToPlcRaw(PredictedZ);
            if (forceMessage)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 预测完成：重量 {QueryWeight:F3} -> Z {PredictedZ:F3}";
            }
        }

        private async Task TestMoveAsync()
        {
            try
            {
                if (!HasCoefficient)
                {
                    StatusMessage = $"{DateTime.Now:HH:mm:ss} 测试失败：请先计算系数。";
                    return;
                }

                if (!CommunicationManager.Is485Open)
                {
                    StatusMessage = $"{DateTime.Now:HH:mm:ss} 测试失败：RS485 未连接。";
                    return;
                }

                await WriteInt32AtAddressAsync(ZManualPositionLowAddress, PredictedRawValue);
                await WriteCoilWithLockAsync(ZManualLocateTriggerCoilAddress, true);
                await Task.Delay(100);
                await WriteCoilWithLockAsync(ZManualLocateTriggerCoilAddress, false);

                StatusMessage = $"{DateTime.Now:HH:mm:ss} 已触发Z轴测试运动，目标Z={PredictedZ:F3} (raw={PredictedRawValue})";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 测试运动失败：{ex.Message}";
            }
        }

        private void SaveConfig()
        {
            try
            {
                WeightToZCalibrationConfig cfg = new()
                {
                    CurrentWeight = CurrentWeight,
                    QueryWeight = QueryWeight,
                    CurrentZ = CurrentZ,
                    ZPerWeight = ZPerWeight,
                    HasCoefficient = HasCoefficient
                };

                _weightConfigService.Save(cfg);
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 系数配置已保存。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 保存失败：{ex.Message}";
            }
        }

        private void LoadConfig()
        {
            try
            {
                WeightToZCalibrationConfig cfg = _weightConfigService.Load() ?? new WeightToZCalibrationConfig();
                CurrentWeight = cfg.CurrentWeight;
                QueryWeight = cfg.QueryWeight;
                CurrentZ = cfg.CurrentZ;

                if (cfg.HasCoefficient && Math.Abs(cfg.ZPerWeight) > 0.0000001d)
                {
                    ZPerWeight = cfg.ZPerWeight;
                    HasCoefficient = true;
                    FormulaText = $"Z(mm) = 重量 * {ZPerWeight:F6}";
                    UpdatePrediction(forceMessage: false);
                }
                else
                {
                    HasCoefficient = false;
                    ZPerWeight = 0;
                    FormulaText = "请先读取当前Z，然后根据当前重量计算系数。";
                    PredictedZ = 0;
                    PredictedRawValue = 0;
                }

                StatusMessage = $"{DateTime.Now:HH:mm:ss} 系数配置已加载。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 加载失败：{ex.Message}";
            }
        }

        private double FromPlcRaw(int raw)
        {
            if (Math.Abs(ZScale) < 0.0000001d)
            {
                throw new InvalidOperationException("Z比例不能为0。");
            }

            return raw / ZScale;
        }

        private int ToPlcRaw(double zValue)
        {
            if (Math.Abs(ZScale) < 0.0000001d)
            {
                throw new InvalidOperationException("Z比例不能为0。");
            }

            double scaled = Math.Round(zValue * ZScale, MidpointRounding.AwayFromZero);
            if (scaled < int.MinValue || scaled > int.MaxValue)
            {
                throw new InvalidOperationException($"Z值超出32位范围：{zValue:F6}");
            }

            return (int)scaled;
        }

        private static ushort EnsureLowAddress(int address, string fieldName)
        {
            if (address < 0 || address > ushort.MaxValue - 1)
            {
                throw new InvalidOperationException($"{fieldName}超出范围(0-65534)：{address}");
            }

            return (ushort)address;
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
            await _plcLock.WaitAsync();
            try
            {
                var read = await _plc.TryReadHoldingRegistersAsync(lowAddress, 2);
                if (!read.Success)
                {
                    throw new InvalidOperationException(read.Error);
                }

                ushort[] regs = read.Values;
                if (regs.Length < 2)
                {
                    throw new InvalidOperationException("PLC 返回寄存器数量不足。");
                }

                return ComposeInt32(regs[0], regs[1]);
            }
            finally
            {
                _plcLock.Release();
            }
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

        private async Task WriteCoilWithLockAsync(ushort address, bool value)
        {
            await _plcLock.WaitAsync();
            try
            {
                var write = await _plc.TryWriteSingleCoilAsync(address, value);
                if (!write.Success)
                {
                    throw new InvalidOperationException(write.Error);
                }
            }
            finally
            {
                _plcLock.Release();
            }
        }
    }

}
