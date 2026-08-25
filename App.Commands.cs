using System;
using JL_Monitor_Brightness.Models;
using JL_Monitor_Brightness.Services;

namespace JL_Monitor_Brightness
{
    /// <summary>
    /// Отклик на горячие клавиши и меню в трее: прибавить, убавить, показать шторку,
    /// сменить монитор, открыть настройки, выйти.
    ///
    /// Вынесено из App.xaml.cs 25.08.2026: файл дорос до 595 строк и держал в себе
    /// запуск, мониторы, обновления и все команды разом.
    /// </summary>
    public partial class App
    {
        private void HotkeyService_BrightnessUpPressed(object sender, NHotkey.HotkeyEventArgs e)
        {
            if (СообщитьЕслиНетМониторов())
            {
                return;
            }

            if (_currentMonitor != null)
            {
                _monitorService.IncreaseBrightness(_currentMonitor, _settings.BrightnessStep);
                _brightnessOverlay.SetMonitor(_currentMonitor);
                _brightnessOverlay.ShowOverlay();
            }
        }

        private void HotkeyService_BrightnessDownPressed(object sender, NHotkey.HotkeyEventArgs e)
        {
            if (СообщитьЕслиНетМониторов())
            {
                return;
            }

            if (_currentMonitor != null)
            {
                _monitorService.DecreaseBrightness(_currentMonitor, _settings.BrightnessStep);
                _brightnessOverlay.SetMonitor(_currentMonitor);
                _brightnessOverlay.ShowOverlay();
            }
        }

        private void HotkeyService_BrightnessOverlayPressed(object sender, NHotkey.HotkeyEventArgs e)
        {
            if (СообщитьЕслиНетМониторов())
            {
                return;
            }

            if (_currentMonitor != null)
            {
                _brightnessOverlay.SetMonitor(_currentMonitor);
                _brightnessOverlay.ShowOverlay();
            }
        }

        private void TrayService_OpenSettingsRequested(object sender, EventArgs e)
        {
            ShowSettings();
        }

        private void TrayService_ExitRequested(object sender, EventArgs e)
        {
            // Иначе OnClosing окна настроек перехватит закрытие и снова спрячет его в трей.
            _mainWindow?.CloseForReal();
            Shutdown();
        }

        private void TrayService_MonitorSelected(object sender, int e)
        {
            var monitors = _monitorService.GetMonitors();
            if (monitors.Count > e)
            {
                _currentMonitor = monitors[e];
                _settings.DefaultMonitorIndex = e;
                _settings.SaveSettings();
            }
        }

        private void TrayService_BrightnessIncreaseRequested(object sender, EventArgs e)
        {
            if (СообщитьЕслиНетМониторов())
            {
                return;
            }

            if (_currentMonitor != null)
            {
                _monitorService.IncreaseBrightness(_currentMonitor, _settings.BrightnessStep);
                _brightnessOverlay.SetMonitor(_currentMonitor);
                _brightnessOverlay.ShowOverlay();
            }
        }

        private void TrayService_BrightnessDecreaseRequested(object sender, EventArgs e)
        {
            if (СообщитьЕслиНетМониторов())
            {
                return;
            }

            if (_currentMonitor != null)
            {
                _monitorService.DecreaseBrightness(_currentMonitor, _settings.BrightnessStep);
                _brightnessOverlay.SetMonitor(_currentMonitor);
                _brightnessOverlay.ShowOverlay();
            }
        }

        private void TrayService_ShowOverlayRequested(object sender, EventArgs e)
        {
            if (СообщитьЕслиНетМониторов())
            {
                return;
            }

            if (_currentMonitor != null)
            {
                _brightnessOverlay.SetMonitor(_currentMonitor);
                _brightnessOverlay.ShowOverlay();
            }
        }
        
        private void TrayService_CheckForUpdatesRequested(object sender, EventArgs e)
        {
            CheckForUpdatesAsync(true);
        }
    }
}
