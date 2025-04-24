using System;
using System.Text.Json.Serialization;

namespace JL_Monitor_Brightness.Models
{
    public class UpdateInfo
    {
        [JsonPropertyName("latest_version")]
        public string LatestVersion { get; set; }

        [JsonPropertyName("installer_url")]
        public string InstallerUrl { get; set; }

        [JsonPropertyName("portable_url")]
        public string PortableUrl { get; set; }

        [JsonPropertyName("release_notes")]
        public string ReleaseNotes { get; set; }
        
        public bool IsNewerVersion(string currentVersion)
        {
            if (string.IsNullOrEmpty(LatestVersion) || string.IsNullOrEmpty(currentVersion))
                return false;
                
            try
            {
                var latest = Version.Parse(LatestVersion);
                var current = Version.Parse(currentVersion);
                
                return latest > current;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error comparing versions: {ex.Message}");
                return false;
            }
        }
    }
}