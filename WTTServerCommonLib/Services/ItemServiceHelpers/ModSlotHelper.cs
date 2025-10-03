using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Server;
using WTTServerCommonLib.Models;

namespace WTTServerCommonLib.Services.ItemServiceHelpers;

[Injectable]
public class ModSlotHelper
{
    public void ProcessModSlots(
        CustomItemConfig itemConfig, 
        string newItemId, 
        DatabaseTables database)
    {

        string itemTplToClone = itemConfig.ItemTplToClone;
        if (itemConfig.AddToModSlots != true || itemConfig.ModSlot == null || itemConfig.ModSlot.Count == 0)
            return;

        var targetSlotNames = itemConfig.ModSlot
            .Select(slot => slot.ToLower())
            .ToList();

        foreach (var (_, parentTemplate) in database.Templates.Items)
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

                if (slotFilter.Filter.Contains(itemTplToClone) && 
                    slotFilter.Filter.Add(newItemId))
                {
                    //Log.Info($"[ModSlots] Added {newItemId} to slot '{slot.Name}' for parent template {parentTemplateId}");
                }
            }
        }
    }
}