using System;
using System.Windows;

namespace JL_Monitor_Brightness
{
    /// <summary>
    /// Проверка обновлений из окна настроек.
    ///
    /// Вынесено из MainWindow.xaml.cs 25.08.2026 вместе с горячими клавишами.
    /// </summary>
    public partial class MainWindow
    {
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
    }
}
