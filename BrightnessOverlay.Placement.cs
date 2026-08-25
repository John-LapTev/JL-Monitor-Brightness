using System;
using System.Windows;
using System.Windows.Media;

namespace JL_Monitor_Brightness
{
    /// <summary>
    /// Где показать шторку и как не отобрать фокус: вызовы Win32 и расчёт положения
    /// на том экране, где сейчас курсор.
    ///
    /// Вынесено из BrightnessOverlay.xaml.cs 25.08.2026 — объявления P/Invoke и
    /// структур занимали треть файла и мешали читать саму логику шторки.
    /// </summary>
    public partial class BrightnessOverlay
    {

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
    }
}
