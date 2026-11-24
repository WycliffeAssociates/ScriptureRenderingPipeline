using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using PipelineCommon.Models;

namespace PipelineCommon.Helpers
{
    public static class TranslationDatabaseInterface
    {
        /// <summary>
        /// The language list is about 1.5MB and rarely changes so keep it around instead of downloading it for every lookup
        /// </summary>
        private static readonly MemoryCache Cache = new(new MemoryCacheOptions());
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

        public static Task<TranslationDatabaseLanguage?> GetLangagueAsync(string path, string languageCode)
        {
            return GetLanguageAsync(path, languageCode, () => DownloadLanguagesAsync(path));
        }

        /// <summary>
        /// Look up a language, only calling download when the list for this path isn't cached
        /// </summary>
        /// <param name="path">The URL of the language list, also used as the cache key</param>
        /// <param name="languageCode">The language code to look up</param>
        /// <param name="download">Gets the language list when it isn't cached</param>
        /// <returns>The language or null if it isn't in the list</returns>
        /// <remarks>If download throws nothing is cached so the next lookup tries again</remarks>
        internal static async Task<TranslationDatabaseLanguage?> GetLanguageAsync(string path, string languageCode,
            Func<Task<TranslationDatabaseLanguage[]>> download)
        {
            var languages = await Cache.GetOrCreateAsync(path, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return BuildLookup(await download());
            });
            return languages!.GetValueOrDefault(languageCode);
        }

        private static async Task<TranslationDatabaseLanguage[]> DownloadLanguagesAsync(string path)
        {
            return await Utils.httpClient.GetFromJsonAsync(path, HelpersJsonContext.Default.TranslationDatabaseLanguageArray)
                   ?? throw new InvalidDataException($"Language list at {path} was empty");
        }

        private static Dictionary<string, TranslationDatabaseLanguage> BuildLookup(TranslationDatabaseLanguage[] languages)
        {
            var output = new Dictionary<string, TranslationDatabaseLanguage>(languages.Length);
            foreach (var language in languages)
            {
                // Keep the first entry for a code, which is what the old linear search returned
                if (language.LanguageCode != null)
                {
                    output.TryAdd(language.LanguageCode, language);
                }
            }
            return output;
        }
    }
}
