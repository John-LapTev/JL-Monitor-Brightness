using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using JL_Monitor_Brightness.Models;

namespace JL_Monitor_Brightness.Services
{
    public class UpdateService
    {
        private const string UpdateCheckUrl = "https://jl-studio.art/my_apps/JL-Monitor-Brightness/update_info.json";
        private readonly HttpClient _httpClient;
        
        public UpdateService()
        {
            _httpClient = new HttpClient();
            // Устанавливаем таймаут в 5 секунд
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }
        
        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(UpdateCheckUrl);
                var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(response);
                return updateInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking for updates: {ex.Message}");
                return null;
            }
        }
        
        public bool IsUpdateAvailable(UpdateInfo updateInfo, string currentVersion)
        {
            if (updateInfo == null)
                return false;
                
            return updateInfo.IsNewerVersion(currentVersion);
        }
        
        public void OpenInBrowser(string url)
        {
            try
            {
                // ⚠️ url приходит из update_info.json с сервера. При UseShellExecute
                // оболочка выполнит не только https://, но и file://, путь к .exe и любую
                // зарегистрированную схему — то есть чужой сервер получал бы запуск команды.
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                {
                    System.Diagnostics.Debug.WriteLine($"Отклонён небезопасный URL: {url}");
                    return;
                }

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening URL: {ex.Message}");
            }
        }
    }
}