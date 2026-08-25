using System;
using System.IO;
using System.Reflection;
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

        /// <summary>Версия, про которую пользователь попросил не напоминать.</summary>
        public string SkippedVersion { get; set; }
        // ⚠️ Версия НЕ хранится в настройках: иначе после обновления в settings.xml
        // навсегда остаётся старое значение и программа вечно предлагает обновиться.
        // Единственный источник правды — версия сборки.
        [XmlIgnore]
        public string CurrentVersion => AppVersion;

        /// <summary>Версия текущей сборки, вида "1.0.0".</summary>
        public static string AppVersion { get; } = ResolveAppVersion();

        private static string ResolveAppVersion()
        {
            var asm = Assembly.GetExecutingAssembly();
            string informational = asm
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                // У informational-версии бывает суффикс сборки: "1.2.0+9a1b2c3"
                int plus = informational.IndexOf('+');
                return plus > 0 ? informational.Substring(0, plus) : informational;
            }

            return asm.GetName().Version?.ToString(3) ?? "0.0.0";
        }
        
        // Настройки горячих клавиш
        public int BrightnessUpKey { get; set; } = (int)Key.Up;
        public int BrightnessUpModifiers { get; set; } = (int)ModifierKeys.Alt | (int)ModifierKeys.Control;
        
        public int BrightnessDownKey { get; set; } = (int)Key.Down;
        public int BrightnessDownModifiers { get; set; } = (int)ModifierKeys.Alt | (int)ModifierKeys.Control;
        
        public int BrightnessOverlayKey { get; set; } = (int)Key.Home;
        public int BrightnessOverlayModifiers { get; set; } = (int)ModifierKeys.Alt | (int)ModifierKeys.Control;
        
        // Настройки интерфейса
        public double OverlayOpacity { get; set; } = 0.9;
        public int OverlayTimeout { get; set; } = DefaultOverlayTimeout;
        public bool ShowPercentage { get; set; } = true;
        public string ThemeColor { get; set; } = DefaultThemeColor;

        public const int DefaultOverlayTimeout = 1400;   // мс: пилюля небольшая, три секунды ей ни к чему
        public const string DefaultThemeColor = "#7C82F4";

        private static readonly string SettingsFilePath = GetSettingsFilePath();

        private static string AppDataSettingsPath() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "JL-Monitor-Brightness", "settings.xml");

        private static string GetSettingsFilePath()
        {
            // Проверяем, является ли приложение портативным
            // Assembly.Location в .NET 6 указывает на управляемую .dll, а в single-file
            // публикации возвращает пустую строку — портативный режим тогда молча
            // уезжает в AppData вместо папки рядом с программой.
            string exePath = Environment.ProcessPath;
            string exeDirectory = string.IsNullOrEmpty(exePath) ? null : Path.GetDirectoryName(exePath);

            if (string.IsNullOrEmpty(exeDirectory))
            {
                return AppDataSettingsPath();
            }
            
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
                return AppDataSettingsPath();
            }
        }

        /// <summary>
        /// Приводит значения в допустимые пределы. Файл настроек правится руками и
        /// переживает обновления — испорченное значение не должно ронять запуск.
        /// </summary>
        public void Validate()
        {
            if (OverlayOpacity < 0.1 || OverlayOpacity > 1.0)
            {
                OverlayOpacity = 0.9;
            }

            if (OverlayTimeout < 300 || OverlayTimeout > 30000)
            {
                OverlayTimeout = DefaultOverlayTimeout;
            }

            if (BrightnessStep == 0 || BrightnessStep > 50)
            {
                BrightnessStep = 10;
            }

            if (DefaultMonitorIndex < 0)
            {
                DefaultMonitorIndex = 0;
            }

            if (!TryParseColor(ThemeColor, out _))
            {
                ThemeColor = DefaultThemeColor;
            }
        }

        /// <summary>Кисть акцента. Никогда не бросает: битый цвет откатывается к стандартному.</summary>
        public System.Windows.Media.SolidColorBrush CreateThemeBrush()
        {
            if (!TryParseColor(ThemeColor, out var color))
            {
                TryParseColor(DefaultThemeColor, out color);
            }

            var brush = new System.Windows.Media.SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static bool TryParseColor(string value, out System.Windows.Media.Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            try
            {
                color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
                return true;
            }
            catch
            {
                return false;
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
                        var loaded = (Settings)serializer.Deserialize(stream);
                        loaded.Validate();
                        return loaded;
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