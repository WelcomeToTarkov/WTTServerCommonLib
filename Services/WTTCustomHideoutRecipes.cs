using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using WTTServerCommonLib.Helpers;
using WTTServerCommonLib.Models;
using LogLevel = SPTarkov.Server.Core.Models.Spt.Logging.LogLevel;

namespace WTTServerCommonLib.Services;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class WTTCustomHideoutRecipes(
    DatabaseServer databaseServer,
    ModHelper modHelper)
{
    private DatabaseTables? _database;
    
    public void AddHideoutRecipes(Assembly assembly)
    {
        string assemblyLocation = modHelper.GetAbsolutePathToModFolder(assembly);
        string defaultDir = Path.Combine("db", "HideoutRecipes");
        string finalDir = Path.Combine(assemblyLocation, defaultDir);
            
        if (_database == null)
        {
            _database = databaseServer.GetTables();
        }
        if (!Directory.Exists(finalDir))
        {
            Log.Error($"[HideoutRecipes] 'db/HideoutRecipes' directory not found at {finalDir}");
            return;
        }
        
        var jsonFiles = Directory.EnumerateFiles(finalDir, "*.json", SearchOption.AllDirectories);

        foreach (var file in jsonFiles)
        {
            var relativePath = Path.GetRelativePath(finalDir, file);

            HideoutProduction recipe = null;
            try
            {
                recipe = modHelper.GetJsonDataFromFile<HideoutProduction>(finalDir, relativePath);
            }
            catch (Exception ex)
            {
                Log.Debug($"[HideoutRecipes] Failed to read {file}: {ex.Message}");
                continue;
            }

            if (recipe == null)
            {
                Log.Debug($"[HideoutRecipes] {file} returned null");
                continue;
            }

            if (string.IsNullOrWhiteSpace(recipe.Id))
            {
                Log.Debug($"[HideoutRecipes] Missing Id in {file}");
                continue;
            }

            bool recipeExists = _database.Hideout.Production.Recipes.Any(r => r.Id == recipe.Id);
            if (recipeExists)
            {
                Log.Debug($"[HideoutRecipes] Recipe {recipe.Id} already exists, skipping");
                continue;
            }

            _database.Hideout.Production.Recipes.Add(recipe);
            Log.Info($"[HideoutRecipes] Added hideout recipe {recipe.Id} for item {recipe.EndProduct}");
        }
    }
}