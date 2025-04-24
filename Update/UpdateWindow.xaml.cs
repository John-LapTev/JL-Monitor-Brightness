using System;
using System.Windows;
using JL_Monitor_Brightness.Models;
using JL_Monitor_Brightness.Services;

namespace JL_Monitor_Brightness
{
    public partial class UpdateWindow : Window
    {
        private readonly UpdateInfo _updateInfo;
        private readonly Settings _settings;
        private readonly UpdateService _updateService;

        public UpdateWindow(UpdateInfo updateInfo, Settings settings, UpdateService updateService)
        {
            InitializeComponent();
            
            _updateInfo = updateInfo;
            _settings = settings;
            _updateService = updateService;
            
            // Отображаем информацию о версиях
            VersionInfoTextBlock.Text = $"Текущая версия: {_settings.CurrentVersion} | Новая версия: {_updateInfo.LatestVersion}";
            
            // Отображаем список изменений
            ReleaseNotesTextBlock.Text = _updateInfo.ReleaseNotes?.Replace("\\n", "\n") ?? "Нет информации о изменениях";
            
            // Блокируем кнопки, если соответствующие URL не указаны
            DownloadInstallerButton.IsEnabled = !string.IsNullOrEmpty(_updateInfo.InstallerUrl);
            DownloadPortableButton.IsEnabled = !string.IsNullOrEmpty(_updateInfo.PortableUrl);
        }

        private void DownloadInstallerButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_updateInfo.InstallerUrl))
            {
                _updateService.OpenInBrowser(_updateInfo.InstallerUrl);
                SaveDontRemindPreference();
                Close();
            }
        }

        private void DownloadPortableButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_updateInfo.PortableUrl))
            {
                _updateService.OpenInBrowser(_updateInfo.PortableUrl);
                SaveDontRemindPreference();
                Close();
            }
        }

        private void RemindLaterButton_Click(object sender, RoutedEventArgs e)
        {
            // Просто закрываем окно
            Close();
        }
        
        private void SaveDontRemindPreference()
        {
            if (DontRemindCheckBox.IsChecked == true)
            {
                // Пользователь не хочет получать напоминание об этой версии
                // Сохраняем последнюю проверенную версию
                _settings.LastUpdateCheck = DateTime.Now;
                _settings.SaveSettings();
            }
        }
    }
}