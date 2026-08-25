using System;
using System.Threading;
using Microsoft.Win32;
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

        private const string InstanceMutexName = "Global\\JL-Monitor-Brightness-SingleInstance";
        private Mutex _instanceMutex;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Второй запуск даёт вторую иконку в трее, а регистрация горячих клавиш
            // падает на конфликте — раньше это было особенно вероятно из-за сломанного
            // автозапуска, когда программу запускали руками поверх уже работающей.
            _instanceMutex = new Mutex(true, InstanceMutexName, out bool isFirstInstance);
            if (!isFirstInstance)
            {
                Shutdown();
                return;
            }

            // Без этого любое исключение вне try оставляет процесс-зомби:
            // окна нет, иконки в трее нет, а процесс жив.
            DispatcherUnhandledException += (s, args) =>
            {
                ShowFatal(args.Exception);
                args.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                ShowFatal(args.ExceptionObject as Exception);

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
                _brightnessOverlay.BrightnessFailed += BrightnessOverlay_BrightnessFailed;
                
                // Регистрируем обработчики событий для горячих клавиш
                _hotkeyService.RegistrationFailed += (s, combo) =>
                    _trayService?.ShowNotification(
                        "Горячая клавиша занята",
                        $"Комбинацию {combo} держит другая программа. Задайте другую в настройках.",
                        BalloonIcon.Warning);
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

                // ⚠️ Без этих подписок список мониторов строился ровно один раз за запуск:
                // подключил монитор — его нет, отключил или вышел из сна — хендлы протухли
                // и горячие клавиши молча переставали работать до перезапуска программы.
                SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
                SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
                
                // Регистрируем горячие клавиши
                RegisterHotkeys();
                
                // Проверяем аргументы командной строки
                bool showSettings = true;
                foreach (string arg in e.Args)
                {
                    if (string.Equals(arg, "/minimized", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(arg, "-minimized", StringComparison.OrdinalIgnoreCase))
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
                ShowFatal(ex);
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            // ?. обязателен: если старт упал на ранней стадии, часть сервисов ещё null,
            // и выход из программы завершался NullReferenceException.
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
            SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
            _hotkeyService?.UnregisterHotkeys();
            _monitorService?.Dispose();
            _trayService?.Dispose();
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
        }

        /// <summary>
        /// Показывает ошибку и — главное — ПИШЕТ ЕЁ В ФАЙЛ.
        ///
        /// Модальное окно легко пропустить: оно уходит за другие окна, а остаётся
        /// только системный звук. Так и случилось при первой проверке 25.08.2026:
        /// «звук ошибки есть, а окна нет». Без файла причину не узнать.
        /// </summary>
        private static void ShowFatal(Exception ex)
        {
            string текст = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                           $"{ex?.GetType().Name}: {ex?.Message}\n\n" +
                           $"{ex?.StackTrace}\n" +
                           (ex?.InnerException != null
                               ? $"\nВнутренняя: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}\n"
                               : "") +
                           new string('-', 70) + "\n";

            foreach (string путь in LogPaths())
            {
                try
                {
                    System.IO.File.AppendAllText(путь, текст, System.Text.Encoding.UTF8);
                    break;
                }
                catch
                {
                    // папка может быть закрыта на запись — пробуем следующую
                }
            }

            MessageBox.Show(
                $"Непредвиденная ошибка: {ex?.Message}\n\n" +
                "Подробности записаны в файл ошибки.log рядом с программой " +
                "(или в папке %APPDATA%\\JL-Monitor-Brightness).",
                "JL Monitor Brightness", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>Куда пробуем писать журнал ошибок, по порядку.</summary>
        private static System.Collections.Generic.IEnumerable<string> LogPaths()
        {
            string возле = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(возле))
            {
                string папка = System.IO.Path.GetDirectoryName(возле);
                if (!string.IsNullOrEmpty(папка))
                {
                    yield return System.IO.Path.Combine(папка, "ошибки.log");
                }
            }

            string appData = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "JL-Monitor-Brightness");
            System.IO.Directory.CreateDirectory(appData);
            yield return System.IO.Path.Combine(appData, "ошибки.log");
        }

        private DateTime _lastDdcWarning = DateTime.MinValue;
        private DateTime _lastNoMonitorsHint = DateTime.MinValue;

        /// <summary>
        /// Сообщает, что управлять нечем. Возвращает true, если мониторов нет.
        ///
        /// Без этого нажатие горячей клавиши на компьютере без внешнего монитора
        /// выглядело как поломка: человек жмёт, ничего не происходит, никаких
        /// объяснений. Замечание тестировщика 25.08.2026.
        /// </summary>
        private bool СообщитьЕслиНетМониторов()
        {
            if (_currentMonitor != null)
            {
                return false;
            }

            // Не чаще раза в полминуты: клавишу жмут подряд, а не по одному разу.
            if (DateTime.Now - _lastNoMonitorsHint >= TimeSpan.FromSeconds(30))
            {
                _lastNoMonitorsHint = DateTime.Now;
                _trayService?.ShowNotification(
                    "Управлять нечем",
                    "Внешние мониторы с поддержкой DDC/CI не найдены. Встроенный экран " +
                    "ноутбука программа не регулирует — для него есть клавиши на самом ноутбуке.",
                    BalloonIcon.Info);
            }

            return true;
        }

        private void BrightnessOverlay_BrightnessFailed(object sender, PhysicalMonitorInfo monitor)
        {
            // Не чаще раза в полминуты: при зажатой клавише отказов будет десяток подряд.
            if (DateTime.Now - _lastDdcWarning < TimeSpan.FromSeconds(30))
            {
                return;
            }

            _lastDdcWarning = DateTime.Now;
            _trayService?.ShowNotification(
                "Монитор не отвечает",
                $"«{monitor?.Description}» не принял яркость. Проверьте, включён ли DDC/CI " +
                "в меню монитора, и не подключён ли он через переходник или док-станцию.",
                BalloonIcon.Warning);
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            // Конфигурация экранов поменялась — старые хендлы недействительны.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshMonitors();
                _trayService?.UpdateMonitorsList();
            }));
        }

        private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume)
            {
                return;
            }

            // После пробуждения монитор отвечает не сразу: DDC/CI поднимается
            // с задержкой, и немедленное перечисление вернёт пустой список.
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                RefreshMonitors();
                _trayService?.UpdateMonitorsList();
            };
            timer.Start();
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
                // Монитор по умолчанию берём из настроек, а не всегда первый.
                // Индекс проверяем: мониторы отключают, и сохранённый может выйти за границы.
                int index = _settings.DefaultMonitorIndex;
                if (index < 0 || index >= monitors.Count)
                {
                    index = 0;
                }

                _currentMonitor = monitors[index];
                _trayService.SetSelectedMonitor(index);

                if (_brightnessOverlay != null)
                {
                    _brightnessOverlay.MonitorCount = monitors.Count;
                }
                
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
            // Окно с включённой галочкой «сворачивать в трей» не закрывается, а прячется —
            // поэтому недостаточно Activate(), нужно и Show() для скрытого.
            if (_mainWindow != null)
            {
                if (!_mainWindow.IsVisible)
                {
                    _mainWindow.Show();
                }

                if (_mainWindow.WindowState == WindowState.Minimized)
                {
                    _mainWindow.WindowState = WindowState.Normal;
                }

                _mainWindow.Activate();
                return;
            }
            
            // Создаем новое окно настроек
            _mainWindow = new MainWindow(_monitorService, _hotkeyService, _settings);

            // Сохранение применяется сразу, не дожидаясь закрытия окна: настройки
            // правят пачкой и хотят видеть результат тут же.
            _mainWindow.SettingsSaved += (s, e) =>
            {
                RegisterHotkeys();

                // Акцент кладётся напрямую, а не только через оверлей: до первого
                // вызова шторки оверлея ещё нет, и цвет не применился бы вовсе.
                Resources["PrimaryBrush"] = _settings.CreateThemeBrush();
                _brightnessOverlay?.ApplySettings();
            };

            _mainWindow.Closed += (s, e) => 
            {
                _mainWindow = null;
                
                // После закрытия окна обновляем горячие клавиши и настройки.
                // ⚠️ Раньше здесь создавался новый BrightnessOverlay, а старый не закрывался:
                // окно оставалось в Application.Windows вместе с таймером и подписками,
                // и каждый заход в настройки добавлял по висящему окну.
                RegisterHotkeys();
                _brightnessOverlay?.ApplySettings();
            };
            
            _mainWindow.IsVisibleChanged += (s, e) =>
            {
                // Окно спрятали в трей — момент применить настройки, закрытия ведь не будет.
                if (_mainWindow != null && !_mainWindow.IsVisible)
                {
                    RegisterHotkeys();
                    _brightnessOverlay?.ApplySettings();
                }
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
                
                // Пользователь мог попросить не напоминать про конкретную версию.
                // При ручной проверке (showNoUpdatesMessage) пропуск игнорируем — он сам спросил.
                bool skipped = !showNoUpdatesMessage
                    && !string.IsNullOrEmpty(_settings.SkippedVersion)
                    && string.Equals(_settings.SkippedVersion, updateInfo.LatestVersion,
                                     StringComparison.OrdinalIgnoreCase);

                // Если есть новая версия
                if (!skipped && _updateService.IsUpdateAvailable(updateInfo, _settings.CurrentVersion))
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
        #endregion
    }
}