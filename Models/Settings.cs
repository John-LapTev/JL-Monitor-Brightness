using System;
using System.IO;
using System.Windows.Input;
using System.Xml.Serialization;

namespace JL_Monitor_Brightness.Models
{
    [Serializable]
    public class Settings
    {
        // Общие настройки
        public bool StartWithWindows { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public int DefaultMonitorIndex { get; set; } = 0;
        public uint BrightnessStep { get; set; } = 10;
        public bool CheckForUpdatesAtStartup { get; set; } = true;
        public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;
        public string CurrentVersion { get; set; } = "1.0.0";
        
        // Настройки горячих клавиш
        public int BrightnessUpKey { get; set; } = (int)Key.Up;
        public int BrightnessUpModifiers { get; set; } = (int)ModifierKeys.Alt | (int)ModifierKeys.Control;
        
        public int BrightnessDownKey { get; set; } = (int)Key.Down;
        public int BrightnessDownModifiers { get; set; } = (int)ModifierKeys.Alt | (int)ModifierKeys.Control;
        
        public int BrightnessOverlayKey { get; set; } = (int)Key.Home;
        public int BrightnessOverlayModifiers { get; set; } = (int)ModifierKeys.Alt | (int)ModifierKeys.Control;
        
        // Настройки интерфейса
        public double OverlayOpacity { get; set; } = 0.9;
        public int OverlayTimeout { get; set; } = 3000; // миллисекунды
        public bool ShowPercentage { get; set; } = true;
        public string ThemeColor { get; set; } = "#1E90FF"; // DodgerBlue

        private static readonly string SettingsFilePath = GetSettingsFilePath();

        private static string GetSettingsFilePath()
        {
            // Проверяем, является ли приложение портативным
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string exeDirectory = Path.GetDirectoryName(exePath);
            
            // Проверяем, есть ли возможность записи в папку приложения
            try
            {
                // Проверяем, можем ли мы создать тестовый файл
                string testPath = Path.Combine(exeDirectory, "write_test.tmp");
                File.WriteAllText(testPath, "test");
                File.Delete(testPath);
                
                // Если можем писать в папку приложения, используем её
                return Path.Combine(exeDirectory, "settings.xml");
            }
            catch 
            {
                // Если не можем писать в папку приложения, используем AppData
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "JL-Monitor-Brightness", "settings.xml");
            }
        }

        public static Settings LoadSettings()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(SettingsFilePath))
                {
                    using (var stream = new FileStream(SettingsFilePath, FileMode.Open))
                    {
                        var serializer = new XmlSerializer(typeof(Settings));
                        return (Settings)serializer.Deserialize(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            return new Settings();
        }

        public bool SaveSettings()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var stream = new FileStream(SettingsFilePath, FileMode.Create))
                {
                    var serializer = new XmlSerializer(typeof(Settings));
                    serializer.Serialize(stream, this);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                return false;
            }
        }
    }
}