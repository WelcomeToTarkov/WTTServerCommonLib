using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using WTTServerCommonLib.Constants;
using WTTServerCommonLib.Helpers;
using WTTServerCommonLib.Models;
using WTTServerCommonLib.Services.ItemServiceHelpers;
using LogLevel = SPTarkov.Server.Core.Models.Spt.Logging.LogLevel;
using Path = System.IO.Path;

namespace WTTServerCommonLib.Services;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class WTTCustomItemServiceExtended(
    CustomItemService customItemService,
    DatabaseServer databaseServer,
    ISptLogger<WTTCustomItemServiceExtended> logger,
    JsonUtil jsonUtil,
    ModHelper modHelper)
{
    private readonly List<(string newItemId, CustomItemConfig config)> _deferredModSlotConfigs = new();
    private DatabaseTables? _database;

    public void CreateCustomItems(Assembly assembly, string? relativePath = null)
    {
        try
        {

            string assemblyLocation = modHelper.GetAbsolutePathToModFolder(assembly);
            string defaultDir = Path.Combine("db", "Items");
            string finalDir = Path.Combine(assemblyLocation, relativePath ?? defaultDir);
            
            if (_database == null)
            {
                _database = databaseServer.GetTables();
            }
            if (!Directory.Exists(finalDir))
            {
                logger.Log(LogLevel.Error, $"Config directory not found at {finalDir}");
                return;
            }

            var jsonFiles = Directory.GetFiles(finalDir, "*.json");
            if (!jsonFiles.Any())
            {
                logger.Log(LogLevel.Warn, $"No JSON config files found in {finalDir}");
                return;
            }

            int totalItemsCreated = 0;

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    totalItemsCreated += ProcessConfigFile(filePath);
                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Error, $"Error processing {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            logger.Log(LogLevel.Info, $"Created {totalItemsCreated} custom items from {jsonFiles.Length} files");
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, $"Error loading configs: {ex.Message}");
        }
    }

    private int ProcessConfigFile(string filePath)
    {
        string json = File.ReadAllText(filePath);
        Dictionary<string, CustomItemConfig>? config;

        try
        {
            config = jsonUtil.Deserialize<Dictionary<string, CustomItemConfig>>(json);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, $"Failed deserializing {Path.GetFileName(filePath)}: {ex.Message}");
            return 0;
        }

        if (config == null)
        {
            logger.Log(LogLevel.Warn, $"No valid items found in {Path.GetFileName(filePath)}");
            return 0;
        }

        int itemsCreated = 0;
        foreach (var (itemId, configData) in config)
        {
            configData.Validate();
            if (CreateItemFromConfig(itemId, configData))
                itemsCreated++;
        }

        return itemsCreated;
    }

    private bool CreateItemFromConfig(string newItemId, CustomItemConfig config)
    {
        try
        {
            var itemDetails = new NewItemFromCloneDetails
            {
                ItemTplToClone = NameHelper.ResolveId(config.ItemTplToClone, ItemMaps.ItemMap),
                ParentId = NameHelper.ResolveId(config.ParentId, ItemMaps.ItemBaseClassMap),
                NewId = newItemId,
                FleaPriceRoubles = config.FleaPriceRoubles,
                HandbookPriceRoubles = config.HandbookPriceRoubles,
                HandbookParentId = NameHelper.ResolveId(config.HandbookParentId, ItemMaps.ItemHandbookCategoryMap),
                Locales = config.Locales,
                OverrideProperties = config.OverrideProperties
            };

            customItemService.CreateItemFromClone(itemDetails);
            logger.Log(LogLevel.Info, $"Created item {newItemId}");

            ProcessAdditionalProperties(newItemId, config);

            return true;
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, $"Failed to create item {newItemId}: {ex.Message}");
            return false;
        }
    }

    private void ProcessAdditionalProperties(string newItemId, CustomItemConfig config)
    {
        if (_database == null)
        {
            return;
        }
        if (config is { AddToTraders: true, Traders: not null })
            TraderItemHelper.AddItem(config, newItemId, _database);

        if (config.AddWeaponPreset == true)
            WeaponPresetHelper.ProcessWeaponPresets(config, newItemId, _database);

        if (config is { Masteries: true, MasterySections: not null })
            MasteryHelper.AddOrUpdateMasteries(config.MasterySections, newItemId, _database);

        if (config.AddToModSlots == true)
            AddDeferredModSlot(newItemId, config);

        if (config.AddToInventorySlots != null)
            InventorySlotHelper.ProcessInventorySlots(config, newItemId, _database);

        if (config.AddToHallOfFame == true)
            HallOfFameHelper.AddToHallOfFame(config, newItemId, _database);

        if (config.AddToSpecialSlots == true)
            SpecialSlotsHelper.AddToSpecialSlots(config, newItemId, _database);

        if (config is { AddToStaticLootContainers: true, StaticLootContainers: not null })
            StaticLootHelper.ProcessStaticLootContainers(config, newItemId, _database);

        if (config.AddToBots == true)
        {
            // TODO: Add bot processing here
        }
        
        if (config is { AddToGeneratorAsFuel: true, GeneratorFuelSlotStages: not null })
            GeneratorFuelHelper.AddGeneratorFuel(config, newItemId, _database);
    }
    private void AddDeferredModSlot(string newItemId, CustomItemConfig config)
    {
        if (_deferredModSlotConfigs.Any(d => d.newItemId == newItemId))
        {
            logger.Log(LogLevel.Warn, $"Deferred modslot for {newItemId} already exists, skipping.");
            return;
        }
        _deferredModSlotConfigs.Add((newItemId, config));
    }
    public void ProcessDeferredModSlots()
    {
        if (_deferredModSlotConfigs.Count == 0)
        {
            logger.Log(LogLevel.Info, "No deferred modslots to process");
            return;
        }

        logger.Log(LogLevel.Info, $"Processing {_deferredModSlotConfigs.Count} deferred modslots...");

        foreach (var (newItemId, config) in _deferredModSlotConfigs)
        {
            try
            {
                if (_database == null)
                {
                    return;
                }
                ModslotHelper.ProcessModSlots(config, newItemId, _database);
                logger.Log(LogLevel.Debug, $"Processed modslots for {newItemId}");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, $"Failed processing modslots for {newItemId}: {ex.Message}");
            }
        }

        _deferredModSlotConfigs.Clear();
        
        logger.Log(LogLevel.Info, "Finished processing deferred modslots");
    }
}
