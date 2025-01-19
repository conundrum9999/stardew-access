using StardewModdingAPI.Events;
using System.Diagnostics;

namespace stardew_access.Features;

using Microsoft.Xna.Framework;
using Utils;
using StardewValley;
using StardewValley.Objects;
using StardewModdingAPI;

internal class Radar : FeatureBase
{
    #if DEBUG
    private static readonly Stopwatch watch = new();
    private static readonly Dictionary<string, (int counter, double average)> averages = [];

    private static void AddToAverage(string key, double value)
    {
        if (!averages.ContainsKey(key))
        {
            // Initialize the tuple for the key
            averages[key] = (0, 0);
        }

        // Extract the current counter and average
        var (counter, average) = averages[key];

        // Update the counter and calculate the new running average
        counter++;
        average += (value - average) / counter;

        // Store the updated values back in the dictionary
        averages[key] = (counter, average);
    }
    #endif

    private readonly List<Vector2> _closed;
    private readonly List<Furniture> _furniture;
    private readonly List<NPC> _npcs;
    internal List<string> Exclusions;
    private List<string> _tempExclusions;
    public List<string> Focus;
    public bool IsRunning;
    public bool RadarFocus = false;
    public int Delay, Range;

    // Reusable collections to minimize allocations in search functions
    private static readonly Queue<(int x, int y)> ToSearch = new();
    //private static readonly HashSet<Vector2> Searched = [];
    
    // Array of all eight direction Vectors
    /*private static readonly Vector2[] Directions =
    [
        new(-1, 0), // Left
        new(0, 1),  // Up
        new(1, 0),  // Right
        new(0, -1), // Down
        new(-1, 1), // Up-Left
        new(1, 1),  // Up-Right
        new(-1, -1),// Down-Left
        new(1, -1)  // Down-Right
    ];*/
    // Possible neighbor offsets (8-directional):
    private static readonly (int dx, int dy)[] Directions =
    [
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (-1, 1), (1, -1), (-1, -1)
    ];

    public static bool RadarDebug = false;

    private static Radar? instance;

    public new static Radar Instance
    {
        get
        {
            instance ??= new Radar();
            return instance;
        }
    }

    public Radar()
    {
        Delay = 3000;
        Range = 5;

        IsRunning = false;
        _closed = [];
        _furniture = [];
        _npcs = [];
        Exclusions = [];
        _tempExclusions = [];
        Focus = [];

        Exclusions.Add("stone");
        Exclusions.Add("weed");
        Exclusions.Add("twig");
        Exclusions.Add("coloured stone");
        Exclusions.Add("ice crystal");
        Exclusions.Add("clay stone");
        Exclusions.Add("fossil stone");
        Exclusions.Add("street lamp");
        Exclusions.Add("crop");
        Exclusions.Add("tree");
        Exclusions.Add("flooring");
        Exclusions.Add("water");
        Exclusions.Add("debris");
        Exclusions.Add("grass");
        Exclusions.Add("decoration");
        Exclusions.Add("bridge");
        Exclusions.Add("other");

        /* Not excluded Categories
         *
         *
         * exclusions.Add("farmer");
         * exclusions.Add("animal");
         * exclusions.Add("npc");
         * exclusions.Add("furniture")
         * exclusions.Add("building");
         * exclusions.Add("resource clump");
         * exclusions.Add("mine item");
         * exclusions.Add("container");
         * exclusions.Add("bundle");
         * exclusions.Add("door");
         * exclusions.Add("machine");
         * exclusions.Add("interactable");
         */
    }

    internal override void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsPlayerFree) return;

        RunRadarFeatureIfEnabled();

        async void RunRadarFeatureIfEnabled()
        {
            if (!IsRunning && MainClass.Config.Radar)
            {
                IsRunning = true;
                Run();
                await Task.Delay(Delay);
                IsRunning = false;
            }
        }
    }

    public void Run()
    {
        if (RadarDebug)
            Log.Debug($"\n\nRead Tile started");

        Vector2 currPosition = Game1.player.Tile;

        _closed.Clear();
        _furniture.Clear();
        _npcs.Clear();

        SearchNearbyTiles(currPosition, Range);

        if (RadarDebug)
            Log.Debug($"\nRead Tile stopped\n\n");
    }

    /// <summary>
    /// Search the area using Breadth First Search algorithm(BFS).
    /// </summary>
    /// <param name="center">The starting point.</param>
    /// <param name="limit">The limiting factor or simply radius of the search area.</param>
    /// <param name="playSound">True by default if False then it will not play sound and only return the list of detected tiles(for api).</param>
    /// <returns>A dictionary with all the detected tiles along with the name of the object on it and it's category.</returns>
    public Dictionary<Vector2, (string, string)> SearchNearbyTiles(Vector2 center, int limit, bool playSound = true)
    {
        var currentLocation = Game1.currentLocation;
        Dictionary<Vector2, (string, string)> detectedTiles = [];

        Queue<Vector2> toSearch = new();
        HashSet<Vector2> searched = [];
        int[] dirX = [-1, 0, 1, 0];
        int[] dirY = [0, 1, 0, -1];

        toSearch.Enqueue(center);
        searched.Add(center);

        while (toSearch.Count > 0)
        {
            Vector2 item = toSearch.Dequeue();
            if (playSound)
                CheckTileAndPlaySound(item, currentLocation);
            else
            {
                (bool, string?, string) tileInfo = CheckTile(item, currentLocation);
                if (tileInfo.Item1 && tileInfo.Item2 != null)
                {
                    // Add detected tile to the dictionary
                    detectedTiles.Add(item, (tileInfo.Item2, tileInfo.Item3));
                }
            }

            for (int i = 0; i < 4; i++)
            {
                Vector2 dir = new(item.X + dirX[i], item.Y + dirY[i]);

                if (IsValid(dir, center, searched, limit))
                {
                    toSearch.Enqueue(dir);
                    searched.Add(dir);
                }
            }
        }

        searched.Clear();
        return detectedTiles;
    }

    private static int refreshCounter = 0;
    private static double _average = 0;
    public static void AddToAverage(double value)
    {
        refreshCounter++;
        _average += (value - _average) / refreshCounter;
    }

    /// <summary>
    /// Searches the entire location using a Breadth-First Search (BFS) algorithm.
    /// </summary>
    /// <param name="detectedTiles">A pre-allocated list to store detected tile information.</param>
    /// <returns>The same list populated with detected tile data.</returns>
    public static Dictionary<string, List<(Vector2 position, string name)>> SearchLocation(
        Dictionary<string, List<(Vector2 position, string name)>> detectedTiles)
    {
        var currentLocation = Game1.currentLocation;
        foreach (var catList in detectedTiles.Values)
            catList.Clear();
        ToSearch.Clear();

        int widthInTiles = currentLocation.Map.DisplayWidth / Game1.tileSize;
        int heightInTiles = currentLocation.Map.DisplayHeight / Game1.tileSize;
        // 2D array for visited flags:
        bool[,] visited = new bool[widthInTiles + 2, heightInTiles + 2];

        // Start BFS from the player's tile.
        (int x, int y) start = ((int)Game1.player.Tile.X, (int)Game1.player.Tile.Y);
        // Mark visited and enqueue:
        ToSearch.Clear();
        ToSearch.Enqueue(start);
        // Add 1 to account for doors with -1 indexes
        visited[start.x+1, start.y+1] = true;

        while (ToSearch.Count > 0)
        {
            var (cx, cy) = ToSearch.Dequeue();
            Vector2 currentTile = new(cx, cy);
            var tileInfo = CheckTile(currentTile, currentLocation, true);

            if (tileInfo.Item1 && tileInfo.name != null)
            {
                // Add the detected tile information as a tuple to the list
                if (!detectedTiles.TryGetValue(tileInfo.category, out List<(Vector2 position, string name)>? category))
                {
                    category = [];
                    detectedTiles[tileInfo.category] = category;
                }
                category.Add((currentTile, tileInfo.name));
            }

            // Now enqueue neighbors:
            foreach (var (dx, dy) in Directions)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                // Check in-bounds:
                if (nx < 0 || nx > widthInTiles || ny < 0 || ny > heightInTiles)
                {
                    if (!DoorUtils.IsWarpAtTile((nx, ny), currentLocation))
                        continue;
                }

                // Not visited yet?
                // Add extra 1 to offset for -1 indexes
                if (!visited[nx+1, ny+1])
                {
                    visited[nx+1, ny+1] = true;

                    ToSearch.Enqueue((nx, ny));
                }
            }
        }

        return detectedTiles;
    }

    /// <summary>
    /// Checks if the provided tile position is within the range/radius and whether the tile has already been checked or not.
    /// </summary>
    /// <param name="item">The position of the tile to be searched.</param>
    /// <param name="center">The starting point of the search.</param>
    /// <param name="searched">The list of searched items.</param>
    /// <param name="limit">The radius of search</param>
    /// <returns>Returns true if the tile is valid for search.</returns>
    public static bool IsValid(Vector2 item, Vector2 center, HashSet<Vector2> searched, int limit)
    {
        if (Math.Abs(item.X - center.X) > limit)
            return false;
        if (Math.Abs(item.Y - center.Y) > limit)
            return false;

        if (searched.Contains(item))
            return false;

        return true;
    }

    public static (bool, string? name, string category) CheckTile(Vector2 position, GameLocation currentLocation,
        bool lessInfo = false)
    {
        (string? name, CATEGORY? category) = TileInfo.GetNameWithCategoryAtTile(position, currentLocation, lessInfo);
        if (name == null)
            return (false, null, CATEGORY.Other.ToString());

        category ??= CATEGORY.Other;

        return (true, name, category.ToString());
    }

    public void CheckTileAndPlaySound(Vector2 position, GameLocation currentLocation)
    {
        try
        {
            if (currentLocation.isObjectAtTile((int)position.X, (int)position.Y))
            {
                (string? name, CATEGORY category) objDetails =
                    TileInfo.GetObjectAtTile(currentLocation, (int)position.X, (int)position.Y);
                string? objectName = objDetails.name;
                CATEGORY category = objDetails.category;
                StardewValley.Object obj = currentLocation.getObjectAtTile((int)position.X, (int)position.Y);

                if (objectName != null)
                {
                    objectName = objectName.ToLower().Trim();

                    if (obj is Furniture furniture)
                    {
                        if (!_furniture.Contains(furniture))
                        {
                            _furniture.Add(furniture);
                            PlaySoundAt(position, objectName, category, currentLocation);
                        }
                    }
                    else
                        PlaySoundAt(position, objectName, category, currentLocation);
                }
            }
            else
            {
                (string? name, CATEGORY? category) = TileInfo.GetNameWithCategoryAtTile(position, currentLocation);
                if (name != null)
                {
                    category ??= CATEGORY.Other;

                    PlaySoundAt(position, name, category, currentLocation);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"{e.Message}\n{e.StackTrace}\n{e.Source}");
        }
    }

    public void PlaySoundAt(Vector2 position, string searchQuery, CATEGORY category, GameLocation currentLocation)
    {
        #region Check whether to skip the object or not

        // Skip if player is directly looking at the tile
        if (CurrentPlayer.FacingTile.Equals(position))
            return;

        if (!RadarFocus)
        {
            if ((Exclusions.Contains(category.ToString().ToLower().Trim()) ||
                 Exclusions.Contains(searchQuery.ToLower().Trim())))
                return;

            // Check if a word in searchQuery matches the one in exclusions list
            string[] sqArr = searchQuery.ToLower().Trim().Split(" ");
            for (int j = 0; j < sqArr.Length; j++)
            {
                if (Exclusions.Contains(sqArr[j]))
                    return;
            }
        }
        else
        {
            if (Focus.Count >= 0)
            {
                bool found = false;

                // Check if a word in searchQuery matches the one in focus list
                string[] sqArr = searchQuery.ToLower().Trim().Split(" ");
                for (int j = 0; j < sqArr.Length; j++)
                {
                    if (Focus.Contains(sqArr[j]))
                    {
                        found = true;
                        break;
                    }
                }

                // This condition has to be after the for loop
                if (!found && !(Focus.Contains(category.ToString().ToLower().Trim()) ||
                                Focus.Contains(searchQuery.ToLower().Trim())))
                    return;
            }
            else
                return;
        }

        #endregion

        if (RadarDebug)
            Log.Error($"{RadarFocus}\tObject:{searchQuery.ToLower().Trim()}\tPosition: X={position.X} Y={position.Y}");

        int px = (int)Game1.player.Tile.X; // Player's X postion
        int py = (int)Game1.player.Tile.Y; // Player's Y postion

        int ox = (int)position.X; // Object's X postion
        int oy = (int)position.Y; // Object's Y postion

        int dx = ox - px; // Distance of object's X position
        int dy = oy - py; // Distance of object's Y position

        if (dy < 0 && (Math.Abs(dy) >= Math.Abs(dx))) // Object is at top
        {
            currentLocation.localSound(GetSoundName(category, "top"), position);
        }
        else if (dx > 0 && (Math.Abs(dx) >= Math.Abs(dy))) // Object is at right
        {
            currentLocation.localSound(GetSoundName(category, "right"), position);
        }
        else if (dx < 0 && (Math.Abs(dx) > Math.Abs(dy))) // Object is at left
        {
            currentLocation.localSound(GetSoundName(category, "left"), position);
        }
        else if (dy > 0 && (Math.Abs(dy) > Math.Abs(dx))) // Object is at bottom
        {
            currentLocation.localSound(GetSoundName(category, "bottom"), position);
        }
    }

    public static string GetSoundName(CATEGORY category, string post)
    {
        string soundName = $"_{post}";

        if (!MainClass.Config.RadarStereoSound)
            soundName = $"_mono{soundName}";

        if (category == CATEGORY.Farmers) // Villagers and farmers
            soundName = $"npc{soundName}";
        else if (category == CATEGORY.Animals) // Farm Animals
            soundName = $"npc{soundName}";
        else if (category == CATEGORY.NPCs) // Other npcs, also includes enemies
            soundName = $"npc{soundName}";
        else if (category == CATEGORY.Water) // Water tiles
            soundName = $"obj{soundName}";
        else if (category == CATEGORY.Furniture) // Furnitures
            soundName = $"obj{soundName}";
        else if (category == CATEGORY.Other) // Other Objects
            soundName = $"obj{soundName}";
        else if (category == CATEGORY.Crops) // Crops
            soundName = $"obj{soundName}";
        else if (category == CATEGORY.Trees) // Trees
            soundName = $"obj{soundName}";
        else if (category == CATEGORY.Buildings) // Buildings
            soundName = $"obj{soundName}";
        else if (category == CATEGORY.MineItems) // Mine items
            soundName = $"obj{soundName}";
        else if (category == CATEGORY.Containers) // Chests
            soundName = $"obj{soundName}";
        else if (category == CATEGORY.Debris) // Grass and debris
            soundName = $"obj{soundName}";
        else if (category == CATEGORY.Flooring) // Flooring
            soundName = $"obj{soundName}";
        else // Default
            soundName = $"obj{soundName}";

        return soundName;
    }

    public bool ToggleFocus()
    {
        RadarFocus = !RadarFocus;

        if (RadarFocus)
            EnableFocus();
        else
            DisableFocus();

        return RadarFocus;
    }

    public void EnableFocus()
    {
        _tempExclusions = [.. Exclusions];
        Exclusions.Clear();
    }

    public void DisableFocus()
    {
        Exclusions = [.. _tempExclusions];
        _tempExclusions.Clear();
    }
}
