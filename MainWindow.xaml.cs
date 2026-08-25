using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using JL_Monitor_Brightness.Models;
using JL_Monitor_Brightness.Services;
using Microsoft.Win32;

namespace JL_Monitor_Brightness
{
    public partial class MainWindow : Window
    {
        private readonly MonitorService _monitorService;
        private readonly HotkeyService _hotkeyService;
        private readonly Settings _settings;
        private readonly UpdateService _updateService;
        
        private bool _isInitializing = true;
        private TextBox _currentHotkeyTextBox;
        private ModifierKeys _currentModifiers;
        private Key _currentKey;

        public MainWindow(MonitorService monitorService, HotkeyService hotkeyService, Settings settings)
        {
            InitializeComponent();
            
            _monitorService = monitorService;
            _hotkeyService = hotkeyService;
            _settings = settings;
            _updateService = new UpdateService();
            
            LoadSettings();
            PopulateMonitors();
            
            _isInitializing = false;
        }

        private void LoadSettings()
        {
            // Общие настройки
            StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
            MinimizeToTrayCheckBox.IsChecked = _settings.MinimizeToTray;
            BrightnessStepSlider.Value = _settings.BrightnessStep;
            BrightnessStepTextBlock.Text = $"{_settings.BrightnessStep}%";
            
            // Горячие клавиши
            BrightnessUpHotkeyTextBox.Text = _hotkeyService.GetHotkeyDescription("BrightnessUp");
            BrightnessDownHotkeyTextBox.Text = _hotkeyService.GetHotkeyDescription("BrightnessDown");
            BrightnessOverlayHotkeyTextBox.Text = _hotkeyService.GetHotkeyDescription("BrightnessOverlay");
            
            // Настройки интерфейса
            OpacitySlider.Value = _settings.OverlayOpacity;
            OpacityTextBlock.Text = $"{Math.Round(_settings.OverlayOpacity * 100)}%";
            
            TimeoutSlider.Value = _settings.OverlayTimeout / 1000;
            TimeoutTextBlock.Text = $"{_settings.OverlayTimeout / 1000} сек";
            
            ShowPercentageCheckBox.IsChecked = _settings.ShowPercentage;
            
            // Цветовая тема
            foreach (ComboBoxItem item in ThemeColorComboBox.Items)
            {
                if (item.Tag.ToString() == _settings.ThemeColor)
                {
                    ThemeColorComboBox.SelectedItem = item;
                    ColorPreviewRectangle.Fill = _settings.CreateThemeBrush();
                    break;
                }
            }
            
            if (ThemeColorComboBox.SelectedItem == null && ThemeColorComboBox.Items.Count > 0)
            {
                ThemeColorComboBox.SelectedIndex = 0;
            }
            
            // Настройки обновления
            CheckForUpdatesCheckBox.IsChecked = _settings.CheckForUpdatesAtStartup;
            VersionTextBlock.Text = $"Версия {_settings.CurrentVersion}";
        }

        private void PopulateMonitors()
        {
            DefaultMonitorComboBox.Items.Clear();
            
            var monitors = _monitorService.GetMonitors();
            
            for (int i = 0; i < monitors.Count; i++)
            {
                var monitor = monitors[i];
                DefaultMonitorComboBox.Items.Add(new ComboBoxItem
                {
                    Content = $"{i + 1}. {monitor.Description}",
                    Tag = i
                });
            }

            // Пустой список без объяснения выглядит как поломка. Говорим прямо,
            // что мониторов нет и почему — замечание тестировщика 25.08.2026.
            if (monitors.Count == 0)
            {
                DefaultMonitorComboBox.Items.Add(new ComboBoxItem
                {
                    Content = "Мониторы с DDC/CI не найдены",
                    IsEnabled = false,
                });
                DefaultMonitorComboBox.SelectedIndex = 0;
                DefaultMonitorComboBox.ToolTip =
                    "Программа управляет только внешними мониторами с поддержкой DDC/CI. " +
                    "Встроенный экран ноутбука регулируется его собственными клавишами.";
                return;
            }

            DefaultMonitorComboBox.ToolTip = null;
            
            if (DefaultMonitorComboBox.Items.Count > _settings.DefaultMonitorIndex)
            {
                DefaultMonitorComboBox.SelectedIndex = _settings.DefaultMonitorIndex;
            }
            else if (DefaultMonitorComboBox.Items.Count > 0)
            {
                DefaultMonitorComboBox.SelectedIndex = 0;
            }
        }

        /// <summary>Настройки записаны на диск — самое время их применить.</summary>
        public event EventHandler SettingsSaved;

        private void SaveSettings()
        {
            // Общие настройки
            _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
            _settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? false;
            _settings.BrightnessStep = (uint)BrightnessStepSlider.Value;
            
            if (DefaultMonitorComboBox.SelectedItem != null)
            {
                _settings.DefaultMonitorIndex = (int)((ComboBoxItem)DefaultMonitorComboBox.SelectedItem).Tag;
            }
            
            // Горячие клавиши
            // Они уже обновлены в событиях клавиш
            
            // Настройки интерфейса
            _settings.OverlayOpacity = OpacitySlider.Value;
            _settings.OverlayTimeout = (int)(TimeoutSlider.Value * 1000);
            _settings.ShowPercentage = ShowPercentageCheckBox.IsChecked ?? true;
            
            if (ThemeColorComboBox.SelectedItem != null)
            {
                _settings.ThemeColor = ((ComboBoxItem)ThemeColorComboBox.SelectedItem).Tag.ToString();
            }
            
            // Настройки обновления
            _settings.CheckForUpdatesAtStartup = CheckForUpdatesCheckBox.IsChecked ?? true;
            
            // Сохранение настроек
            if (_settings.SaveSettings())
            {
                UpdateStartupRegistry();

                // ⛔ Раньше настройки вступали в силу только при закрытии окна —
                // App слушал Closed и IsVisibleChanged. Как только окно перестало
                // закрываться после сохранения, применение пропало: цвет акцента
                // сохранялся в файл, но на экране не менялся до перезапуска.
                SettingsSaved?.Invoke(this, EventArgs.Empty);

                ShowSaveToast("Сохранено", ok: true);
            }
            else
            {
                ShowSaveToast("Не удалось сохранить", ok: false);
            }
        }

        /// <summary>
        /// Плашка подтверждения внизу окна.
        ///
        /// ⛔ Раньше здесь был MessageBox. Он приходил со звуком системной ошибки на
        /// обычное успешное сохранение, требовал нажать «ОК», а следом окно ещё и
        /// закрывалось — чтобы поправить вторую настройку, приходилось лезть в трей.
        /// Замечено при проверке 25.08.2026.
        /// </summary>
        private void ShowSaveToast(string message, bool ok)
        {
            SaveToastText.Text = message;
            SaveToast.Background = ok
                ? (Brush)Application.Current.Resources["PrimaryBrush"]
                : new SolidColorBrush(Color.FromRgb(0xE1, 0x4B, 0x4B));

            // Появление, пауза, уход. BeginTime у второй пары держит плашку на
            // экране: две секунды — успеваешь прочитать, но не ждёшь.
            var fade = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(2.4) };
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.16))));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.0))));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.4))));

            var slide = new DoubleAnimation(6, 0, TimeSpan.FromSeconds(0.22))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            SaveToast.BeginAnimation(OpacityProperty, fade);
            SaveToastSlide.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        private const string RunKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string RunValueName = "JL-Monitor-Brightness";
        private const string StartupArgument = "/minimized";

        private void UpdateStartupRegistry()
        {
            try
            {
                using RegistryKey rk = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (rk == null)
                {
                    return;
                }

                if (_settings.StartWithWindows)
                {
                    // Assembly.Location в .NET 6 указывает на управляемую .dll (а в single-file
                    // публикации вообще пуст) — Windows такой путь не запускает. Нужен apphost.
                    string appPath = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(appPath))
                    {
                        return;
                    }

                    // Путь установки содержит пробелы, поэтому кавычки обязательны:
                    // иначе аргумент склеится с путём и разбор командной строки сломается.
                    rk.SetValue(RunValueName, $"\"{appPath}\" {StartupArgument}");
                }
                else if (rk.GetValue(RunValueName) != null)
                {
                    rk.DeleteValue(RunValueName, false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении автозапуска: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Появление содержимого при смене вкладки.
        ///
        /// ⛔ Раньше это делал EventTrigger прямо в шаблоне TabControl — и приложение
        /// не запускалось вовсе: Selector.SelectionChanged поднимает любой ComboBox
        /// внутри вкладки, событие всплывает до TabControl, триггер ловит чужое и падает
        /// с «Не удаётся найти имя Content». Здесь источник проверяется явно.
        /// </summary>
        private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Событие всплывает от вложенных списков — берём только своё.
            if (!ReferenceEquals(e.OriginalSource, Tabs))
            {
                return;
            }

            if (Tabs.SelectedContent is not UIElement content)
            {
                return;
            }

            var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1,
                TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                },
                FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop
            };
            content.BeginAnimation(OpacityProperty, fade);
        }

        /// <summary>
        /// Двойной клик по ползунку возвращает значение по умолчанию.
        /// Просьба тестировщика 25.08.2026 — привычка из программ со звуком, где так
        /// сбрасывают любую ручку. Значение лежит в Tag рядом с самим ползунком,
        /// чтобы не держать вторую таблицу умолчаний в коде.
        /// </summary>
        private void Slider_ResetToDefault(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not Slider slider || slider.Tag is not string текст)
            {
                return;
            }

            if (double.TryParse(текст, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out double значение))
            {
                slider.Value = значение;
                e.Handled = true;
            }
        }

        #region Окно без системной рамки

        /// <summary>
        /// Появление окна: всплывает с лёгким увеличением за 0.22 с.
        /// Кривая та же, что у регулятора — движение резко стартует и мягко доезжает.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var spline = new System.Windows.Media.Animation.KeySpline(0.22, 1, 0.36, 1);

            this.Opacity = 0;
            var fade = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
            fade.KeyFrames.Add(new System.Windows.Media.Animation.SplineDoubleKeyFrame(1,
                System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180)), spline));
            this.BeginAnimation(OpacityProperty, fade);

            foreach (var prop in new[] { System.Windows.Media.ScaleTransform.ScaleXProperty,
                                         System.Windows.Media.ScaleTransform.ScaleYProperty })
            {
                var scale = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
                scale.KeyFrames.Add(new System.Windows.Media.Animation.SplineDoubleKeyFrame(1,
                    System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240)), spline));
                WindowScale.BeginAnimation(prop, scale);
            }
        }

        // WindowStyle=None убирает системную рамку — перетаскивание делаем сами.
        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                try { DragMove(); } catch { /* окно уже закрывается */ }
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            // Close(), а не CloseForReal(): при включённой галочке окно должно уйти
            // в трей — крестик здесь ведёт себя как системный.
            Close();
        }

        #endregion

        #region Event Handlers
        private void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            // Нет необходимости делать что-то здесь, настройка будет сохранена при нажатии кнопки "Сохранить"
        }

        /// <summary>
        /// Закрытие окна при включённой галочке прячет его в трей, а не выгружает.
        /// Раньше настройка MinimizeToTray не читалась нигде — галочка не делала ничего.
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_settings != null && _settings.MinimizeToTray && !_closingToExit)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            base.OnClosing(e);
        }

        /// <summary>Закрыть окно по-настоящему, минуя сворачивание в трей.</summary>
        public void CloseForReal()
        {
            _closingToExit = true;
            Close();
        }

        private bool _closingToExit;

        private void MinimizeToTrayCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            // Нет необходимости делать что-то здесь, настройка будет сохранена при нажатии кнопки "Сохранить"
        }

        private void DefaultMonitorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            // Нет необходимости делать что-то здесь, настройка будет сохранена при нажатии кнопки "Сохранить"
        }

        private void BrightnessStepSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            
            int value = (int)e.NewValue;
            BrightnessStepTextBlock.Text = $"{value}%";
        }

        private void RefreshMonitorsButton_Click(object sender, RoutedEventArgs e)
        {
            PopulateMonitors();
        }

        private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _currentHotkeyTextBox = sender as TextBox;
            if (_currentHotkeyTextBox != null)
            {
                _currentHotkeyTextBox.Text = "Нажмите комбинацию клавиш...";
                _currentModifiers = ModifierKeys.None;
                _currentKey = Key.None;
            }
        }

        private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null && textBox.Text == "Нажмите комбинацию клавиш...")
            {
                // Восстанавливаем исходную горячую клавишу
                if (textBox == BrightnessUpHotkeyTextBox)
                {
                    textBox.Text = _hotkeyService.GetHotkeyDescription("BrightnessUp");
                }
                else if (textBox == BrightnessDownHotkeyTextBox)
                {
                    textBox.Text = _hotkeyService.GetHotkeyDescription("BrightnessDown");
                }
                else if (textBox == BrightnessOverlayHotkeyTextBox)
                {
                    textBox.Text = _hotkeyService.GetHotkeyDescription("BrightnessOverlay");
                }
            }
            
            _currentHotkeyTextBox = null;
        }

        private void HotkeyTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            
            if (_currentHotkeyTextBox == null) return;
            
            // Получаем модификаторы и клавишу
            _currentModifiers = Keyboard.Modifiers;
            // При зажатом Alt WPF кладёт в e.Key значение Key.System,
            // а настоящую клавишу — в e.SystemKey. Без этого ни одну
            // комбинацию с Alt назначить нельзя.
            _currentKey = e.Key == Key.System ? e.SystemKey : e.Key;
            
            // Игнорируем сами модификаторы как ключевые клавиши
            if (_currentKey == Key.LeftCtrl || _currentKey == Key.RightCtrl ||
                _currentKey == Key.LeftAlt || _currentKey == Key.RightAlt ||
                _currentKey == Key.LeftShift || _currentKey == Key.RightShift ||
                _currentKey == Key.LWin || _currentKey == Key.RWin ||
                _currentKey == Key.System)
            {
                return;
            }
            
            // Требуем хотя бы один модификатор
            if (_currentModifiers == ModifierKeys.None)
            {
                _currentHotkeyTextBox.Text = "Нажмите с модификатором (Ctrl, Alt, Shift)";
                return;
            }
            
            // Создаем описание горячей клавиши
            string description = string.Empty;
            
            if ((_currentModifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                description += "Alt + ";
            if ((_currentModifiers & ModifierKeys.Control) == ModifierKeys.Control)
                description += "Ctrl + ";
            if ((_currentModifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                description += "Shift + ";
            if ((_currentModifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                description += "Win + ";
            
            description += _currentKey.ToString();
            
            _currentHotkeyTextBox.Text = description;
            
            // Обновляем сервис горячих клавиш
            string hotkeyName = string.Empty;
            
            if (_currentHotkeyTextBox == BrightnessUpHotkeyTextBox)
            {
                hotkeyName = "BrightnessUp";
                _settings.BrightnessUpKey = (int)_currentKey;
                _settings.BrightnessUpModifiers = (int)_currentModifiers;
            }
            else if (_currentHotkeyTextBox == BrightnessDownHotkeyTextBox)
            {
                hotkeyName = "BrightnessDown";
                _settings.BrightnessDownKey = (int)_currentKey;
                _settings.BrightnessDownModifiers = (int)_currentModifiers;
            }
            else if (_currentHotkeyTextBox == BrightnessOverlayHotkeyTextBox)
            {
                hotkeyName = "BrightnessOverlay";
                _settings.BrightnessOverlayKey = (int)_currentKey;
                _settings.BrightnessOverlayModifiers = (int)_currentModifiers;
            }
            
            if (!string.IsNullOrEmpty(hotkeyName))
            {
                try
                {
                    // ⚠️ Раньше здесь снимались ВСЕ три комбинации, а регистрировалась
                    // обратно только редактируемая: пока открыты настройки, две другие
                    // были мертвы. UpdateHotkey сам делает Remove нужной перед AddOrReplace.
                    
                    // Регистрируем новую горячую клавишу
                    bool success = _hotkeyService.UpdateHotkey(hotkeyName, _currentKey, _currentModifiers);
                    
                    if (!success)
                    {
                        MessageBox.Show($"Не удалось зарегистрировать горячую клавишу: {description}\nВозможно, она уже используется другим приложением.", 
                                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                        _currentHotkeyTextBox.Text = "Ошибка регистрации!";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при регистрации горячей клавиши: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    _currentHotkeyTextBox.Text = "Ошибка регистрации!";
                }
            }
            
            // Снимаем фокус с текстового поля
            Keyboard.ClearFocus();
        }

        private void ClearHotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;
            
            string hotkeyName = button.Tag.ToString();
            
            // Очищаем текстовое поле и настройки
            if (hotkeyName == "BrightnessUp")
            {
                BrightnessUpHotkeyTextBox.Text = "Не задано";
                _settings.BrightnessUpKey = (int)Key.None;
                _settings.BrightnessUpModifiers = (int)ModifierKeys.None;
            }
            else if (hotkeyName == "BrightnessDown")
            {
                BrightnessDownHotkeyTextBox.Text = "Не задано";
                _settings.BrightnessDownKey = (int)Key.None;
                _settings.BrightnessDownModifiers = (int)ModifierKeys.None;
            }
            else if (hotkeyName == "BrightnessOverlay")
            {
                BrightnessOverlayHotkeyTextBox.Text = "Не задано";
                _settings.BrightnessOverlayKey = (int)Key.None;
                _settings.BrightnessOverlayModifiers = (int)ModifierKeys.None;
            }
            
            try
            {
                // Удаляем горячую клавишу
                _hotkeyService.UpdateHotkey(hotkeyName, Key.None, ModifierKeys.None);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении горячей клавиши: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            
            double value = Math.Round(e.NewValue * 100);
            OpacityTextBlock.Text = $"{value}%";
        }

        private void TimeoutSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            
            int ms = (int)e.NewValue;
            // Слайдер задаёт миллисекунды, а человеку понятнее секунды с десятыми.
            TimeoutTextBlock.Text = (ms / 1000.0).ToString("0.0", System.Globalization.CultureInfo.CurrentCulture) + " с";
        }

        private void ShowPercentageCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            // Нет необходимости делать что-то здесь, настройка будет сохранена при нажатии кнопки "Сохранить"
        }

        private void ThemeColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            
            ComboBoxItem selectedItem = ThemeColorComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string colorCode = selectedItem.Tag.ToString();
                ColorPreviewRectangle.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorCode));
            }
        }
        
        private void CheckForUpdatesCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            // Нет необходимости делать что-то здесь, настройка будет сохранена при нажатии кнопки "Сохранить"
        }
        
        private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckForUpdatesButton.IsEnabled = false;
                CheckForUpdatesButton.Content = "Проверка...";
                
                var updateInfo = await _updateService.CheckForUpdatesAsync();
                
                CheckForUpdatesButton.IsEnabled = true;
                CheckForUpdatesButton.Content = "Проверить обновления сейчас";
                
                if (updateInfo == null)
                {
                    MessageBox.Show("Не удалось проверить наличие обновлений. Проверьте подключение к интернету.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (_updateService.IsUpdateAvailable(updateInfo, _settings.CurrentVersion))
                {
                    var updateWindow = new UpdateWindow(updateInfo, _settings, _updateService);
                    updateWindow.ShowDialog();
                }
                else
                {
                    MessageBox.Show("У вас установлена последняя версия программы.",
                        "Обновлений нет", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CheckForUpdatesButton.IsEnabled = true;
                CheckForUpdatesButton.Content = "Проверить обновления сейчас";
                
                MessageBox.Show($"Произошла ошибка при проверке обновлений: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // ⛔ Окно НЕ закрывается: настройки правят пачкой, а после каждого
            // сохранения приходилось поднимать его заново из трея.
            SaveSettings();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }
        #endregion
    }
}