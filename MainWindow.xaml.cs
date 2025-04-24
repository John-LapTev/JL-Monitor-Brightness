using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
                    ColorPreviewRectangle.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.ThemeColor));
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
            
            if (DefaultMonitorComboBox.Items.Count > _settings.DefaultMonitorIndex)
            {
                DefaultMonitorComboBox.SelectedIndex = _settings.DefaultMonitorIndex;
            }
            else if (DefaultMonitorComboBox.Items.Count > 0)
            {
                DefaultMonitorComboBox.SelectedIndex = 0;
            }
        }

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
                MessageBox.Show("Настройки успешно сохранены", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Ошибка при сохранении настроек", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateStartupRegistry()
        {
            try
            {
                RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                
                if (_settings.StartWithWindows)
                {
                    string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    rk.SetValue("JL-Monitor-Brightness", appPath);
                }
                else
                {
                    if (rk.GetValue("JL-Monitor-Brightness") != null)
                    {
                        rk.DeleteValue("JL-Monitor-Brightness", false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении автозапуска: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Event Handlers
        private void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            // Нет необходимости делать что-то здесь, настройка будет сохранена при нажатии кнопки "Сохранить"
        }

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
            _currentKey = e.Key;
            
            // Игнорируем сами модификаторы как ключевые клавиши
            if (_currentKey == Key.LeftCtrl || _currentKey == Key.RightCtrl ||
                _currentKey == Key.LeftAlt || _currentKey == Key.RightAlt ||
                _currentKey == Key.LeftShift || _currentKey == Key.RightShift ||
                _currentKey == Key.LWin || _currentKey == Key.RWin ||
                _currentKey == Key.System)
            {
                return;
            }

            // Для клавиш с модификатором System (например, Alt+Tab) используем реальный код клавиши
            if (_currentKey == Key.Tab && (_currentModifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                _currentKey = Key.Tab;
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
                    // Сначала удаляем старую горячую клавишу
                    try { _hotkeyService.UnregisterHotkeys(); } catch { }
                    
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
            
            int value = (int)e.NewValue;
            TimeoutTextBlock.Text = $"{value} сек";
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
            SaveSettings();
            Close();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }
        #endregion
    }
}