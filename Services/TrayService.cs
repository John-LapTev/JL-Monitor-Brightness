using System;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using JL_Monitor_Brightness.Models;

namespace JL_Monitor_Brightness.Services
{
    public class TrayService
    {
        private TaskbarIcon _notifyIcon;
        private MonitorService _monitorService;
        private MenuItem _increaseMenuItem;
        private MenuItem _decreaseMenuItem;
        private MenuItem _monitorsMenuItem;

        public event EventHandler OpenSettingsRequested;
        public event EventHandler ExitRequested;
        public event EventHandler<int> MonitorSelected;
        public event EventHandler BrightnessIncreaseRequested;
        public event EventHandler BrightnessDecreaseRequested;
        public event EventHandler ShowOverlayRequested;
        public event EventHandler CheckForUpdatesRequested;

        public TrayService(MonitorService monitorService)
        {
            _monitorService = monitorService;
        }

        public void Initialize()
        {
            _notifyIcon = new TaskbarIcon
            {
                Icon = new System.Drawing.Icon(Application.GetResourceStream(
                    new Uri("pack://application:,,,/Resources/Icons/brightness.ico")).Stream),
                ToolTipText = "Monitor Brightness Controller"
            };

            _notifyIcon.TrayMouseDoubleClick += NotifyIcon_TrayMouseDoubleClick;
            
            CreateContextMenu();
        }

        private void NotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            ShowOverlayRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CreateContextMenu()
        {
            var contextMenu = new ContextMenu();

            // Подменю для выбора монитора
            _monitorsMenuItem = new MenuItem
            {
                Header = "Выбрать монитор"
            };
            contextMenu.Items.Add(_monitorsMenuItem);
            
            contextMenu.Items.Add(new Separator());

            // Пункты для увеличения/уменьшения яркости
            _increaseMenuItem = new MenuItem
            {
                Header = "Увеличить яркость"
            };
            _increaseMenuItem.Click += (s, e) => BrightnessIncreaseRequested?.Invoke(this, EventArgs.Empty);
            
            _decreaseMenuItem = new MenuItem
            {
                Header = "Уменьшить яркость"
            };
            _decreaseMenuItem.Click += (s, e) => BrightnessDecreaseRequested?.Invoke(this, EventArgs.Empty);
            
            contextMenu.Items.Add(_increaseMenuItem);
            contextMenu.Items.Add(_decreaseMenuItem);
            
            contextMenu.Items.Add(new Separator());

            // Пункт для показа оверлея
            var showOverlayMenuItem = new MenuItem
            {
                Header = "Показать регулятор яркости"
            };
            showOverlayMenuItem.Click += (s, e) => ShowOverlayRequested?.Invoke(this, EventArgs.Empty);
            contextMenu.Items.Add(showOverlayMenuItem);
            
            // Пункт для открытия настроек
            var settingsMenuItem = new MenuItem
            {
                Header = "Настройки"
            };
            settingsMenuItem.Click += (s, e) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
            contextMenu.Items.Add(settingsMenuItem);
            
            // Добавляем пункт для проверки обновлений
            var checkForUpdatesMenuItem = new MenuItem
            {
                Header = "Проверить обновления"
            };
            checkForUpdatesMenuItem.Click += (s, e) => CheckForUpdatesRequested?.Invoke(this, EventArgs.Empty);
            contextMenu.Items.Add(checkForUpdatesMenuItem);
            
            contextMenu.Items.Add(new Separator());
            
            // Пункт для выхода из приложения
            var exitMenuItem = new MenuItem
            {
                Header = "Выход"
            };
            exitMenuItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenu = contextMenu;
        }

        public void UpdateMonitorsList()
        {
            _monitorsMenuItem.Items.Clear();
            
            var monitors = _monitorService.GetMonitors();
            
            for (int i = 0; i < monitors.Count; i++)
            {
                var monitor = monitors[i];
                var index = i; // Нужно для корректной работы лямбда-выражения
                
                var monitorMenuItem = new MenuItem
                {
                    Header = $"{index + 1}. {monitor.Description} ({monitor.BrightnessPercentage}%)",
                    IsCheckable = true
                };
                
                monitorMenuItem.Click += (s, e) =>
                {
                    foreach (MenuItem item in _monitorsMenuItem.Items)
                    {
                        item.IsChecked = false;
                    }
                    monitorMenuItem.IsChecked = true;
                    MonitorSelected?.Invoke(this, index);
                };
                
                _monitorsMenuItem.Items.Add(monitorMenuItem);
            }
            
            // Если есть хотя бы один монитор, выбираем первый по умолчанию
            if (_monitorsMenuItem.Items.Count > 0)
            {
                ((MenuItem)_monitorsMenuItem.Items[0]).IsChecked = true;
            }
        }

        public void ShowNotification(string title, string message, BalloonIcon icon = BalloonIcon.Info)
        {
            _notifyIcon.ShowBalloonTip(title, message, icon);
        }

        public void UpdateBrightnessMenuItems(bool isEnabled)
        {
            _increaseMenuItem.IsEnabled = isEnabled;
            _decreaseMenuItem.IsEnabled = isEnabled;
        }

        public void Dispose()
        {
            _notifyIcon?.Dispose();
        }
    }
}