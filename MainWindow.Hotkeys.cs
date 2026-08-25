using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JL_Monitor_Brightness
{
    /// <summary>
    /// Запись горячих клавиш в окне настроек: поле ждёт нажатие, показывает
    /// комбинацию и отдаёт её в настройки.
    ///
    /// Вынесено из MainWindow.xaml.cs 25.08.2026: файл дорос до 674 строк, и в нём
    /// вперемешку жили настройки, оформление окна, горячие клавиши и обновления.
    /// </summary>
    public partial class MainWindow
    {
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
    }
}
