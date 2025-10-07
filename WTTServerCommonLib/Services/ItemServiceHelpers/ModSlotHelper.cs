using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using WTTServerCommonLib.Helpers;
using WTTServerCommonLib.Models;

namespace WTTServerCommonLib.Services.ItemServiceHelpers;

[Injectable]
public class ModSlotHelper(ISptLogger<ModSlotHelper> logger, DatabaseService databaseService)
{
    public void ProcessModSlots(CustomItemConfig itemConfig, string newItemId)
    {

        string itemTplToClone = itemConfig.ItemTplToClone;
        MongoId finalTplToClone = ItemTplResolver.ResolveId(itemTplToClone);
        if (itemConfig.AddToModSlots != true || itemConfig.ModSlot == null || itemConfig.ModSlot.Count == 0)
            return;

        var targetSlotNames = itemConfig.ModSlot
            .Select(slot => slot.ToLower())
            .ToList();

        var items = databaseService.GetItems();
        foreach (var (_, parentTemplate) in items)
        {
            if (parentTemplate.Properties?.Slots == null)
                continue;

            foreach (var slot in parentTemplate.Properties.Slots)
            {
                var slotNameLower = slot.Name?.ToLower();
                if (slotNameLower == null || !targetSlotNames.Contains(slotNameLower))
                    continue;

                var slotFilter = slot.Properties?.Filters?.FirstOrDefault();
                if (slotFilter?.Filter == null)
                    continue;

                if (slotFilter.Filter.Contains(finalTplToClone) && 
                    slotFilter.Filter.Add(newItemId))
                {
                    //Log.Info($"[ModSlots] Added {newItemId} to slot '{slot.Name}' for parent template {parentTemplateId}");
                }
            }
        }
    }
}