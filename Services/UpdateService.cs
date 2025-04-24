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
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
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