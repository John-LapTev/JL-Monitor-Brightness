using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using JL_Monitor_Brightness.Models;
using JL_Monitor_Brightness.Services;

namespace JL_Monitor_Brightness
{
    public partial class BrightnessOverlay : Window
    {
        private readonly DispatcherTimer _hideTimer;
        private readonly MonitorService _monitorService;
        private PhysicalMonitorInfo _currentMonitor;
        private Settings _settings;
        private bool _isUpdatingSlider = false;

        public BrightnessOverlay(MonitorService monitorService, Settings settings)
        {
            InitializeComponent();
            
            _monitorService = monitorService;
            _settings = settings;
            
            // Настройка таймера для автоматического скрытия
            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_settings.OverlayTimeout)
            };
            _hideTimer.Tick += HideTimer_Tick;
            
            // Применить настройки
            ApplySettings();
            
            // Обработчики событий
            this.MouseEnter += BrightnessOverlay_MouseEnter;
            this.MouseLeave += BrightnessOverlay_MouseLeave;
            this.Loaded += BrightnessOverlay_Loaded;
            this.KeyDown += BrightnessOverlay_KeyDown;
        }

        private void ApplySettings()
        {
            this.Opacity = _settings.OverlayOpacity;
            
            // Применяем цветовую тему
            var primaryBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.ThemeColor));
            this.Resources["PrimaryBrush"] = primaryBrush;
        }

        private void BrightnessOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            // Позиционирование окна в центре экрана
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = screenHeight - this.Height - 100; // Размещаем ближе к нижней части экрана
            
            // Анимация появления
            this.Opacity = 0;
            var animation = new DoubleAnimation(0, _settings.OverlayOpacity, TimeSpan.FromMilliseconds(250));
            this.BeginAnimation(OpacityProperty, animation);
            
            // Запускаем таймер скрытия
            StartHideTimer();
        }

        private void BrightnessOverlay_MouseEnter(object sender, MouseEventArgs e)
        {
            _hideTimer.Stop();
        }

        private void BrightnessOverlay_MouseLeave(object sender, MouseEventArgs e)
        {
            StartHideTimer();
        }

        private void BrightnessOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
            else if (e.Key == Key.Left || e.Key == Key.Down)
            {
                DecreaseBrightness();
                e.Handled = true;
            }
            else if (e.Key == Key.Right || e.Key == Key.Up)
            {
                IncreaseBrightness();
                e.Handled = true;
            }
        }

        private void StartHideTimer()
        {
            _hideTimer.Start();
        }

        private void HideTimer_Tick(object sender, EventArgs e)
        {
            _hideTimer.Stop();
            
            // Анимация скрытия
            var animation = new DoubleAnimation(_settings.OverlayOpacity, 0, TimeSpan.FromMilliseconds(250));
            animation.Completed += (s, args) => Hide();
            this.BeginAnimation(OpacityProperty, animation);
        }

        public void SetMonitor(PhysicalMonitorInfo monitor)
        {
            _currentMonitor = monitor;
            MonitorNameTextBlock.Text = monitor.Description;
            
            // Обновляем слайдер без вызова события изменения значения
            _isUpdatingSlider = true;
            BrightnessSlider.Value = monitor.BrightnessPercentage;
            _isUpdatingSlider = false;
            
            // Обновляем текст процента
            UpdateBrightnessText(monitor.BrightnessPercentage);
        }

        private void UpdateBrightnessText(double percentage)
        {
            if (_settings.ShowPercentage)
            {
                BrightnessTextBlock.Text = $"{Math.Round(percentage)}%";
            }
            else
            {
                BrightnessTextBlock.Text = string.Empty;
            }
        }

        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingSlider || _currentMonitor == null) return;
            
            var percentage = (int)Math.Round(e.NewValue);
            UpdateBrightnessText(percentage);
            
            // Пересчитываем значение яркости в соответствии с диапазоном монитора
            uint brightness = (uint)(_currentMonitor.MinBrightness + 
                (percentage / 100.0) * (_currentMonitor.MaxBrightness - _currentMonitor.MinBrightness));
            
            _monitorService.SetBrightness(_currentMonitor, brightness);
            
            // Сбрасываем таймер скрытия
            if (_hideTimer.IsEnabled)
            {
                _hideTimer.Stop();
                _hideTimer.Start();
            }
        }

        private void DecreaseButton_Click(object sender, RoutedEventArgs e)
        {
            DecreaseBrightness();
        }

        private void IncreaseButton_Click(object sender, RoutedEventArgs e)
        {
            IncreaseBrightness();
        }

        private void DecreaseBrightness()
        {
            if (_currentMonitor != null)
            {
                _monitorService.DecreaseBrightness(_currentMonitor, _settings.BrightnessStep);
                
                // Обновляем интерфейс
                _isUpdatingSlider = true;
                BrightnessSlider.Value = _currentMonitor.BrightnessPercentage;
                _isUpdatingSlider = false;
                
                UpdateBrightnessText(_currentMonitor.BrightnessPercentage);
                
                // Сбрасываем таймер скрытия
                if (_hideTimer.IsEnabled)
                {
                    _hideTimer.Stop();
                    _hideTimer.Start();
                }
            }
        }

        private void IncreaseBrightness()
        {
            if (_currentMonitor != null)
            {
                _monitorService.IncreaseBrightness(_currentMonitor, _settings.BrightnessStep);
                
                // Обновляем интерфейс
                _isUpdatingSlider = true;
                BrightnessSlider.Value = _currentMonitor.BrightnessPercentage;
                _isUpdatingSlider = false;
                
                UpdateBrightnessText(_currentMonitor.BrightnessPercentage);
                
                // Сбрасываем таймер скрытия
                if (_hideTimer.IsEnabled)
                {
                    _hideTimer.Stop();
                    _hideTimer.Start();
                }
            }
        }

        public void ShowOverlay()
        {
            // Принудительно размещаем окно поверх всех окон
            this.Topmost = true;
            
            // Показываем окно и делаем его активным
            this.Show();
            this.Activate();
            this.Focus();
            
            // Обновляем расположение
            WindowState = WindowState.Normal;
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = screenHeight - this.Height - 100;
            
            // Анимация появления с нуля
            this.Opacity = 0;
            var animation = new DoubleAnimation(0, _settings.OverlayOpacity, TimeSpan.FromMilliseconds(250));
            this.BeginAnimation(OpacityProperty, animation);
            
            // Запускаем таймер скрытия
            StartHideTimer();
        }
    }
}