using System;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

namespace JL_Monitor_Brightness
{
    /// <summary>
    /// Проверка обновлений при запуске и по команде из трея.
    ///
    /// Вынесено из App.xaml.cs 25.08.2026 вместе с командами.
    /// </summary>
    public partial class App
    {

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
    }
}
