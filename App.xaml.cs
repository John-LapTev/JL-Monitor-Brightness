using System;
using System.Windows;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;
using JL_Monitor_Brightness.Models;
using JL_Monitor_Brightness.Services;

namespace JL_Monitor_Brightness
{
    public partial class App : Application
    {
        private MonitorService _monitorService;
        private HotkeyService _hotkeyService;
        private TrayService _trayService;
        private UpdateService _updateService;
        private BrightnessOverlay _brightnessOverlay;
        private MainWindow _mainWindow;
        private Settings _settings;
        
        private PhysicalMonitorInfo _currentMonitor;
        private int _currentMonitorIndex = 0;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                // Загружаем настройки
                _settings = Settings.LoadSettings();
                
                // Инициализируем сервисы
                _monitorService = new MonitorService();
                _hotkeyService = new HotkeyService();
                _trayService = new TrayService(_monitorService);
                _updateService = new UpdateService();
                
                // Инициализируем оверлей
                _brightnessOverlay = new BrightnessOverlay(_monitorService, _settings);
                
                // Регистрируем обработчики событий для горячих клавиш
                _hotkeyService.BrightnessUpPressed += HotkeyService_BrightnessUpPressed;
                _hotkeyService.BrightnessDownPressed += HotkeyService_BrightnessDownPressed;
                _hotkeyService.BrightnessOverlayPressed += HotkeyService_BrightnessOverlayPressed;
                
                // Регистрируем обработчики событий для трея
                _trayService.OpenSettingsRequested += TrayService_OpenSettingsRequested;
                _trayService.ExitRequested += TrayService_ExitRequested;
                _trayService.MonitorSelected += TrayService_MonitorSelected;
                _trayService.BrightnessIncreaseRequested += TrayService_BrightnessIncreaseRequested;
                _trayService.BrightnessDecreaseRequested += TrayService_BrightnessDecreaseRequested;
                _trayService.ShowOverlayRequested += TrayService_ShowOverlayRequested;
                _trayService.CheckForUpdatesRequested += TrayService_CheckForUpdatesRequested;
                
                // Инициализируем трей
                _trayService.Initialize();
                
                // Обновляем список мониторов
                RefreshMonitors();
                
                // Регистрируем горячие клавиши
                RegisterHotkeys();
                
                // Проверяем аргументы командной строки
                bool showSettings = true;
                foreach (string arg in e.Args)
                {
                    if (arg.ToLower() == "/minimized" || arg.ToLower() == "-minimized")
                    {
                        showSettings = false;
                        break;
                    }
                }
                
                // Показываем окно настроек при первом запуске
                if (showSettings)
                {
                    ShowSettings();
                }
                else
                {
                    // Показываем уведомление о запуске в трее
                    _trayService.ShowNotification("JL Monitor Brightness", 
                        "Приложение запущено. Используйте значок в трее для настройки.", 
                        BalloonIcon.Info);
                }
                
                // Проверяем обновления при запуске, если опция включена
                if (_settings.CheckForUpdatesAtStartup)
                {
                    // Проверяем не чаще раза в день
                    if (DateTime.Now.Subtract(_settings.LastUpdateCheck).TotalDays >= 1)
                    {
                        CheckForUpdatesAsync(false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске приложения: {ex.Message}\n\n{ex.StackTrace}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            // Освобождаем ресурсы
            _hotkeyService.UnregisterHotkeys();
            _monitorService.ReleaseMonitors();
            _trayService.Dispose();
        }

        private void RegisterHotkeys()
        {
            try
            {
                // Восстанавливаем горячие клавиши из настроек
                if (_settings.BrightnessUpKey != (int)Key.None)
                {
                    _hotkeyService.UpdateHotkey("BrightnessUp", 
                        (Key)_settings.BrightnessUpKey, 
                        (ModifierKeys)_settings.BrightnessUpModifiers);
                }
                
                if (_settings.BrightnessDownKey != (int)Key.None)
                {
                    _hotkeyService.UpdateHotkey("BrightnessDown", 
                        (Key)_settings.BrightnessDownKey, 
                        (ModifierKeys)_settings.BrightnessDownModifiers);
                }
                
                if (_settings.BrightnessOverlayKey != (int)Key.None)
                {
                    _hotkeyService.UpdateHotkey("BrightnessOverlay", 
                        (Key)_settings.BrightnessOverlayKey, 
                        (ModifierKeys)_settings.BrightnessOverlayModifiers);
                }
                
                // Регистрируем горячие клавиши
                _hotkeyService.RegisterHotkeys();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при регистрации горячих клавиш: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshMonitors()
        {
            var monitors = _monitorService.GetMonitors();
            
            if (monitors.Count > 0)
            {
                // Устанавливаем текущий монитор
                _currentMonitorIndex = Math.Min(_settings.DefaultMonitorIndex, monitors.Count - 1);
                _currentMonitor = monitors[_currentMonitorIndex];
                
                // Обновляем список мониторов в трее
                _trayService.UpdateMonitorsList();
                
                // Обновляем доступность кнопок в трее
                _trayService.UpdateBrightnessMenuItems(true);
            }
            else
            {
                _currentMonitor = null;
                _trayService.UpdateBrightnessMenuItems(false);
                _trayService.ShowNotification("Мониторы не найдены", 
                    "Не удалось найти мониторы, поддерживающие регулировку яркости.", 
                    BalloonIcon.Warning);
            }
        }

        private void ShowSettings()
        {
            // Если окно уже открыто, просто активируем его
            if (_mainWindow != null)
            {
                _mainWindow.Activate();
                return;
            }
            
            // Создаем новое окно настроек
            _mainWindow = new MainWindow(_monitorService, _hotkeyService, _settings);
            _mainWindow.Closed += (s, e) => 
            {
                _mainWindow = null;
                
                // После закрытия окна обновляем горячие клавиши и настройки
                RegisterHotkeys();
                _brightnessOverlay = new BrightnessOverlay(_monitorService, _settings);
            };
            
            _mainWindow.Show();
            _mainWindow.Activate();
        }

        private async void CheckForUpdatesAsync(bool showNoUpdatesMessage)
        {
            try
            {
                // Показываем индикатор проверки в трее
                _trayService.ShowNotification("Проверка обновлений", 
                    "Проверка наличия новых версий...", 
                    BalloonIcon.Info);
                
                // Выполняем проверку
                var updateInfo = await _updateService.CheckForUpdatesAsync();
                
                // Обновляем дату последней проверки
                _settings.LastUpdateCheck = DateTime.Now;
                _settings.SaveSettings();
                
                // Если произошла ошибка при проверке
                if (updateInfo == null)
                {
                    _trayService.ShowNotification("Ошибка проверки обновлений", 
                        "Не удалось проверить наличие обновлений. Проверьте подключение к интернету.", 
                        BalloonIcon.Error);
                    return;
                }
                
                // Если есть новая версия
                if (_updateService.IsUpdateAvailable(updateInfo, _settings.CurrentVersion))
                {
                    // Показываем окно обновления
                    var updateWindow = new UpdateWindow(updateInfo, _settings, _updateService);
                    updateWindow.ShowDialog();
                }
                else if (showNoUpdatesMessage)
                {
                    // Если обновлений нет и пользователь запросил проверку вручную
                    _trayService.ShowNotification("Обновлений нет", 
                        "У вас установлена последняя версия программы.", 
                        BalloonIcon.Info);
                }
            }
            catch (Exception ex)
            {
                _trayService.ShowNotification("Ошибка проверки обновлений", 
                    $"Произошла ошибка: {ex.Message}", 
                    BalloonIcon.Error);
            }
        }

        #region Event Handlers
        private void HotkeyService_BrightnessUpPressed(object sender, NHotkey.HotkeyEventArgs e)
        {
            if (_currentMonitor != null)
            {
                _monitorService.IncreaseBrightness(_currentMonitor, _settings.BrightnessStep);
                _brightnessOverlay.SetMonitor(_currentMonitor);
                _brightnessOverlay.ShowOverlay();
            }
        }

        private void HotkeyService_BrightnessDownPressed(object sender, NHotkey.HotkeyEventArgs e)
        {
            if (_currentMonitor != null)
            {
                _monitorService.DecreaseBrightness(_currentMonitor, _settings.BrightnessStep);
                _brightnessOverlay.SetMonitor(_currentMonitor);
                _brightnessOverlay.ShowOverlay();
            }
        }

        private void HotkeyService_BrightnessOverlayPressed(object sender, NHotkey.HotkeyEventArgs e)
        {
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
            Shutdown();
        }

        private void TrayService_MonitorSelected(object sender, int e)
        {
            var monitors = _monitorService.GetMonitors();
            if (monitors.Count > e)
            {
                _currentMonitorIndex = e;
                _currentMonitor = monitors[e];
                _settings.DefaultMonitorIndex = e;
                _settings.SaveSettings();
            }
        }

        private void TrayService_BrightnessIncreaseRequested(object sender, EventArgs e)
        {
            if (_currentMonitor != null)
            {
                _monitorService.IncreaseBrightness(_currentMonitor, _settings.BrightnessStep);
                _brightnessOverlay.SetMonitor(_currentMonitor);
                _brightnessOverlay.ShowOverlay();
            }
        }

        private void TrayService_BrightnessDecreaseRequested(object sender, EventArgs e)
        {
            if (_currentMonitor != null)
            {
                _monitorService.DecreaseBrightness(_currentMonitor, _settings.BrightnessStep);
                _brightnessOverlay.SetMonitor(_currentMonitor);
                _brightnessOverlay.ShowOverlay();
            }
        }

        private void TrayService_ShowOverlayRequested(object sender, EventArgs e)
        {
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
        #endregion
    }
}