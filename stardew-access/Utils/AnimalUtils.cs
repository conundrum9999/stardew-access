using Microsoft.Xna.Framework;
using StardewValley;

namespace stardew_access.Utils;

public class AnimalUtils
{
    private static readonly Dictionary<Vector2, FarmAnimal> AnimalByCoordinate = [];
    public static Dictionary<Vector2, FarmAnimal>? GetAnimalsByLocation(GameLocation location)
    {
        IEnumerable<FarmAnimal>? farmAnimals = location switch
        {
            Farm farm => farm.getAllFarmAnimals(),
            AnimalHouse animalHouse => animalHouse.Animals.Values,
            _ => null
        };

        if (farmAnimals is null || !farmAnimals.Any()) return null;

        AnimalByCoordinate.Clear();

        // Populate the dictionary
        foreach (FarmAnimal animal in farmAnimals)
        {
            AnimalByCoordinate[animal.Tile] = animal;
        }

        return AnimalByCoordinate;
    }
}
