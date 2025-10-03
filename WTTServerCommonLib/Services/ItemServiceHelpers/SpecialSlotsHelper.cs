using SPTarkov.Server.Core.Models.Spt.Server;
using WTTServerCommonLib.Helpers;
using WTTServerCommonLib.Models;

namespace WTTServerCommonLib.Services.ItemServiceHelpers;

public static class SpecialSlotsHelper
{
    public static void AddToSpecialSlots(CustomItemConfig itemConfig, string itemId, DatabaseTables database)
    {
        if (itemConfig.AddToSpecialSlots != true)
        {
            return;
        }

        var pocketIds = new[]
        {
            "627a4e6b255f7527fb05a0f6", // normal pockets
            "65e080be269cbd5c5005e529"  // unheard pockets
        };

        foreach (var pocketsId in pocketIds)
        {
            if (!database.Templates.Items.TryGetValue(pocketsId, out var pockets))
            {
                Log.Warn( $"[SpecialSlots] Could not find pockets template with id {pocketsId}");
                continue;
            }

            if (pockets.Properties?.Slots == null)
            {
                Log.Warn( $"[SpecialSlots] Pockets template {pocketsId} has no slots.");
                continue;
            }

            foreach (var slot in pockets.Properties.Slots)
            {
                if (slot.Properties?.Filters == null)
                    continue;

                var firstFilter = slot.Properties.Filters.FirstOrDefault();
                if (firstFilter?.Filter == null)
                    continue;

                if (firstFilter.Filter.Add(itemId))
                {
                    //Log.Info($"[SpecialSlots] Added {itemId} to pockets slot in {pocketsId}");
                }
            }
        }
    }
}