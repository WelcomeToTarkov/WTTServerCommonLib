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
    ISptLogger<WTTCustomItemServiceExtended> logger,
    CustomItemService customItemService,
    DatabaseServer databaseServer,
    JsonUtil jsonUtil,
    ModHelper modHelper,
    WeaponPresetHelper weaponPresetHelper,
    TraderItemHelper traderItemHelper,
    StaticLootHelper staticLootHelper,
    SpecialSlotsHelper specialSlotsHelper,
    PosterLootHelper posterLootHelper,
    ModSlotHelper modSlotHelper,
    MasteryHelper masteryHelper,
    InventorySlotHelper inventorySlotHelper,
    HideoutStatuetteHelper hideoutStatuetteHelper,
    HideoutPosterHelper  hideoutPosterHelper,
    HallOfFameHelper hallOfFameHelper,
    GeneratorFuelHelper  generatorFuelHelper
    )
{
    private readonly List<(string newItemId, CustomItemConfig config)> _deferredModSlotConfigs = new();
    private DatabaseTables? _database;

    public void CreateCustomItems(Assembly assembly, string? relativePath = null)
    {
        try
        {
            string assemblyLocation = modHelper.GetAbsolutePathToModFolder(assembly);
            string defaultDir = Path.Combine("db", "CustomItems");
            string finalDir = Path.Combine(assemblyLocation, relativePath ?? defaultDir);
            
            if (_database == null)
            {
                _database = databaseServer.GetTables();
            }
            if (!Directory.Exists(finalDir))
            {
                logger.Error( $"directory not found at {finalDir}");
                return;
            }

            var jsonFiles = Directory.GetFiles(finalDir, "*.json", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(finalDir, "*.jsonc", SearchOption.AllDirectories))
                .ToArray();
            if (!jsonFiles.Any())
            {
                logger.Warning( $"No JSON config files found in {finalDir}");
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
                    logger.Error( $"Error processing {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            logger.Info( $"Created {totalItemsCreated} custom items from {jsonFiles.Length} files");
        }
        catch (Exception ex)
        {
            logger.Error( $"Error loading configs: {ex.Message}");
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
            logger.Error( $"Failed deserializing {Path.GetFileName(filePath)}: {ex.Message}");
            return 0;
        }

        if (config == null)
        {
            logger.Warning( $"No valid items found in {Path.GetFileName(filePath)}");
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
                ItemTplToClone = ItemTplResolver.ResolveId(config.ItemTplToClone),
                ParentId = NameHelper.ResolveId(config.ParentId, ItemMaps.ItemBaseClassMap),
                NewId = newItemId,
                FleaPriceRoubles = config.FleaPriceRoubles,
                HandbookPriceRoubles = config.HandbookPriceRoubles,
                HandbookParentId = NameHelper.ResolveId(config.HandbookParentId, ItemMaps.ItemHandbookCategoryMap),
                Locales = config.Locales,
                OverrideProperties = config.OverrideProperties
            };

            customItemService.CreateItemFromClone(itemDetails);
            logger.Info( $"Created item {newItemId}");

            ProcessAdditionalProperties(newItemId, config);

            return true;
        }
        catch (Exception ex)
        {
            logger.Error( $"Failed to create item {newItemId}: {ex.Message}");
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
            traderItemHelper.AddItem(config, newItemId);

        if (config.AddWeaponPreset == true)
            weaponPresetHelper.ProcessWeaponPresets(config, newItemId);

        if (config is { Masteries: true, MasterySections: not null })
            masteryHelper.AddOrUpdateMasteries(config.MasterySections, newItemId);

        if (config.AddToModSlots == true)
            AddDeferredModSlot(newItemId, config);

        if (config.AddToInventorySlots != null)
            inventorySlotHelper.ProcessInventorySlots(config, newItemId);

        if (config.AddToHallOfFame == true)
            hallOfFameHelper.AddToHallOfFame(config, newItemId);

        if (config.AddToSpecialSlots == true)
            specialSlotsHelper.AddToSpecialSlots(config, newItemId);

        if (config is { AddToStaticLootContainers: true, StaticLootContainers: not null })
            staticLootHelper.ProcessStaticLootContainers(config, newItemId);

        if (config.AddToBots == true)
        {
            // TODO: Add bot processing here
        }
        
        if (config is { AddToGeneratorAsFuel: true, GeneratorFuelSlotStages: not null })
            generatorFuelHelper.AddGeneratorFuel(config, newItemId);
        
        if (config.AddToHideoutPosterSlots == true)
            hideoutPosterHelper.AddToPosterSlot(newItemId);

        if (config is { AddPosterToMaps: true, PosterSpawnProbability: not null })
            posterLootHelper.ProcessPosterLoot(config, newItemId);

        if (config.AddToStatuetteSlots == true)
            hideoutStatuetteHelper.AddToStatuetteSlot(newItemId);
    }
    private void AddDeferredModSlot(string newItemId, CustomItemConfig config)
    {
        if (_deferredModSlotConfigs.Any(d => d.newItemId == newItemId))
        {
            logger.Warning( $"Deferred modslot for {newItemId} already exists, skipping.");
            return;
        }
        _deferredModSlotConfigs.Add((newItemId, config));
    }
    public void ProcessDeferredModSlots()
    {
        if (_deferredModSlotConfigs.Count == 0)
        {
            logger.Info( "No deferred modslots to process");
            return;
        }

        logger.Info( $"Processing {_deferredModSlotConfigs.Count} deferred modslots...");

        foreach (var (newItemId, config) in _deferredModSlotConfigs)
        {
            try
            {
                if (_database == null)
                {
                    return;
                }
                modSlotHelper.ProcessModSlots(config, newItemId);

                if (logger.IsLogEnabled(LogLevel.Debug))
                {
                    logger.Debug($"Processed modslots for {newItemId}");
                }
            }
            catch (Exception ex)
            {
                logger.Critical( $"Failed processing modslots for {newItemId}", ex);
            }
        }

        _deferredModSlotConfigs.Clear();
        
        logger.Info( "Finished processing deferred modslots");
    }
}
