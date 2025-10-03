using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Server;
using WTTServerCommonLib.Helpers;

namespace WTTServerCommonLib.Services.ItemServiceHelpers;

public static class MasteryHelper
{
    public static void AddOrUpdateMasteries(
        IEnumerable<Mastering> masterySections,
        string itemId,
        DatabaseTables database
        )
    {
        var masteries = masterySections.ToList();
        if (!masteries.Any())
        {
            Log.Warn( $"No mastery sections defined for item {itemId}");
            return;
        }

        foreach (var mastery in masteries)
        {
            if (string.IsNullOrEmpty(mastery.Name))
            {
                Log.Error( "Mastery section has no name, skipping.");
                continue;
            }

            var existing = database.Globals.Configuration.Mastering
                .FirstOrDefault(m => m.Name.Equals(mastery.Name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                var templates = existing.Templates.ToList();

                foreach (var template in mastery.Templates)
                {
                    if (string.IsNullOrEmpty(template))
                    {
                        Log.Warn( "Invalid template in mastery section, skipping.");
                        continue;
                    }

                    if (!templates.Contains(template))
                    {
                        templates.Add(template);
                        Log.Warn( $"Added template {template} to mastery '{mastery.Name}'");
                    }
                }

                existing.Templates = templates.ToArray();
                //Log.Info($"[Mastery] Updated existing mastery '{mastery.Name}' for {itemId}");
            }
            else
            {
                var newMastery = new Mastering
                {
                    Name = mastery.Name,
                    Level2 = mastery.Level2,
                    Level3 = mastery.Level3,
                    Templates = mastery.Templates.ToArray()
                };

                var newMastering = database.Globals.Configuration.Mastering.ToList();
                newMastering.Add(newMastery);
                database.Globals.Configuration.Mastering = newMastering.ToArray();

                //Log.Info( $"[Mastery] Created new mastery '{mastery.Name}' for {itemId}");
            }
        }
    }
}