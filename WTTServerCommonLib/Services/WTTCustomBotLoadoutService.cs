using System.Reflection;
using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using WTTServerCommonLib.Models;
using Path = System.IO.Path;

namespace WTTServerCommonLib.Services;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class WTTCustomBotLoadoutService(
    DatabaseServer databaseServer,
    ISptLogger<WTTCustomBotLoadoutService> logger,
    ModHelper modHelper)
{
    private DatabaseTables? _database;

    public void RegisterCustomBotLoadouts(Assembly assembly)
    {
        string assemblyLocation = modHelper.GetAbsolutePathToModFolder(assembly);
        string defaultDir = Path.Combine("db", "CustomBotLoadouts");
        string finalDir = Path.Combine(assemblyLocation, defaultDir);
            
        if (_database == null)
        {
            _database = databaseServer.GetTables();
        }
        
        if (!Directory.Exists(finalDir))
        {
            logger.Error($"'directory not found at {finalDir}");
            return;
        }
        
        var jsonFiles = Directory.EnumerateFiles(finalDir, "*.json", SearchOption.AllDirectories);

        foreach (var file in jsonFiles)
        {
            var relativePath = Path.GetRelativePath(finalDir, file);
            var botTypeName = Path.GetFileNameWithoutExtension(file);

            try
            {
                var customLoadout = modHelper.GetJsonDataFromFile<CustomBotLoadoutConfig>(finalDir, relativePath);
                ApplyCustomBotLoadout(botTypeName, customLoadout);
                logger.Info($"Successfully applied custom loadout for bot type: {botTypeName}");
            }
            catch (Exception ex)
            {
                logger.Critical($"Failed to process bot loadout file {file} for bot type {botTypeName}", ex);
            }
        }
    }

    private void ApplyCustomBotLoadout(string botTypeName, CustomBotLoadoutConfig customBotLoadoutConfigLoadout)
    {
        
        if (_database == null)
        {
            logger.Error("Database is null");
            return;
        }
        if (!_database.Bots.Types.TryGetValue(botTypeName, out var botType))
        {
            logger.Error($"Bot type '{botTypeName}' not found in database");
            return;
        }
        if (botType == null)
        {
            logger.Error("Bottype is null");
            return;
        }
        
        if (customBotLoadoutConfigLoadout.Chances != null)
        {
            MergeChances(botType, customBotLoadoutConfigLoadout.Chances);
        }

        if (customBotLoadoutConfigLoadout.Inventory != null)
        {
            MergeInventory(botType, customBotLoadoutConfigLoadout.Inventory);
        }

        if (customBotLoadoutConfigLoadout.Appearance != null)
        {
            MergeAppearance(botType, customBotLoadoutConfigLoadout.Appearance);
        }
    }

    private void MergeChances(BotType botType, ConfigBotChances chances)
    {
        if (chances.Equipment != null)
        {
            foreach (var equipmentChance in chances.Equipment)
            {
                try
                {
                    botType.BotChances.EquipmentChances[equipmentChance.Key] = equipmentChance.Value;
                }
                catch (ArgumentException)
                {
                    logger.Warning($"Invalid equipment slot: {equipmentChance.Key}");
                }
            }
        }

        if (chances.WeaponMods != null)
        {
            foreach (var modChance in chances.WeaponMods)
            {
                botType.BotChances.WeaponModsChances[modChance.Key] = modChance.Value;
            }
        }
        if (chances.EquipmentMods != null)
        {
            foreach (var modChance in chances.EquipmentMods)
            {
                botType.BotChances.EquipmentModsChances[modChance.Key] = modChance.Value;
            }
        }
    }

    private void MergeInventory(BotType botType, ConfigBotInventory inventory)
    {
        if (inventory.Equipment != null)
        {
            foreach (var equipmentSlot in inventory.Equipment)
            {
                try
                {
                    var equipmentSlotEnum = (EquipmentSlots)Enum.Parse(typeof(EquipmentSlots), equipmentSlot.Key);
                    
                    if (!botType.BotInventory.Equipment.ContainsKey(equipmentSlotEnum))
                    {
                        botType.BotInventory.Equipment[equipmentSlotEnum] = new Dictionary<MongoId, double>();
                    }
                    
                    foreach (var equipmentItem in equipmentSlot.Value)
                    {
                        if (MongoId.IsValidMongoId(equipmentItem.Key))
                        {
                            botType.BotInventory.Equipment[equipmentSlotEnum][equipmentItem.Key] = equipmentItem.Value;
                        }
                        else
                        {
                            logger.Warning($"Invalid MongoId for equipment: {equipmentItem.Key}");
                        }
                    }
                }
                catch (ArgumentException)
                {
                    logger.Warning($"Invalid equipment slot: {equipmentSlot.Key}");
                }
            }
        }

        if (inventory.Mods != null)
        {
            foreach (var baseItemMods in inventory.Mods)
            {
                if (MongoId.IsValidMongoId(baseItemMods.Key))
                {
                    var baseItemId = new MongoId(baseItemMods.Key);
            
                    if (!botType.BotInventory.Mods.ContainsKey(baseItemId))
                    {
                        botType.BotInventory.Mods[baseItemId] = new Dictionary<string, HashSet<MongoId>>();
                    }
            
                    foreach (var modSlot in baseItemMods.Value)
                    {
                        if (!botType.BotInventory.Mods[baseItemId].ContainsKey(modSlot.Key))
                        {
                            botType.BotInventory.Mods[baseItemId][modSlot.Key] = new HashSet<MongoId>();
                        }
                
                        var existingMods = botType.BotInventory.Mods[baseItemId][modSlot.Key];
                
                        foreach (var modIdString in modSlot.Value)
                        {
                            if (MongoId.IsValidMongoId(modIdString))
                            {
                                var modId = new MongoId(modIdString);
                                existingMods.Add(modId); 
                            }
                            else
                            {
                                logger.Warning($"Invalid MongoId for mod: {modIdString}");
                            }
                        }
                    }
                }
                else
                {
                    logger.Warning($"Invalid base item MongoId: {baseItemMods.Key}");
                }
            }
        }

        if (inventory.Ammo != null)
        {
            foreach (var caliberAmmo in inventory.Ammo)
            {
                if (!botType.BotInventory.Ammo.ContainsKey(caliberAmmo.Key))
                {
                    botType.BotInventory.Ammo[caliberAmmo.Key] = new Dictionary<MongoId, double>();
                }
                
                foreach (var ammoType in caliberAmmo.Value)
                {
                    if (MongoId.IsValidMongoId(ammoType.Key))
                    {
                        botType.BotInventory.Ammo[caliberAmmo.Key][ammoType.Key] = ammoType.Value;
                    }
                    else
                    {
                        logger.Warning($"Invalid MongoId for ammo: {ammoType.Key}");
                    }
                }
            }
        }
    }

    private void MergeAppearance(BotType botType, ConfigBotAppearance appearance)
    {
        if (appearance.Body != null)
        {
            foreach (var body in appearance.Body)
            {
                if (body.Key.IsValidMongoId())
                {
                    botType.BotAppearance.Body[body.Key] = body.Value;
                }
            }
        }


        if (appearance.Feet != null)
        {
            foreach (var feet in appearance.Feet)
            {
                if (feet.Key.IsValidMongoId())
                {
                    botType.BotAppearance.Feet[feet.Key] = feet.Value;
                }
            }
        }

        if (appearance.Hands != null)
        {
            foreach (var hands in appearance.Hands)
            {
                if (hands.Key.IsValidMongoId())
                {
                    botType.BotAppearance.Hands[hands.Key] = hands.Value;
                }
            }
        }

        if (appearance.Head != null)
        {
            foreach (var head in appearance.Head)
            {
                if (head.Key.IsValidMongoId())
                {
                    botType.BotAppearance.Head[head.Key] = head.Value;
                }
            }
            
        }

        if (appearance.Voice != null)
        {
            foreach (var voice in appearance.Voice)
            {
                if (voice.Key.IsValidMongoId())
                {
                    botType.BotAppearance.Voice[voice.Key] = voice.Value;
                }
            }
        }
    }
}
