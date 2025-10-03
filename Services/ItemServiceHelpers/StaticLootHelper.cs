using System.Reflection;
using SPTarkov.Server.Core.Models.Eft.Common; 
using SPTarkov.Server.Core.Models.Spt.Server;
using WTTServerCommonLib.Constants;
using WTTServerCommonLib.Helpers;
using WTTServerCommonLib.Models;

namespace WTTServerCommonLib.Services.ItemServiceHelpers
{
public static class StaticLootHelper
{
    public static void ProcessStaticLootContainers(
        CustomItemConfig config,
        string itemId,
        DatabaseTables database)
    {
        if (config.StaticLootContainers != null)
            foreach (var container in config.StaticLootContainers)
            {
                if (string.IsNullOrWhiteSpace(container.ContainerName)) continue;
                AddToStaticLoot(container.ContainerName, itemId, container.Probability, database);
            }
    }

    private static void AddToStaticLoot(
        string containerId,
        string itemId,
        int probability,
        DatabaseTables database)
    {
        var locationsDict = database.Locations.GetDictionary();
        if (locationsDict.Count == 0)
        {
            Log.Warn( "[StaticLoot] Locations dictionary was empty.");
            return;
        }

        foreach (var kv in locationsDict)
        {
            var locationId = kv.Key;
            var location = kv.Value;

            var staticLootDict = location.StaticLoot?.Value;
            var actualContainerId = NameHelper.ResolveId(containerId, ItemMaps.ItemMap);
            
            if (staticLootDict == null || !staticLootDict.TryGetValue(actualContainerId, out var containerDetails))
            {
                Log.Warn( $"[StaticLoot] Loot container '{containerId}' not found in {locationId}");
                continue;
            }

            AddDistributionToContainer(containerDetails, itemId, probability, locationId, actualContainerId);
        }
    }

    private static void AddDistributionToContainer(
        object containerDetails,
        string itemId,
        int probability,
        string locationId,
        string containerId)
    {
        var containerType = containerDetails.GetType();
        var itemDistProp = containerType.GetProperty("ItemDistribution",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (itemDistProp?.GetValue(containerDetails) is not IEnumerable<ItemDistribution> existing)
        {
            Log.Warn( $"[StaticLoot] Loot container '{containerId}' in {locationId} has no ItemDistribution list.");
            return;
        }

        var newList = existing.ToList();

        newList.Add(new ItemDistribution
        {
            Tpl = itemId,
            RelativeProbability = probability
        });

        itemDistProp.SetValue(containerDetails, newList.ToArray());

    }

    
}

}
