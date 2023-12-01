using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PipelineCommon.Models;

namespace PipelineCommon.Helpers
{
    internal static class TranslationDatabaseInterface
    {
        public static async Task<TranslationDatabaseLanguage> GetLangagueAsync(string path, string languageCode)
        {
            var client = Utils.httpClient;
            var result = await client.GetAsync(path);
            var data = await result.Content.ReadAsStringAsync();
            var languages = JsonConvert.DeserializeObject<TranslationDatabaseLanguage[]>(data);
            return languages.FirstOrDefault(l => l.LanguageCode == languageCode);
        }
    }
}
