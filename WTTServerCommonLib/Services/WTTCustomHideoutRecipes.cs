using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using LogLevel = SPTarkov.Server.Core.Models.Spt.Logging.LogLevel;

namespace WTTServerCommonLib.Services;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class WTTCustomHideoutRecipes(
    ISptLogger<WTTCustomHideoutRecipes> logger,
    DatabaseServer databaseServer,
    ModHelper modHelper)
{
    private DatabaseTables? _database;
    
    public void AddHideoutRecipes(Assembly assembly)
    {
        string assemblyLocation = modHelper.GetAbsolutePathToModFolder(assembly);
        string defaultDir = Path.Combine("db", "CustomHideoutRecipes");
        string finalDir = Path.Combine(assemblyLocation, defaultDir);
            
        if (_database == null)
        {
            _database = databaseServer.GetTables();
        }
        if (!Directory.Exists(finalDir))
        {
            logger.Error($"'directory not found at {finalDir}");
            return;
        }
        
        var jsonFiles = Directory.EnumerateFiles(finalDir, "*.json", SearchOption.AllDirectories);

        foreach (var file in jsonFiles)
        {
            var relativePath = Path.GetRelativePath(finalDir, file);

            HideoutProduction recipe;
            try
            {
                recipe = modHelper.GetJsonDataFromFile<HideoutProduction>(finalDir, relativePath);
            }
            catch (Exception ex)
            {
                logger.Critical($"Failed to read {file}", ex);
                continue;
            }

            if (!MongoId.IsValidMongoId(recipe.Id))
            {
                logger.Error($"Missing Id in {file}");
                continue;
            }

            bool recipeExists = _database.Hideout.Production.Recipes != null && _database.Hideout.Production.Recipes.Any(r => r.Id == recipe.Id);
            if (recipeExists)
            {
                if (logger.IsLogEnabled(LogLevel.Debug))
                {
                    logger.Debug($"Recipe {recipe.Id} already exists, skipping");
                }
                
                continue;
            }

            _database.Hideout.Production.Recipes?.Add(recipe);
            logger.Info($"Added hideout recipe {recipe.Id} for item {recipe.EndProduct}");
        }
    }
}