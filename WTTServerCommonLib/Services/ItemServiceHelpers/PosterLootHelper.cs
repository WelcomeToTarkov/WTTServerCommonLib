using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using WTTServerCommonLib.Models;
using LogLevel = SPTarkov.Server.Core.Models.Spt.Logging.LogLevel;

namespace WTTServerCommonLib.Services.ItemServiceHelpers;

[Injectable]
public class PosterLootHelper(ISptLogger<PosterLootHelper> logger)
{
    public void ProcessPosterLoot(CustomItemConfig config, string itemId, DatabaseTables? db)
    {
        var locations = db?.Locations.GetDictionary();
        if (locations == null || locations.Count == 0) return;

        foreach (var (map, location) in locations)
        {
            var spawnPoints = location.LooseLoot?.Value?.Spawnpoints;
            if (spawnPoints == null) continue;

            var spawnPointsList = spawnPoints as IList<Spawnpoint> ?? spawnPoints.ToList();
            if (!ReferenceEquals(spawnPoints, spawnPointsList))
                if (location.LooseLoot != null)
                    if (location.LooseLoot.Value != null)
                        location.LooseLoot.Value.Spawnpoints = spawnPointsList;

            foreach (var spawnpoint in spawnPointsList)
            {
                var template = spawnpoint.Template;
                var templateId = template?.Id ?? "";
                if (string.IsNullOrEmpty(templateId) || !templateId.StartsWith("flyer", StringComparison.OrdinalIgnoreCase)) continue;

                var spawnPointItems = template?.Items;
                var items = spawnPointItems as IList<SptLootItem> ?? spawnPointItems?.ToList() ?? new List<SptLootItem>();
                if (!ReferenceEquals(spawnPointItems, items))
                    if (template != null)
                        template.Items = items;

                if (items.Any(it => it.Template == itemId)) continue;

                var itemDistList = spawnpoint.ItemDistribution;
                var dist = itemDistList as IList<LooseLootItemDistribution> ?? itemDistList?.ToList() ?? new List<LooseLootItemDistribution>();
                if (!ReferenceEquals(itemDistList, dist)) spawnpoint.ItemDistribution = dist;

                var newId = new MongoId();

                items.Add(new SptLootItem
                {
                    Id = newId,
                    Template = itemId,
                    ComposedKey = newId,
                    Upd = new Upd { StackObjectsCount = 1 }
                });

                dist.Add(new LooseLootItemDistribution
                {
                    ComposedKey = new ComposedKey { Key = newId },
                    RelativeProbability = config.PosterSpawnProbability
                });

                if (logger.IsLogEnabled(LogLevel.Debug))
                {
                    logger.Debug($"[PosterLoot] {map} + {spawnpoint.LocationId ?? "?"} id={templateId} key={newId}");
                }
            }
        }
    }
}
