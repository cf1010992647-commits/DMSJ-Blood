using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using System;
using System.Windows.Input;

namespace Blood_Alcohol.ViewModels
{
    /// <summary>
    /// DMSJ：参数配置页面ViewModel。
    /// 用于配置并保存加热箱温度、定量环温度、传输线温度、摇匀时长。
    /// </summary>
    public class ParameterConfigViewModel : BaseViewModel
    {
        private const string ConfigFileName = "ProcessParameterConfig.json";
        private readonly ConfigService<ProcessParameterConfig> _configService = new(ConfigFileName);

        private double _heatingBoxTemperature = 60.0;
        private double _quantitativeLoopTemperature = 80.0;
        private double _transferLineTemperature = 120.0;
        private int _shakeDurationSeconds = 10;
        private string _statusMessage = "参数配置已加载。";

        public double HeatingBoxTemperature
        {
            get => _heatingBoxTemperature;
            set
            {
                if (Math.Abs(_heatingBoxTemperature - value) > 0.000001d)
                {
                    _heatingBoxTemperature = value;
                    OnPropertyChanged();
                }
            }
        }

        public double QuantitativeLoopTemperature
        {
            get => _quantitativeLoopTemperature;
            set
            {
                if (Math.Abs(_quantitativeLoopTemperature - value) > 0.000001d)
                {
                    _quantitativeLoopTemperature = value;
                    OnPropertyChanged();
                }
            }
        }

        public double TransferLineTemperature
        {
            get => _transferLineTemperature;
            set
            {
                if (Math.Abs(_transferLineTemperature - value) > 0.000001d)
                {
                    _transferLineTemperature = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ShakeDurationSeconds
        {
            get => _shakeDurationSeconds;
            set
            {
                if (_shakeDurationSeconds != value)
                {
                    _shakeDurationSeconds = value;
                    OnPropertyChanged();
                }
            }
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

        public ICommand SaveConfigCommand { get; }
        public ICommand ReloadConfigCommand { get; }
        public ICommand ResetDefaultCommand { get; }

        public ParameterConfigViewModel()
        {
            SaveConfigCommand = new RelayCommand(_ => SaveConfig());
            ReloadConfigCommand = new RelayCommand(_ => LoadConfig());
            ResetDefaultCommand = new RelayCommand(_ => ResetDefault());

            LoadConfig();
        }

        private void LoadConfig()
        {
            try
            {
                ProcessParameterConfig config = _configService.Load() ?? new ProcessParameterConfig();
                HeatingBoxTemperature = config.HeatingBoxTemperature;
                QuantitativeLoopTemperature = config.QuantitativeLoopTemperature;
                TransferLineTemperature = config.TransferLineTemperature;
                ShakeDurationSeconds = config.ShakeDurationSeconds;
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 参数配置已加载。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 加载失败：{ex.Message}";
            }
        }

        private void SaveConfig()
        {
            try
            {
                ProcessParameterConfig config = new()
                {
                    HeatingBoxTemperature = HeatingBoxTemperature,
                    QuantitativeLoopTemperature = QuantitativeLoopTemperature,
                    TransferLineTemperature = TransferLineTemperature,
                    ShakeDurationSeconds = Math.Max(0, ShakeDurationSeconds)
                };

                _configService.Save(config);
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 参数配置已保存。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{DateTime.Now:HH:mm:ss} 保存失败：{ex.Message}";
            }
        }

        private void ResetDefault()
        {
            HeatingBoxTemperature = 60.0;
            QuantitativeLoopTemperature = 80.0;
            TransferLineTemperature = 120.0;
            ShakeDurationSeconds = 10;
            StatusMessage = $"{DateTime.Now:HH:mm:ss} 已恢复默认值（未保存）。";
        }
    }
}
