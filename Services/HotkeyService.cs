using System;
using System.Windows;
using System.Windows.Input;
using NHotkey;
using NHotkey.Wpf;

namespace JL_Monitor_Brightness.Services
{
    public class HotkeyService
    {
        public event EventHandler<HotkeyEventArgs> BrightnessUpPressed;
        public event EventHandler<HotkeyEventArgs> BrightnessDownPressed;
        public event EventHandler<HotkeyEventArgs> BrightnessOverlayPressed;

        private const string BrightnessUpId = "BrightnessUp";
        private const string BrightnessDownId = "BrightnessDown";
        private const string BrightnessOverlayId = "BrightnessOverlay";

        private Key _brightnessUpKey = Key.F2;
        private Key _brightnessDownKey = Key.F1;
        private Key _brightnessOverlayKey = Key.F3;

        private ModifierKeys _brightnessUpModifiers = ModifierKeys.Alt;
        private ModifierKeys _brightnessDownModifiers = ModifierKeys.Alt;
        private ModifierKeys _brightnessOverlayModifiers = ModifierKeys.Alt;

        public void RegisterHotkeys()
        {
            // Регистрируем каждую клавишу по отдельности и продолжаем, даже если одна из них не удалась
            RegisterSingleHotkey(BrightnessUpId, _brightnessUpKey, _brightnessUpModifiers, OnBrightnessUpPressed);
            RegisterSingleHotkey(BrightnessDownId, _brightnessDownKey, _brightnessDownModifiers, OnBrightnessDownPressed);
            RegisterSingleHotkey(BrightnessOverlayId, _brightnessOverlayKey, _brightnessOverlayModifiers, OnBrightnessOverlayPressed);
        }
    
        private void RegisterSingleHotkey(string hotkeyId, Key key, ModifierKeys modifiers, EventHandler<HotkeyEventArgs> handler)
        {
            // Пропускаем регистрацию, если клавиша не задана или это клавиша None
            if (key == Key.None || modifiers == ModifierKeys.None)
            {
                System.Diagnostics.Debug.WriteLine($"Skipping registration for {hotkeyId}: No key or modifier specified");
                return;
            }
                
            try
            {
                // Сначала попробуем удалить существующую регистрацию
                try { HotkeyManager.Current.Remove(hotkeyId); } catch { }
                
                // Теперь зарегистрируем горячую клавишу
                HotkeyManager.Current.AddOrReplace(hotkeyId, key, modifiers, handler);
                System.Diagnostics.Debug.WriteLine($"Successfully registered hotkey {hotkeyId}: {GetKeyDescription(key, modifiers)}");
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем работу программы
                System.Diagnostics.Debug.WriteLine($"Error registering hotkey {hotkeyId} ({GetKeyDescription(key, modifiers)}): {ex.Message}");
                
                // Попробуем добавить обработчик только для приложения (не глобально)
                try
                {
                    MessageBox.Show($"Не удалось зарегистрировать горячую клавишу: {GetKeyDescription(key, modifiers)}\nВозможно, она уже используется другим приложением.",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch { /* игнорируем ошибку, если не удается показать сообщение */ }
            }
        }

        public void UnregisterHotkeys()
        {
            try
            {
                HotkeyManager.Current.Remove(BrightnessUpId);
                HotkeyManager.Current.Remove(BrightnessDownId);
                HotkeyManager.Current.Remove(BrightnessOverlayId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error unregistering hotkeys: {ex.Message}");
            }
        }

        public bool UpdateHotkey(string hotkeyName, Key key, ModifierKeys modifiers)
        {
            try
            {
                switch (hotkeyName)
                {
                    case BrightnessUpId:
                        _brightnessUpKey = key;
                        _brightnessUpModifiers = modifiers;
                        HotkeyManager.Current.AddOrReplace(BrightnessUpId, key, modifiers, OnBrightnessUpPressed);
                        break;
                    case BrightnessDownId:
                        _brightnessDownKey = key;
                        _brightnessDownModifiers = modifiers;
                        HotkeyManager.Current.AddOrReplace(BrightnessDownId, key, modifiers, OnBrightnessDownPressed);
                        break;
                    case BrightnessOverlayId:
                        _brightnessOverlayKey = key;
                        _brightnessOverlayModifiers = modifiers;
                        HotkeyManager.Current.AddOrReplace(BrightnessOverlayId, key, modifiers, OnBrightnessOverlayPressed);
                        break;
                    default:
                        return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating hotkey {hotkeyName}: {ex.Message}");
                return false;
            }
        }

        private void OnBrightnessUpPressed(object sender, HotkeyEventArgs e)
        {
            BrightnessUpPressed?.Invoke(this, e);
            e.Handled = true;
        }

        private void OnBrightnessDownPressed(object sender, HotkeyEventArgs e)
        {
            BrightnessDownPressed?.Invoke(this, e);
            e.Handled = true;
        }

        private void OnBrightnessOverlayPressed(object sender, HotkeyEventArgs e)
        {
            BrightnessOverlayPressed?.Invoke(this, e);
            e.Handled = true;
        }

        public string GetHotkeyDescription(string hotkeyName)
        {
            switch (hotkeyName)
            {
                case BrightnessUpId:
                    return GetKeyDescription(_brightnessUpKey, _brightnessUpModifiers);
                case BrightnessDownId:
                    return GetKeyDescription(_brightnessDownKey, _brightnessDownModifiers);
                case BrightnessOverlayId:
                    return GetKeyDescription(_brightnessOverlayKey, _brightnessOverlayModifiers);
                default:
                    return string.Empty;
            }
        }

        private string GetKeyDescription(Key key, ModifierKeys modifiers)
        {
            string description = string.Empty;

            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                description += "Alt + ";
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                description += "Ctrl + ";
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                description += "Shift + ";
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                description += "Win + ";

            description += key.ToString();

            return description;
        }
    }
}