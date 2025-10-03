using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using WTTServerCommonLib.Helpers;
using WTTServerCommonLib.Models;
using Path = System.IO.Path;

namespace WTTServerCommonLib.Services;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class WTTCustomQuestService(
    DatabaseServer databaseServer,
    ConfigServer cfgServer,
    ImageRouter imageRouter,
    ModHelper modHelper)
{
    private DatabaseTables _database = null!;

    public void AddCustomQuests(Assembly assembly, string? relativePath = null)
    {
        
        string assemblyLocation = modHelper.GetAbsolutePathToModFolder(assembly);
        string defaultDir = Path.Combine("db", "Quests");
        string finalDir = Path.Combine(assemblyLocation, relativePath ?? defaultDir);
        _database = databaseServer.GetTables();

        ImportQuestSideConfig(finalDir);
        LoadAllTraderQuests(finalDir);
    }

    private void LoadAllTraderQuests(string basePath)
    {
        if (!Directory.Exists(basePath))
        {
            Log.Warn($"Quest base directory not found: {basePath}");
            return;
        }

        foreach (var traderDir in Directory.GetDirectories(basePath))
        {
            string traderKey = Path.GetFileName(traderDir);

            if (!TraderIds.TraderMap.TryGetValue(traderKey, out var traderId))
            {
                if (traderKey.IsValidMongoId())
                {
                    traderId = traderKey;
                }
                else
                {
                    Log.Warn($"Unknown trader key '{traderKey}'");
                    continue;
                }
            }

            LoadQuestsFromDirectory(traderId, traderDir);
        }

    }

    private void LoadQuestsFromDirectory(string traderId, string questsBasePath)
    {
        string traderBasePath = Path.Combine(questsBasePath, traderId);

        var questFiles  = LoadJsonFiles<Dictionary<string, Quest>>(traderBasePath);
        var assortFiles = LoadJsonFiles<Dictionary<string, Dictionary<string, string>>>(Path.Combine(traderBasePath, "questAssort"));
        var imageFiles  = LoadImageFiles(Path.Combine(traderBasePath, "images"));

        ImportQuestData(questFiles, traderId);
        ImportQuestAssortData(assortFiles, traderId);
        ImportLocaleData(traderId, questsBasePath);
        ImportImageData(imageFiles, traderId);
    }

    private List<T> LoadJsonFiles<T>(string directoryPath)
    {
        var result = new List<T>();

        if (!Directory.Exists(directoryPath)) return result;

        foreach (var filePath in Directory.GetFiles(directoryPath, "*.json"))
        {
            try
            {
                string content = File.ReadAllText(filePath);
                var jsonData = JsonSerializer.Deserialize<T>(content);
                if (jsonData != null)
                {
                    result.Add(jsonData);
                    Log.Info($"Loaded JSON file: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error loading JSON file {filePath}: {ex.Message}");
            }
        }

        return result;
    }

    private readonly string[] _validImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif"];

    private List<string> LoadImageFiles(string directoryPath)
    {
        return Directory.Exists(directoryPath)
            ? Directory.GetFiles(directoryPath)
                .Where(f => _validImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList()
            : new List<string>();
    }

    private void ImportQuestData(List<Dictionary<string, Quest>> questFiles, string traderId)
    {
        if (!questFiles.Any())
        {
            Log.Warn($"{traderId}: No quest files found");
            return;
        }

        int questCount = 0;
        foreach (var file in questFiles)
        {
            foreach (var (key, quest) in file)
            {
                _database.Templates.Quests[key] = quest;
                questCount++;
            }
        }

        Log.Info($"{traderId}: Loaded {questCount} quests");
    }

    private void ImportQuestAssortData(List<Dictionary<string, Dictionary<string, string>>> assortFiles, string traderId)
    {
        if (!assortFiles.Any())
        {
            Log.Warn($"{traderId}: No quest assort files found");
            return;
        }

        int assortCount = 0;
        foreach (var file in assortFiles)
        {
            foreach (var (questId, assortDict) in file)
            {
                var trader = _database.Traders.GetValueOrDefault(traderId);
                if (trader == null)
                {
                    Log.Warn($"Trader {traderId} not found in database.");
                    continue;
                }

                if (trader.QuestAssort == null)
                {
                    Log.Warn($"Trader Quest Assort is null for trader {traderId}");
                    continue;
                }
                
                if (!trader.QuestAssort.ContainsKey(questId))
                {
                    trader.QuestAssort[questId] = new Dictionary<MongoId, MongoId>();
                }

                foreach (var (key, value) in assortDict)
                {
                    trader.QuestAssort[questId][key] = value;
                    assortCount++;
                }

            }
        }

        Log.Info($"{traderId}: Loaded {assortCount} quest assort items");
    }

    private void ImportLocaleData(string traderId, string questsBasePath)
    {
        string localesPath = Path.Combine(questsBasePath, traderId, "locales");
        if (!Directory.Exists(localesPath))
        {
            Log.Warn($"{traderId}: No locales directory found");
            return;
        }

        var localeFiles = Directory.GetFiles(localesPath, "*.json");
        if (!localeFiles.Any())
        {
            Log.Warn($"{traderId}: No locale files found");
            return;
        }

        var locales = new Dictionary<string, Dictionary<string, string>>();
        Dictionary<string, string>? fallback = null;

        foreach (var file in localeFiles)
        {
            try
            {
                string localeCode = Path.GetFileNameWithoutExtension(file);
                string content = File.ReadAllText(file);
                var localeData = JsonSerializer.Deserialize<Dictionary<string, string>>(content) ?? new();

                locales[localeCode] = localeData;
                if (localeCode.Equals("en", StringComparison.OrdinalIgnoreCase))
                {
                    fallback = localeData;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error loading locale file {file}: {ex.Message}");
            }
        }

        fallback ??= locales.Values.FirstOrDefault() ?? new();

        foreach (var (localeCode, lazyLocale) in _database.Locales.Global)
        {
            var localeData = lazyLocale.Value;
            if (locales.TryGetValue(localeCode, out var customLocales))
            {
                foreach (var (key, value) in customLocales)
                {
                    if (localeData != null)
                        localeData.TryAdd(key, value);
                }
            }
            else
            {
                foreach (var (key, value) in fallback)
                {
                    if (localeData != null)
                        localeData.TryAdd(key, value);
                }
            }
        }

        Log.Info($"{traderId}: Loaded {locales.Count} locale files");
    }

    private void ImportImageData(List<string> imageFiles, string traderId)
    {
        foreach (var imagePath in imageFiles)
        {
            string imageName = Path.GetFileNameWithoutExtension(imagePath);
            imageRouter.AddRoute($"/files/quest/icon/{imageName}", imagePath);
        }
        Log.Info($"{traderId}: Loaded {imageFiles.Count} images");
    }

    private void ImportQuestSideConfig(string questsBasePath)
    {
        try
        {
            string configPath = Path.Combine(questsBasePath, "QuestSideData.json");
            if (!File.Exists(configPath)) return;

            string content = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<QuestConfig>(content);

            var questConfig = cfgServer.GetConfig<QuestConfig>();
            if (config != null)
            {
                foreach (var questId in config.UsecOnlyQuests)
                {
                    questConfig.UsecOnlyQuests.Add(questId);
                }
            
                foreach (var questId in config.BearOnlyQuests)
                {
                    questConfig.BearOnlyQuests.Add(questId);
                }

                Log.Info("Loaded QuestSideData.json");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error loading QuestSideData.json: {ex.Message}");
        }
    }
}
