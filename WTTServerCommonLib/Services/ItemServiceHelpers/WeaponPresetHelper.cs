using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using WTTServerCommonLib.Models;

namespace WTTServerCommonLib.Services.ItemServiceHelpers;

[Injectable]
public class WeaponPresetHelper(ISptLogger<WeaponPresetHelper> logger)
{
    public void ProcessWeaponPresets(CustomItemConfig itemConfig, string itemId, DatabaseTables tables)
    {
        var globals = tables.Globals;

        var itemPresets = globals.ItemPresets;

        if (itemConfig.WeaponPresets == null)
        {
            logger.Warning( "WeaponPresets list is null. Skipping.");
            return;
        }

        foreach (var presetData in itemConfig.WeaponPresets)
        {

            if (presetData.Items.Count == 0)
            {
                logger.Warning($"Preset {presetData.Id} has no items defined. Skipping.");
                continue;
            }

            var preset = new Preset
            {
                Id = presetData.Id,
                Name = presetData.Name,
                Parent = presetData.Parent,
                ChangeWeaponName = presetData.ChangeWeaponName,
                Encyclopedia = string.IsNullOrEmpty(presetData.Encyclopedia) ? null : presetData.Encyclopedia,
                Type = "Preset",
                Items = new List<Item>()
            };

            foreach (var itemData in presetData.Items)
            {
                var item = new Item
                {
                    Id = itemData.Id,
                    Template = itemData.Template,
                    ParentId = string.IsNullOrEmpty(itemData.ParentId) ? null : itemData.ParentId,
                    SlotId = string.IsNullOrEmpty(itemData.SlotId) ? null : itemData.SlotId
                };

                preset.Items.Add(item);
            }

            itemPresets[preset.Id] = preset;
        }
    }
}