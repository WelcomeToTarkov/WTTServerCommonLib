using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Utils;
using WTTServerCommonLib.Helpers;

namespace WTTServerCommonLib.Services;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class WTTLocaleService(DatabaseServer databaseServer, ModHelper modHelper, JsonUtil jsonUtil)
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

        var customLocales = ConfigHelper.LoadLocalesFromDirectory(finalDir, jsonUtil);

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

}
