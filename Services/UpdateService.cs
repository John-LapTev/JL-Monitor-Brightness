using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using JL_Monitor_Brightness.Models;

namespace JL_Monitor_Brightness.Services
{
    /// <summary>
    /// Проверка обновлений по GitHub Releases.
    ///
    /// ⛔ Раньше опрашивался файл `update_info.json` на стороннем сайте. К 25.08.2026
    /// этой страницы там не было вовсе — сервер отвечал 404, и проверка обновлений
    /// молча не находила ничего, включая уже вышедшую 1.1.0. Файл в репозитории при
    /// этом всё ещё описывал версию 1.0.0: его надо было обновлять руками при каждом
    /// релизе, и об этом забыли ровно один раз — навсегда.
    ///
    /// GitHub отдаёт последнюю версию и ссылки на файлы сам, из уже опубликованного
    /// релиза. Отдельный шаг выкладки исчезает, а вместе с ним и повод забыть.
    /// </summary>
    public class UpdateService
    {
        private const string LatestReleaseUrl =
            "https://api.github.com/repos/John-LapTev/JL-Monitor-Brightness/releases/latest";

        /// <summary>Страница релизов — куда отправить человека, если файлов в релизе нет.</summary>
        private const string ReleasesPage =
            "https://github.com/John-LapTev/JL-Monitor-Brightness/releases/latest";

        private readonly HttpClient _httpClient;

        public UpdateService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };

            // ⚠️ Без User-Agent GitHub отвечает 403 на любой запрос к API — это его
            // жёсткое требование, а не рекомендация.
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("JL-Monitor-Brightness", CurrentVersion()));
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }

        /// <summary>Версия из сборки — единственный источник, настройки её не хранят.</summary>
        private static string CurrentVersion()
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                string json = await _httpClient.GetStringAsync(LatestReleaseUrl);
                return ParseRelease(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking for updates: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Достаёт из ответа GitHub то, что нужно программе. Черновики и предрелизы
        /// пропускаются: `releases/latest` их и так не отдаёт, но проверка стоит строки.
        /// </summary>
        private static UpdateInfo ParseRelease(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
            if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;

            string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag)) return null;

            // Тег пишется как «v1.1.0», а Version.Parse ведущую букву не понимает.
            string version = tag.TrimStart('v', 'V');

            string installer = null, portable = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                    string url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    if (name == null || url == null) continue;

                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        if (name.Contains("Portable", StringComparison.OrdinalIgnoreCase)) portable = url;
                        else if (name.Contains("Setup", StringComparison.OrdinalIgnoreCase)) installer = url;
                    }
                }
            }

            // Файлов в релизе может не оказаться — тогда ведём на страницу релиза,
            // это лучше, чем неработающая кнопка «Скачать».
            installer ??= ReleasesPage;
            portable ??= ReleasesPage;

            string notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;

            return new UpdateInfo
            {
                LatestVersion = version,
                InstallerUrl = installer,
                PortableUrl = portable,
                ReleaseNotes = CleanNotes(notes, version),
            };
        }

        /// <summary>
        /// Описание релиза на GitHub размечено Markdown, а показывается в обычном
        /// TextBlock. Снимаем то, что иначе видно как мусор: решётки заголовков и
        /// звёздочки жирного.
        /// </summary>
        private static string CleanNotes(string notes, string version)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return $"Версия {version}";
            }

            var lines = notes.Replace("\r\n", "\n").Split('\n')
                .Select(l => l.TrimStart('#', ' ').Replace("**", "").Replace("`", ""))
                .Select(l => l.StartsWith("- ") ? "• " + l.Substring(2) : l);

            return string.Join("\n", lines).Trim();
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
                // ⚠️ Ссылка приходит извне. При UseShellExecute оболочка выполнит не
                // только https://, но и file://, путь к .exe и любую зарегистрированную
                // схему — то есть чужой ответ получал бы запуск команды.
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
