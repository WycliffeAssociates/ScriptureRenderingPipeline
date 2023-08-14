using System.Linq;
using ScriptureRenderingPipeline.Models;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace ScriptureRenderingPipeline.Helpers
{
    internal static class TranslationDatabaseInterface
    {
        public static async Task<TranslationDatabaseLanguage> GetLangagueAsync(string path, string languageCode)
        {
            var client = new HttpClient();
            var result = await client.GetAsync(path);
            var languages = JsonSerializer.Deserialize<TranslationDatabaseLanguage[]>(await result.Content.ReadAsStreamAsync());
            return languages.FirstOrDefault(l => l.LanguageCode == languageCode);
        }
    }
}
