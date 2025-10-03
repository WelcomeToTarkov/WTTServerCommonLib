using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Models.Spt.Server;
using WTTServerCommonLib.Helpers;

namespace WTTServerCommonLib.Services;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class WTTLocaleService(DatabaseServer databaseServer, ModHelper modHelper)
{
    private DatabaseTables? _database;
    

    public void ProcessLocales(Assembly assembly, string? relativePath = null)
    {

        _database = databaseServer.GetTables();
        string assemblyLocation = modHelper.GetAbsolutePathToModFolder(assembly);
        string defaultDir = Path.Combine("db", "locales");
        string finalDir = Path.Combine(assemblyLocation, relativePath ?? defaultDir);

        if (!Directory.Exists(finalDir))
        {
            Log.Warn($"Locale directory not found: {finalDir}");
            return;
        }

        var customLocales = LoadLocalesFromDirectory(finalDir);

        var fallback = customLocales.TryGetValue("en", out var locale) ? locale : new Dictionary<string, string>();

        foreach (var (localeCode, lazyLocale) in _database.Locales.Global)
        {
            var localeData = lazyLocale.Value;
            var customLocale = customLocales.GetValueOrDefault(localeCode, fallback);

            foreach (var (key, value) in customLocale)
            {
                if (localeData != null)
                    localeData.TryAdd(key, value);
            }
        }

        Log.Info($"WTTLocaleService: Merged {customLocales.Count} locale files.");
    }

    private Dictionary<string, Dictionary<string, string>> LoadLocalesFromDirectory(string directoryPath)
    {
        var locales = new Dictionary<string, Dictionary<string, string>>();

        foreach (var filePath in Directory.GetFiles(directoryPath, "*.json"))
        {
            var localeCode = Path.GetFileNameWithoutExtension(filePath);

            try
            {
                var json = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (data != null)
                {
                    locales[localeCode] = data;
                    Log.Info($"Loaded locale file: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to parse {filePath}: {ex.Message}");
            }
        }

        return locales;
    }
}
