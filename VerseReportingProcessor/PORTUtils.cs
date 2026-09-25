using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace VerseReportingProcessor;

/// <summary>
/// Shared lookups against PORT (Dataverse).
/// </summary>
public static class PORTUtils
{
    /// <summary>
    /// Finds the <c>wa_language</c> record whose IETF tag matches <paramref name="languageCode"/>.
    /// </summary>
    /// <returns>A reference to the language, or null if no match was found.</returns>
    public static async Task<EntityReference?> GetLanguageFromCode(IOrganizationServiceAsync service, string? languageCode, ILogger logger)
    {
        var query = new QueryExpression("wa_language")
        {
            ColumnSet = new ColumnSet("wa_languageid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("wa_ietftag", ConditionOperator.Equal, languageCode)
                }
            }
        };
        var language = (await service.RetrieveMultipleAsync(query)).Entities.FirstOrDefault();
        if (language == null)
        {
            logger.LogError("Language {Language} not found", languageCode);
            return null;
        }

        return language.ToEntityReference();
    }
}
