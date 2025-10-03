using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Server;
using WTTServerCommonLib.Helpers;
using WTTServerCommonLib.Models;

namespace WTTServerCommonLib.Services.ItemServiceHelpers;

public static class HideoutStatuetteHelper
{
    private static readonly string CustomizationItem = "673c7b00cbf4b984b5099181";
    private static readonly string[] StatuetteSlotIds =
    [
        "Statuette_Gym_1", "Statuette_PlaceOfFame_1", "Statuette_PlaceOfFame_2",
        "Statuette_PlaceOfFame_3", "Statuette_Heating_1", "Statuette_Heating_2",
        "Statuette_Library_1", "Statuette_Library_2", "Statuette_RestSpace_1",
        "Statuette_RestSpace_2", "Statuette_MedStation_1", "Statuette_MedStation_2",
        "Statuette_Kitchen_1", "Statuette_Kitchen_2", "Statuette_BoozeGenerator_1",
        "Statuette_Workbench_1", "Statuette_IntelligenceCenter_1", "Statuette_ShootingRange_1"
    ];

    public static void AddToStatuetteSlot(string itemId, DatabaseTables database)
    {
        foreach (var StatuetteSlotId in StatuetteSlotIds)
        {
            if (!database.Templates.Items.TryGetValue(CustomizationItem, out var StatuetteParent) || StatuetteParent.Properties?.Slots == null)
                continue;

            AddItemToStatuetteSlots(itemId, StatuetteParent, StatuetteSlotId);
        }
    }

    private static void AddItemToStatuetteSlots(string itemId, TemplateItem StatuetteItem, string StatuetteSlotId)
    {
        foreach (var slot in StatuetteItem.Properties.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Name) || slot.Properties?.Filters == null)
                continue;

            string slotType = GetMatchingSlotType(slot.Name);
            if (StatuetteSlotId != slotType)
                continue;

            AddItemToFilters(itemId, slot, StatuetteItem.Name);
        }
    }

    private static void AddItemToFilters(string itemId, Slot slot, string? slotName)
    {
        foreach (var filter in slot.Properties.Filters)
        {
            filter.Filter ??= new HashSet<MongoId>();

            if (filter.Filter.Add(itemId))
                Log.Info($"[Statuette] Added {itemId} to slot '{slot.Name}' in {slotName}");
            else
                Log.Debug($"[Statuette] {itemId} already in slot '{slot.Name}'");
        }
    }

    private static string GetMatchingSlotType(string slotName)
    {
        foreach (var type in StatuetteSlotIds)
        {
            if (slotName.StartsWith(type, StringComparison.OrdinalIgnoreCase))
                return type;
        }
        return null;
    }
}