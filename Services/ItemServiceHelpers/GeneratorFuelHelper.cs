using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Server;
using WTTServerCommonLib.Helpers;
using WTTServerCommonLib.Models;

namespace WTTServerCommonLib.Services.ItemServiceHelpers;

public static class GeneratorFuelHelper
{
    public static void AddGeneratorFuel(CustomItemConfig itemConfig, string itemId, DatabaseTables database)
    {
        var generator = database.Hideout.Areas.Find(a => a.Id == "5d3b396e33c48f02b81cd9f3");
        var validStages = itemConfig.GeneratorFuelSlotStages;

        if (generator == null)
        {
            Log.Error("Generator not found in hideout areas.");
            return;
        }

        foreach (var stage in generator.Stages)
        {
            foreach (var validStage in validStages)
            {
                if (stage.Key != validStage)
                {
                    Log.Error($"Stage {validStage} not found in generator fuel.");
                    break;
                }

                foreach (var bonus in stage.Value.Bonuses)
                {
                    if (bonus is not { Type: BonusType.AdditionalSlots, Filter: List<string> filter }) continue;
                    if (filter.Contains(itemId)) continue;

                    filter.Add(itemId);
                    Log.Info($"[GeneratorFuel] Added item {itemId} as fuel to generator at stage with bonus ID {bonus.Id}");
                }
            }
        }
    }
}
