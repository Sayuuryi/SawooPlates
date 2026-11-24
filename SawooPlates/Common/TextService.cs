using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace SawooPlates.Common;

public sealed class TextService : IDisposable
{
    private const string FallbackLanguage = "en";

    private readonly ExcelService _excelService;
    private readonly Dictionary<string, Dictionary<string, string>> _translations;

    public TextService(ExcelService excelService, IPluginLog log)
    {
        _excelService = excelService;
        _translations = LoadTranslations(log);
    }

    public string LanguageCode => FallbackLanguage;

    public CultureInfo CultureInfo => CultureInfo.InvariantCulture;

    public void Dispose()
    {
    }

    private static Dictionary<string, Dictionary<string, string>> LoadTranslations(IPluginLog log)
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SawooPlates.Translations.json");
            if (stream == null)
            {
                log.Error("Translations.json resource not found");
                return [];
            }

            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream) ?? [];
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not load translations");
            return [];
        }
    }

    public bool TryGetTranslation(string key, [MaybeNullWhen(false)] out string text)
    {
        text = null;
        return _translations.TryGetValue(key, out var entry)
            && (entry.TryGetValue(LanguageCode, out text) || entry.TryGetValue(FallbackLanguage, out text));
    }

    public string Translate(string key)
        => TryGetTranslation(key, out var text) ? text : key;

    public string Translate(string key, params object?[] args)
        => TryGetTranslation(key, out var text) ? string.Format(CultureInfo, text, args) : key;

    /// <summary>Text from the game's Addon sheet in the client language.</summary>
    public string GetAddonText(uint id)
        => _excelService.TryGetRow<Addon>(id, out var row) ? row.Text.ToString() : string.Empty;
}
