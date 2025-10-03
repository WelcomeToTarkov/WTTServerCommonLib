using SPTarkov.Server.Core.Models.Spt.Server;
using WTTServerCommonLib.Helpers;
using WTTServerCommonLib.Models;

namespace WTTServerCommonLib.Services.ItemServiceHelpers;

public static class InventorySlotHelper
{
    public static void ProcessInventorySlots(
        CustomItemConfig itemConfig,
        string itemId,
        DatabaseTables database)
    {
        if (itemConfig.AddToInventorySlots == null)
            return;

        const string pmcInventoryTemplateId = "55d7217a4bdc2d86028b456d";
        var defaultInventorySlots = database.Templates.Items[pmcInventoryTemplateId].Properties?.Slots;
        if (defaultInventorySlots == null)
            return;

        var allowedSlots = itemConfig.AddToInventorySlots
            .Select(slot => slot.ToLower())
            .ToList();

        foreach (var slot in defaultInventorySlots)
        {
            var filtersList = slot.Properties?.Filters?.ToList();
            if (filtersList == null || filtersList.Count == 0)
                continue;

            var slotNameLower = slot.Name?.ToLower();
            if (slotNameLower == null)
                continue;

            if (allowedSlots.Contains(slotNameLower))
            {
                var firstFilter = filtersList.FirstOrDefault();
                if (firstFilter?.Filter == null)
                    continue;

                if (firstFilter.Filter.Add(itemId))
                {
                    Log.Info($"[InventorySlots] Added {itemId} to inventory slot '{slot.Name}'");
                }
            }
        }
    }
}