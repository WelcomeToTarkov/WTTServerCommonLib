using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using WTTServerCommonLib.Helpers;
using WTTServerCommonLib.Models;
using Path = System.IO.Path;

namespace WTTServerCommonLib.Services;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class WTTCustomAssortSchemeService(
    DatabaseServer databaseServer,
    ISptLogger<WTTCustomItemServiceExtended> logger,
    JsonUtil jsonUtil,
    ModHelper modHelper)
{
    private readonly List<Dictionary<string, TraderAssort>> _customAssortSchemes = new();

    public void AddCustomAssortSchemes(Assembly assembly, string? relativePath = null)
    {
        string assemblyLocation = modHelper.GetAbsolutePathToModFolder(assembly);
        string defaultDir = Path.Combine("db", "CustomAssortSchemes");
        string finalDir = Path.Combine(assemblyLocation, relativePath ?? defaultDir);
        
        if (!Directory.Exists(finalDir))
        {
            throw new DirectoryNotFoundException($"Config directory not found at {finalDir}");
        }

        var jsonFiles = Directory.GetFiles(finalDir, "*.json")
            .Concat(Directory.GetFiles(finalDir, "*.jsonc"))
            .ToArray();
        if (jsonFiles.Length == 0)
        {
            Log.Warn($"No assort scheme files found in {finalDir}");
            return;
        }

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var assortData = jsonUtil.Deserialize<Dictionary<string, TraderAssort>>(json);

                if (assortData != null)
                {
                    _customAssortSchemes.Add(assortData);
                    Log.Info($"Loaded {assortData.Count} trader assort(s) from {Path.GetFileName(file)}");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to load assort file '{file}': {ex.Message}");
            }
        }

        ApplyAssorts();
    }

    private void ApplyAssorts()
    {
        DatabaseTables tables = databaseServer.GetTables();

        foreach (var schemeDict in _customAssortSchemes)
        {
            foreach (var kvp in schemeDict)
            {
                var traderKey = kvp.Key;
                var newAssort = kvp.Value;

                if (!TraderIds.TraderMap.TryGetValue(traderKey, out var traderId))
                {
                    Log.Warn($"Unknown trader key '{traderKey}'");
                    continue;
                }

                if (!tables.Traders.TryGetValue(traderId, out var trader))
                {
                    Log.Warn($"Trader not found in DB: ({traderId})");
                    continue;
                }

                trader.Assort.Items.AddRange(newAssort.Items);

                foreach (var scheme in newAssort.BarterScheme)
                {
                    trader.Assort.BarterScheme[scheme.Key] = scheme.Value;
                }

                foreach (var levelItem in newAssort.LoyalLevelItems)
                {
                    trader.Assort.LoyalLevelItems[levelItem.Key] = levelItem.Value;
                }

                Log.Info($"Merged {newAssort.Items.Count} items into trader");
            }
        }
    }
}
