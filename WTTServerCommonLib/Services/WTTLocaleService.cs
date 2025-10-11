using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using WTTServerCommonLib.Helpers;

namespace WTTServerCommonLib.Services;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class WTTLocaleService(
    ISptLogger<WTTLocaleService> logger, 
    DatabaseServer databaseServer, 
    ModHelper modHelper, 
    ConfigHelper configHelper
    )
{
    private DatabaseTables? _database;
    

    public void ProcessLocales(Assembly assembly, string? relativePath = null)
    {
        _database = databaseServer.GetTables();
        string assemblyLocation = modHelper.GetAbsolutePathToModFolder(assembly);
        string defaultDir = Path.Combine("db", "CustomLocales");
        string finalDir = Path.Combine(assemblyLocation, relativePath ?? defaultDir);

        if (!Directory.Exists(finalDir)) 
        {
            logger.Warning($"Locale directory not found: {finalDir}");
            return;
        }

        var customLocales = configHelper.LoadLocalesFromDirectory(finalDir);
        
        if (!customLocales.Any())
        {
            logger.Warning("No custom locale files found or loaded");
            return;
        }

        var fallback = customLocales.TryGetValue("en", out var locale) ? locale : customLocales.Values.FirstOrDefault();

        if (fallback == null)
        {
            logger.Warning("No valid fallback locale found");
            return;
        }

        foreach (var (localeCode, lazyLocale) in _database.Locales.Global)
        {
            lazyLocale.AddTransformer(localeData =>
            {
                if (localeData is null)
                {
                    return localeData;
                }

                var customLocale = customLocales.GetValueOrDefault(localeCode, fallback);

                foreach (var (key, value) in customLocale)
                {
                    localeData[key] = value;
                }

                return localeData;
            });
        }

        logger.Info($"WTTLocaleService: Registered transformers for {customLocales.Count} locale files");
    }
}