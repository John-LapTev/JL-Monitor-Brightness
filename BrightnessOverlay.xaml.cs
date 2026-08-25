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
        private readonly DispatcherTimer _writeTimer;
        private uint? _pendingBrightness;
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

            _writeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            _writeTimer.Tick += WriteTimer_Tick;
            
            // Применить настройки
            ApplySettings();
            
            // Обработчики событий
            this.MouseEnter += BrightnessOverlay_MouseEnter;
            this.MouseLeave += BrightnessOverlay_MouseLeave;
            this.Loaded += BrightnessOverlay_Loaded;
            this.KeyDown += BrightnessOverlay_KeyDown;
        }

        /// <summary>Применяет настройки к оверлею. Публичный, чтобы окно не пересоздавать.</summary>
        public void ApplySettings()
        {
            // ⚠️ Прозрачность окна больше не берётся из настроек: стекло делает
            // системное размытие, а Opacity на всём окне гасило бы и его, и текст.
            // Настройка остаётся в файле для совместимости, но на вид не влияет.
            this.Opacity = 1.0;

            // Акцент кладётся в ресурсы ПРИЛОЖЕНИЯ, а не окна: иначе смена темы
            // не доходит до остальных окон, а в дизайнере DynamicResource даёт null.
            Application.Current.Resources["PrimaryBrush"] = _settings.CreateThemeBrush();

            if (_hideTimer != null)
            {
                _hideTimer.Interval = TimeSpan.FromMilliseconds(_settings.OverlayTimeout);
            }
        }

        private void BrightnessOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            PositionOnActiveScreen();
            PlayRevealAnimation();
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

        /// <summary>
        /// Появление: прозрачность, лёгкий подъём и доводка масштаба.
        /// Кривая KeySpline 0.22,1 0.36,1 — точный перенос cubic-bezier из
        /// дизайн-системы: движение резко стартует и мягко доезжает.
        /// </summary>
        /// <summary>
        /// Возвращает пилюлю в исходное состояние. Без этого анимация появления
        /// отработает ровно один раз за запуск: значения остаются под управлением
        /// прошлого Storyboard.
        /// </summary>
        private void ResetRevealState()
        {
            PillSlide.BeginAnimation(TranslateTransform.YProperty, null);
            PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            PillSlide.Y = 8;
            PillScale.ScaleX = 0.96;
            PillScale.ScaleY = 0.96;
        }

        private void PlayRevealAnimation()
        {
            var spline = new KeySpline(0.22, 1, 0.36, 1);

            this.Opacity = 0;
            var fade = new DoubleAnimationUsingKeyFrames();
            fade.KeyFrames.Add(new SplineDoubleKeyFrame(1.0,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220)), spline));
            this.BeginAnimation(OpacityProperty, fade);

            var slide = new DoubleAnimationUsingKeyFrames();
            slide.KeyFrames.Add(new SplineDoubleKeyFrame(0,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280)), spline));
            PillSlide.BeginAnimation(TranslateTransform.YProperty, slide);

            foreach (var prop in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
            {
                var scale = new DoubleAnimationUsingKeyFrames();
                scale.KeyFrames.Add(new SplineDoubleKeyFrame(1,
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280)), spline));
                PillScale.BeginAnimation(prop, scale);
            }
        }

        private void StartHideTimer()
        {
            _hideTimer.Start();
        }

        private void HideTimer_Tick(object sender, EventArgs e)
        {
            _hideTimer.Stop();
            
            // Уход быстрее появления: 160 мс, лёгкий уезд вниз.
            var fade = new DoubleAnimation(1.0, 0, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (s, args) =>
            {
                Hide();
                ResetRevealState();
            };
            this.BeginAnimation(OpacityProperty, fade);

            var slide = new DoubleAnimation(0, 6, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            PillSlide.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        /// <summary>
        /// Сколько мониторов известно приложению. Ставится снаружи при обновлении
        /// списка: перечислять DDC/CI на каждый показ оверлея слишком дорого.
        /// </summary>
        public int MonitorCount { get; set; } = 1;

        public void SetMonitor(PhysicalMonitorInfo monitor)
        {
            _currentMonitor = monitor;

            // Подпись нужна, только когда есть из чего выбирать: на одном мониторе
            // она сообщает очевидное и занимает место.
            MonitorNameTextBlock.Text = monitor.Description;
            MonitorNameTextBlock.Visibility = MonitorCount > 1
                ? Visibility.Visible
                : Visibility.Collapsed;
            
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

        #region Позиционирование

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

        /*
          ⛔ Здесь был системный акрил (SetWindowCompositionAttribute с
          ACCENT_ENABLE_ACRYLICBLURBEHIND). Убран 25.08.2026.

          Он красит ВЕСЬ HWND — прямоугольник 368×120, — а шторка внутри него
          скруглённая пилюля 320×52. По краям вылезала светло-серая плита, из-за
          которой вместо парящей пилюли выходил прямоугольный блок с пилюлей
          внутри. Скруглить окно под акрилом нельзя: он не знает о форме
          содержимого.

          Вместо размытия — плотная заливка пилюли (GlassSolid) с тенью:
          на 52 пикселях высоты этого хватает, текст читается на любых обоях.
        */

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Окно, которое нельзя активировать. Без этого WPF всё равно перехватывает
            // фокус при показе, даже если не звать Activate().
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            int style = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        /// <summary>
        /// Ставит оверлей на экран, где сейчас курсор, по нижнему краю рабочей области.
        /// Раньше позиция считалась от PrimaryScreenWidth — то есть всегда на главном
        /// мониторе, даже когда регулировали второй, а на разных DPI ещё и уезжала.
        /// </summary>
        private void PositionOnActiveScreen()
        {
            try
            {
                if (!GetCursorPos(out POINT cursor))
                {
                    FallbackPosition();
                    return;
                }

                IntPtr hMonitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);
                var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MONITORINFO)) };

                if (!GetMonitorInfo(hMonitor, ref info))
                {
                    FallbackPosition();
                    return;
                }

                // Win32 отдаёт физические пиксели, WPF расставляет окна в единицах,
                // независимых от устройства — без пересчёта на мониторе со 150%
                // окно уедет ровно в полтора раза.
                var dpi = VisualTreeHelper.GetDpi(this);
                double left = info.rcWork.left / dpi.DpiScaleX;
                double top = info.rcWork.top / dpi.DpiScaleY;
                double width = (info.rcWork.right - info.rcWork.left) / dpi.DpiScaleX;
                double height = (info.rcWork.bottom - info.rcWork.top) / dpi.DpiScaleY;

                this.Left = left + (width - this.Width) / 2;
                // Отступ пропорциональный, а не жёсткие 100 пикселей: на 1080p даст ~118,
                // на 1440p ~156, и панель задач учтена, потому что берётся рабочая область.
                this.Top = top + height - this.Height - height * 0.12;
            }
            catch
            {
                FallbackPosition();
            }
        }

        private void FallbackPosition()
        {
            this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
            this.Top = SystemParameters.PrimaryScreenHeight - this.Height - 100;
        }

        #endregion

        private void WriteTimer_Tick(object sender, EventArgs e)
        {
            _writeTimer.Stop();

            if (_pendingBrightness == null || _currentMonitor == null)
            {
                return;
            }

            uint value = _pendingBrightness.Value;
            _pendingBrightness = null;

            // Результат раньше отбрасывался во всех местах вызова: отказ DDC/CI
            // (монитор выключен, кабель, DDC/CI выключен в меню монитора) выглядел
            // для человека как «нажимаю, и ничего не происходит».
            if (!_monitorService.SetBrightness(_currentMonitor, value))
            {
                BrightnessFailed?.Invoke(this, _currentMonitor);
            }
        }

        /// <summary>Монитор не принял яркость — вызывающий решает, как сообщить.</summary>
        public event EventHandler<PhysicalMonitorInfo> BrightnessFailed;

        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingSlider || _currentMonitor == null) return;
            
            var percentage = (int)Math.Round(e.NewValue);
            UpdateBrightnessText(percentage);
            
            // Пересчитываем значение яркости в соответствии с диапазоном монитора
            uint brightness = (uint)(_currentMonitor.MinBrightness + 
                (percentage / 100.0) * (_currentMonitor.MaxBrightness - _currentMonitor.MinBrightness));
            
            // ⚠️ DDC/CI — синхронный обмен по I2C, 20-80 мс на вызов. Раньше он шёл на
            // КАЖДЫЙ тик перетаскивания прямо в UI-потоке: окно подвисало, а экранное
            // меню монитора отставало на секунды. Теперь пишем не чаще раза в 60 мс,
            // побеждает последнее значение.
            _pendingBrightness = brightness;
            if (!_writeTimer.IsEnabled)
            {
                _writeTimer.Start();
            }

            // Сбрасываем таймер скрытия
            if (_hideTimer.IsEnabled)
            {
                _hideTimer.Stop();
                _hideTimer.Start();
            }
        }

        /// <summary>
        /// Колесо мыши над пилюлей вместо прежних кнопок «плюс» и «минус»:
        /// две кнопки 30×30 занимали треть ширины ради того, что удобнее делать колесом.
        /// </summary>
        private void Overlay_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_currentMonitor == null)
            {
                return;
            }

            if (e.Delta > 0)
            {
                IncreaseBrightness();
            }
            else if (e.Delta < 0)
            {
                DecreaseBrightness();
            }

            e.Handled = true;
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
            
            // ⚠️ Ни Activate(), ни Focus(): это OSD, а не окно. Раньше каждое нажатие
            // горячей клавиши вырывало фокус из игры или из поля ввода.
            this.Show();

            WindowState = WindowState.Normal;
            PositionOnActiveScreen();
            
            PlayRevealAnimation();
            
            // Запускаем таймер скрытия
            StartHideTimer();
        }
    }
}