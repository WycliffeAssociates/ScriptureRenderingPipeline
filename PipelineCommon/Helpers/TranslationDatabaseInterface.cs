using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using PipelineCommon.Models;
using System.Text.Json;

namespace PipelineCommon.Helpers
{
    internal static class TranslationDatabaseInterface
    {
        public static async Task<TranslationDatabaseLanguage> GetLangagueAsync(string path, string languageCode)
        {
            var result = await Utils.httpClient.GetAsync(path);
            var data = await result.Content.ReadAsStringAsync();
            var languages = JsonSerializer.Deserialize(data, HelpersJsonContext.Default.TranslationDatabaseLanguageArray);
            return languages.FirstOrDefault(l => l.LanguageCode == languageCode);
        }
    }
}
