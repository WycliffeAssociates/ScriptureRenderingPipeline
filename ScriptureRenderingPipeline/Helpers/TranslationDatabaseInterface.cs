using Newtonsoft.Json;
using ScriptureRenderingPipeline.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ScriptureRenderingPipeline.Helpers
{
    internal static class TranslationDatabaseInterface
    {
        public static async Task<TranslationDatabaseLanguage> GetLangagueAsync(string path, string languageCode)
        {
            var client = new HttpClient();
            var result = await client.GetAsync(path);
            var data = await result.Content.ReadAsStringAsync();
            var languages = JsonConvert.DeserializeObject<TranslationDatabaseLanguage[]>(data);
            return languages.FirstOrDefault(l => l.LanguageCode == languageCode);
        }
    }
}
