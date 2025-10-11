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
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using WTTServerCommonLib.Helpers;
using WTTServerCommonLib.Models;
using Path = System.IO.Path;

namespace WTTServerCommonLib.Services;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class WTTCustomQuestService(
    ISptLogger<WTTCustomQuestService> logger,
    DatabaseServer databaseServer,
    ConfigServer cfgServer,
    ImageRouter imageRouter,
    ModHelper modHelper,
    ConfigHelper configHelper,
    JsonUtil jsonUtil)
{
    private DatabaseTables _database = null!;

    public void AddCustomQuests(Assembly assembly, string? relativePath = null)
    {
        string assemblyLocation = modHelper.GetAbsolutePathToModFolder(assembly);
        string defaultDir = Path.Combine("db", "CustomQuests");
        string finalDir = Path.Combine(assemblyLocation, relativePath ?? defaultDir);
        _database = databaseServer.GetTables();

        ImportQuestSideConfig(finalDir);
        LoadAllTraderQuests(finalDir);
    }

    private void LoadAllTraderQuests(string basePath)
    {
        if (!Directory.Exists(basePath))
        {
            logger.Warning($"Quest base directory not found: {basePath}");
            return;
        }

        var directories = Directory.GetDirectories(basePath);

        foreach (var traderDir in directories)
        {
            string traderKey = Path.GetFileName(traderDir);
            string traderId;
            if (TraderIds.TraderMap.TryGetValue(traderKey.ToLower(), out var mappedTraderId))
            {
                traderId = mappedTraderId;
                logger.Debug($"Mapped trader key '{traderKey}' to ID '{traderId}'");
            }
            else if (traderKey.IsValidMongoId())
            {
                traderId = traderKey;
                logger.Debug($"Using trader key '{traderKey}' as direct ID");
            }
            else
            {
                logger.Warning($"Unknown trader key '{traderKey}' and not a valid Mongo ID");
                continue;
            }

            LoadQuestsFromDirectory(traderId, traderDir);
        }
    }

    private void LoadQuestsFromDirectory(string traderId, string traderDir)
    {
        logger.Info($"Loading quests for trader {traderId} from {traderDir}");

        var questFiles = LoadQuestFiles(traderDir);
        var assortFiles = LoadAssortFiles(traderDir);
        var imageFiles = LoadImageFiles(Path.Combine(traderDir, "Images"));

        ImportQuestData(questFiles, traderId);
        ImportQuestAssortData(assortFiles, traderId);
        ImportLocaleData(traderId, traderDir);
        ImportImageData(imageFiles, traderId);
    }

    private List<Dictionary<string, Quest>> LoadQuestFiles(string traderDir)
    {
        var result = new List<Dictionary<string, Quest>>();
        
        try
        {
            var jsonFiles = Directory.GetFiles(traderDir, "*.json")
                .Concat(Directory.GetFiles(traderDir, "*.jsonc"))
                .ToArray();

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var questData = jsonUtil.DeserializeFromFile<Dictionary<string, Quest>>(filePath);
                    if (questData != null && questData.Any())
                    {
                        result.Add(questData);
                        logger.Info($"Loaded quest file: {Path.GetFileName(filePath)} with {questData.Count} quests");
                        continue;
                    }

                    var singleQuest = jsonUtil.DeserializeFromFile<Quest>(filePath);
                    if (singleQuest != null)
                    {
                        var questId = Path.GetFileNameWithoutExtension(filePath);
                        if (questId.IsValidMongoId())
                        {
                            result.Add(new Dictionary<string, Quest> { { questId, singleQuest } });
                            logger.Info($"Loaded single quest file: {Path.GetFileName(filePath)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to load quest file {filePath}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error scanning for quest files in {traderDir}: {ex.Message}");
        }

        return result;
    }

    private List<Dictionary<string, Dictionary<MongoId, MongoId>>> LoadAssortFiles(string traderDir)
    {
        var result = new List<Dictionary<string, Dictionary<MongoId, MongoId>>>();
        var assortDir = Path.Combine(traderDir, "QuestAssort");

        if (!Directory.Exists(assortDir))
        {
            return result;
        }

        try
        {
            var jsonFiles = Directory.GetFiles(assortDir, "*.json")
                .Concat(Directory.GetFiles(assortDir, "*.jsonc"))
                .ToArray();

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var assortData = jsonUtil.DeserializeFromFile<Dictionary<string, Dictionary<MongoId, MongoId>>>(filePath);
                    if (assortData != null && assortData.Any())
                    {
                        result.Add(assortData);
                        logger.Info($"Loaded assort file: {Path.GetFileName(filePath)}");
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to load assort file {filePath}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error scanning for assort files in {assortDir}: {ex.Message}");
        }

        return result;
    }

    private readonly string[] _validImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif"];

    private List<string> LoadImageFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return new List<string>();
        }

        try
        {
            var images = Directory.GetFiles(directoryPath)
                .Where(f => _validImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();
            
            logger.Info($"Found {images.Count} image files in {directoryPath}");
            return images;
        }
        catch (Exception ex)
        {
            logger.Error($"Error loading images from {directoryPath}: {ex.Message}");
            return new List<string>();
        }
    }

    private void ImportQuestData(List<Dictionary<string, Quest>> questFiles, string traderId)
    {
        if (!questFiles.Any())
        {
            logger.Warning($"{traderId}: No quest files found or loaded");
            return;
        }

        int questCount = 0;
        foreach (var file in questFiles)
        {
            foreach (var (key, quest) in file)
            {
                if (!key.IsValidMongoId())
                {
                    logger.Warning($"{traderId}: Invalid quest ID '{key}', skipping");
                    continue;
                }

                _database.Templates.Quests[key] = quest;
                questCount++;
                logger.Debug($"{traderId}: Added quest {key}");
            }
        }

        logger.Info($"{traderId}: Successfully loaded {questCount} quests");
    }

    private void ImportQuestAssortData(List<Dictionary<string, Dictionary<MongoId, MongoId>>> assortFiles, string traderId)
    {
        if (!assortFiles.Any())
        {
            logger.Warning($"{traderId}: No quest assort files found");
            return;
        }

        var trader = _database.Traders.GetValueOrDefault(traderId);
        if (trader == null)
        {
            logger.Warning($"Trader {traderId} not found in database, cannot import quest assort");
            return;
        }

        int assortCount = 0;
        foreach (var questAssort in assortFiles)
        {
            foreach (var (stage, questAssortDict) in questAssort)
            {
                if (!trader.QuestAssort.ContainsKey(stage))
                {
                    trader.QuestAssort[stage] = new Dictionary<MongoId, MongoId>();
                }

                foreach (var (questId, assortId) in questAssortDict)
                {
                    trader.QuestAssort[stage][questId] = assortId;
                    assortCount++;
                    logger.Debug($"{traderId}: Added assort for quest {questId} in stage {stage}");
                }
            }
        }

        logger.Info($"{traderId}: Loaded {assortCount} quest assort items");
    }
    private void ImportLocaleData(string traderId, string traderDir)
    {
        string localesPath = Path.Combine(traderDir, "Locales");
    
        try
        {
            var locales = configHelper.LoadLocalesFromDirectory(localesPath);
    
            if (!locales.Any())
            {
                logger.Warning($"{traderId}: No locale files found or loaded from {localesPath}");
                return;
            }

            Dictionary<string, string>? fallback = locales.TryGetValue("en", out var englishLocales) ? englishLocales : locales.Values.FirstOrDefault();

            if (fallback == null) return;

            foreach (var (localeCode, lazyLocale) in _database.Locales.Global)
            {
                lazyLocale.AddTransformer(localeData =>
                {
                    if (localeData is null)
                    {
                        return localeData;
                    }

                    var customLocale = locales.GetValueOrDefault(localeCode, fallback);

                    foreach (var (key, value) in customLocale)
                    {
                        localeData[key] = value;
                    }

                    return localeData;
                });
            }

            logger.Info($"{traderId}: Registered transformers for {locales.Count} quest locale files");
        }
        catch (Exception ex)
        {
            logger.Error($"{traderId}: Error loading quest locales: {ex.Message}");
        }
    }

    private void ImportImageData(List<string> imageFiles, string traderId)
    {
        if (!imageFiles.Any())
        {
            logger.Warning($"{traderId}: No images found");
            return;
        }

        foreach (var imagePath in imageFiles)
        {
            try
            {
                string imageName = Path.GetFileNameWithoutExtension(imagePath);
                imageRouter.AddRoute($"/files/quest/icon/{imageName}", imagePath);
                logger.Debug($"{traderId}: Registered image route for {imageName}");
            }
            catch (Exception ex)
            {
                logger.Warning($"{traderId}: Failed to register image {imagePath}: {ex.Message}");
            }
        }
        
        logger.Info($"{traderId}: Loaded {imageFiles.Count} images");
    }

    private void ImportQuestSideConfig(string questsBasePath)
    {
        try
        {
            string configPath = Path.Combine(questsBasePath, "QuestSideData.json");
            if (!File.Exists(configPath))
            {
                logger.Info("No QuestSideData.json found, skipping");
                return;
            }

            string content = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<CustomQuestSideConfig>(content);

            var questConfig = cfgServer.GetConfig<QuestConfig>();
            if (config == null)
            {
                logger.Warning("QuestSideData.json is empty or invalid");
                return;
            }

            int usecAdded = 0;
            int bearAdded = 0;
            
            if (config.UsecOnlyQuests.Count > 0)
            {
                foreach (var questId in config.UsecOnlyQuests)
                {
                    if (questId.IsValidMongoId())
                    {
                        questConfig.UsecOnlyQuests.Add(questId);
                        usecAdded++;
                    }
                    else
                    {
                        logger.Warning($"Invalid USEC quest ID in QuestSideData.json: {questId}");
                    }
                }
            }

            if (config.BearOnlyQuests.Count > 0)
            {
                foreach (var questId in config.BearOnlyQuests)
                {
                    if (questId.IsValidMongoId())
                    {
                        questConfig.BearOnlyQuests.Add(questId);
                        bearAdded++;
                    }
                    else
                    {
                        logger.Warning($"Invalid BEAR quest ID in QuestSideData.json: {questId}");
                    }
                }
            }

            logger.Info($"Loaded QuestSideData.json: {usecAdded} USEC quests, {bearAdded} BEAR quests");
        }
        catch (Exception ex)
        {
            logger.Critical("Error loading QuestSideData.json", ex);
        }
    }
}