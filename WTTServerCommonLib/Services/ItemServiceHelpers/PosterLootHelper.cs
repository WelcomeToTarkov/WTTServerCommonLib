using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using WTTServerCommonLib.Models;
using LogLevel = SPTarkov.Server.Core.Models.Spt.Logging.LogLevel;

namespace WTTServerCommonLib.Services.ItemServiceHelpers;

[Injectable]
public class PosterLootHelper(DatabaseService databaseService, ISptLogger<PosterLootHelper> logger)
{
    public void ProcessPosterLoot(CustomItemConfig config, string itemId)
    {
        var locations = databaseService.GetLocations().GetDictionary();

        foreach (var (locationId, location) in locations)
        {
            if (location.LooseLoot is null)
            {
                continue;
            }

            location.LooseLoot.AddTransformer(lazyLoadedLooseLootData =>
            {
                foreach (var spawnpoint in lazyLoadedLooseLootData?.Spawnpoints ?? [])
                {
                    if (spawnpoint is null)
                    {
                        continue;
                    }

                    var template = spawnpoint.Template;

                    if (template is null)
                    {
                        continue;
                    }

                    var templateId = template.Id;
                    if (string.IsNullOrEmpty(templateId) || !templateId.StartsWith("flyer", StringComparison.OrdinalIgnoreCase)) continue;

                    var spawnPointItems = new List<SptLootItem>(template.Items ?? []);

                    if (spawnPointItems.Any(it => it.Template == itemId)) continue;

                    var itemDistList = new List<LooseLootItemDistribution>(spawnpoint.ItemDistribution ?? []);
                    var newId = new MongoId();

                    spawnPointItems.Add(new SptLootItem
                    {
                        Id = newId,
                        Template = itemId,
                        ComposedKey = newId,
                        Upd = new Upd { StackObjectsCount = 1 }
                    });

                    itemDistList.Add(new LooseLootItemDistribution
                    {
                        ComposedKey = new ComposedKey { Key = newId },
                        RelativeProbability = config.PosterSpawnProbability
                    });

                    if (logger.IsLogEnabled(LogLevel.Debug))
                    {
                        logger.Debug($"[PosterLoot] {locationId} + {spawnpoint.LocationId ?? "?"} id={templateId} key={newId}");
                    }

                    template.Items = spawnPointItems;
                    spawnpoint.ItemDistribution = itemDistList;
                }

                return lazyLoadedLooseLootData;
            });
        }
    }
}
