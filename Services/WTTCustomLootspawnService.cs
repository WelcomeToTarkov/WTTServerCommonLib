using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using Locations = SPTarkov.Server.Core.Models.Spt.Server.Locations;
using Path = System.IO.Path;

namespace WTTServerCommonLib.Services;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class WTTCustomLootspawnService(
    DatabaseServer databaseServer,
    JsonUtil jsonUtil,
    ModHelper modHelper)
{
    
    private const double Epsilon = 0.0001;
    
    
    public void AddCustomLootSpawns(Assembly assembly, string? relativePath = null)
    {
        string assemblyLocation = modHelper.GetAbsolutePathToModFolder(assembly);
        string defaultDir = Path.Combine("db", "CustomLootspawns");
        string finalDir = Path.Combine(assemblyLocation, relativePath ?? defaultDir);

        DatabaseTables database = databaseServer.GetTables();
        var locations = database.Locations.GetDictionary();

        // Load forced spawns
        var forcedPath = Path.Combine(finalDir, "customSpawnpointsForced.json");
        if (File.Exists(forcedPath))
        {
            var forcedJson = File.ReadAllText(forcedPath);
            var forcedSpawns = jsonUtil.Deserialize<Dictionary<string, List<Spawnpoint>>>(forcedJson);

            if (forcedSpawns != null)
                foreach (var kvp in forcedSpawns)
                {
                    string locationId = database.Locations.GetMappedKey(kvp.Key);
                    if (!locations.TryGetValue(locationId, out var location)) continue;
                    if (location.LooseLoot == null) continue;

                    var looseLoot = location.LooseLoot.Value;

                    // Create mutable list from existing forced spawns
                    var existingForced = looseLoot?.SpawnpointsForced?.ToList() ?? new List<Spawnpoint>();

                    // Merge with duplicate check
                    foreach (var newSpawn in kvp.Value)
                    {
                        if (existingForced.All(sp => sp.LocationId != newSpawn.LocationId))
                        {
                            existingForced.Add(newSpawn);
                        }
                    }

                    // Always assign back to ensure mutable collection
                    if (looseLoot != null) looseLoot.SpawnpointsForced = existingForced;
                }
        }

        // Load general custom spawns
        var generalPath = Path.Combine(finalDir, "customSpawnpoints.json");
        if (File.Exists(generalPath))
        {
            var generalJson = File.ReadAllText(generalPath);
            var generalSpawns = jsonUtil.Deserialize<Dictionary<string, List<Spawnpoint>>>(generalJson);
            if (generalSpawns != null) ProcessSpawnpoints(database.Locations, generalSpawns);
        }
    }

    private void ProcessSpawnpoints(
        Locations locations,
        Dictionary<string, List<Spawnpoint>> customMap)
    {
        foreach (var kvp in customMap)
        {
            string locationId = locations.GetMappedKey(kvp.Key);
            var locationMap = locations.GetDictionary();
            if (!locationMap.TryGetValue(locationId, out var location)) continue;
            if (location.LooseLoot == null) continue;

            var looseLoot = location.LooseLoot.Value;
            
            var existingSpawns = looseLoot?.Spawnpoints?.ToList() ?? new List<Spawnpoint>();
            var customSpawns = kvp.Value;

            foreach (var customSpawn in customSpawns)
            {
                var existing = existingSpawns
                    .FirstOrDefault(sp => sp.LocationId == customSpawn.LocationId);

                if (existing == null)
                {
                    // Add new spawn point
                    existingSpawns.Add(customSpawn);
                }
                else
                {
                    // Merge existing spawn point
                    existing.Probability = customSpawn.Probability;

                    if (customSpawn.Template != null)
                    {
                        existing.Template ??= new SpawnpointTemplate();
                        
                        // Directly assign simple properties
                        existing.Template.IsContainer = customSpawn.Template.IsContainer;
                        existing.Template.UseGravity = customSpawn.Template.UseGravity;
                        existing.Template.RandomRotation = customSpawn.Template.RandomRotation;

                        // Merge items
                        if (customSpawn.Template.Items != null)
                        {
                            var existingItems = existing.Template.Items?.ToList() ?? new List<SptLootItem>();
                            foreach (var newItem in customSpawn.Template.Items)
                            {
                                if (existingItems.All(i => i.Id != newItem.Id))
                                    existingItems.Add(newItem);
                            }
                            existing.Template.Items = existingItems;
                        }

                        // Merge group positions
                        if (customSpawn.Template.GroupPositions != null)
                        {
                            var existingGroups = existing.Template.GroupPositions?.ToList() ?? new List<GroupPosition>();
                            foreach (var newGroup in customSpawn.Template.GroupPositions)
                            {
                                bool exists = existingGroups.Any(g =>
                                    AreEqual(g.Position?.X, newGroup.Position?.X) &&
                                    AreEqual(g.Position?.Y, newGroup.Position?.Y) &&
                                    AreEqual(g.Position?.Z, newGroup.Position?.Z));

                                if (!exists)
                                    existingGroups.Add(newGroup);
                            }
                            existing.Template.GroupPositions = existingGroups;
                        }
                    }

                    // Merge item distributions
                    if (customSpawn.ItemDistribution != null)
                    {
                        var existingDist = existing.ItemDistribution?.ToList() ?? new List<LooseLootItemDistribution>();
                        foreach (var newDist in customSpawn.ItemDistribution)
                        {
                            if (existingDist.All(d => d.ComposedKey?.Key != newDist.ComposedKey?.Key))
                                existingDist.Add(newDist);
                        }
                        existing.ItemDistribution = existingDist;
                    }
                }
            }
            
            if (looseLoot != null) looseLoot.Spawnpoints = existingSpawns;
        }
        

    }

    private static bool AreEqual(double? a, double? b)
    {
        if (a == null || b == null) return Equals(a, b);
        return Math.Abs(a.Value - b.Value) < Epsilon;
    }
}