using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.SpecialOrders;
using StardewValley.TokenizableStrings;

namespace stardew_access.Utils;

using Translation;
using StardewValley.TerrainFeatures;
using StardewValley.Menus;

/// <summary>
/// Provides methods to locate tiles of interest in various game locations that are conditional or unpredictable (I.E. not static).
/// </summary>
/// <remarks>
/// The DynamicTiles class currently supports the following location types:
/// - Beach
/// - BoatTunnel
/// - CommunityCenter
/// - Farm
/// - FarmHouse
/// - Forest
/// - IslandFarmHouse
/// - IslandLocation
/// - LibraryMuseum
/// - Town
/// - MineShaft
///
/// And the following Island LocationTypes:
/// - IslandNorth
/// - IslandWest
/// - VolcanoDungeon
///
/// The class also supports the following named locations:
/// - Barn (and its upgraded versions)
/// - Coop (and its upgraded versions)
/// - Mastery Cave
/// - Witch Hut
///
/// The class does not yet support the following location types, but consider adding support in future updates:
/// - AbandonedJojaMart
/// - AdventureGuild
/// - BathHousePool
/// - BeachNightMarket
/// - BugLand
/// - BusStop
/// - Caldera
/// - Cellar
/// - Club
/// - Desert
/// - FarmCave
/// - FishShop
/// - JojaMart
/// - ManorHouse
/// - MermaidHouse
/// - Mine
/// - Mountain
/// - MovieTheater
/// - Railroad
/// - SeedShop
/// - Sewer
/// - Submarine
/// - Summit
/// - WizardHouse
/// - Woods
///
/// The class does not yet support the following named locations, but consider adding support in future updates:
/// - "AnimalShop"
/// - "Backwoods"
/// - "BathHouse_Entry"
/// - "BathHouse_MensLocker"
/// - "BathHouse_WomensLocker"
/// - "Blacksmith"
/// - "ElliottHouse"
/// - "FarmGreenHouse"
/// - "Greenhouse"
/// - "HaleyHouse"
/// - "HarveyRoom"
/// - "Hospital"
/// - "JoshHouse"
/// - "LeahHouse"
/// - "LeoTreeHouse"
/// - "Saloon"
/// - "SamHouse"
/// - "SandyHouse"
/// - "ScienceHouse"
/// - "SebastianRoom"
/// - "SkullCave"
/// - "Sunroom"
/// - "Tent"
/// - "Trailer"
/// - "Trailer_Big"
/// - "Tunnel"
/// - "WitchHut"
/// - "WitchSwamp"
/// - "WitchWarpCave"
/// - "WizardHouseBasement"
///
/// The class does not yet support the following IslandLocation location types, but consider adding support in future updates:
/// - IslandEast
/// - IslandFarmCave
/// - IslandFieldOffice
/// - IslandHut
/// - IslandShrine
/// - IslandSouth
/// - IslandSouthEast
/// - IslandSouthEastCave
/// - IslandWestCave1
///
/// The class does not yet support the following IslandLocation named locations, but consider adding support in future updates:
/// - "CaptainRoom"
/// - "IslandNorthCave1"
/// - "QiNutRoom"
/// </remarks>
public class DynamicTiles
{
    // Static instance for the singleton pattern
    private static DynamicTiles? _instance;

    /// <summary>
    /// The singleton instance of the <see cref="DynamicTiles"/> class.
    /// </summary>
    public static DynamicTiles Instance
    {
        get
        {
            _instance ??= new DynamicTiles();
            return _instance;
        }
    }

    // HashSet for storing which unimplemented locations have been previously logged
    private static readonly HashSet<object> loggedLocations = [];

    // Dictionary of coordinates for feeding benches in barns and coops
    private static readonly Dictionary<string, (int minX, int maxX, int y)> FeedingBenchBounds = new()
    {
        { "Barn", (8, 11, 3) },
        { "Barn2", (8, 15, 3) },
        { "Big Barn", (8, 15, 3) },
        { "Barn3", (8, 19, 3) },
        { "Deluxe Barn", (8, 19, 3) },
        { "Coop", (6, 9, 3) },
        { "Coop2", (6, 13, 3) },
        { "Big Coop", (6, 13, 3) },
        { "Coop3", (6, 17, 3) },
        { "Deluxe Coop", (6, 17, 3) }
    };

    // List to hold Egg Festival eggs
    private static List<Prop>? eggs;

    // Generated DynamicTile token cache
    private static readonly Dictionary<(string? loc, string tile), string> _dynamic_token_cache = [];
    // Maps dynamic keys to categories
    private static readonly Dictionary<string, string> DynamicTileCategories = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicTiles"/> class.
    /// </summary>
    static DynamicTiles()
    {
        string fileName = "dynamic_tile_categories.json";
        // Load the dynamic_tile_categories.json file if it exists
        if (JsonLoader.TryLoadJsonDictionary(fileName, out DynamicTileCategories, subdir: "assets/TileData"))
            Log.Trace($"Loaded assets/TileData/{fileName} with {DynamicTileCategories.Count} keys.", true);
        else
            Log.Debug($"Unable to load assets/TileData/{fileName}.");
    }

    /// <summary>
    /// Normalizes the specified string by removing or converting any disallowed characters,
    /// inserting underscores before uppercase letters, and converting uppercase to lowercase.
    /// Intended for generating fluent-token-friendly strings.
    /// </summary>
    /// <param name="input">
    /// The string to normalize. May contain letters, digits, underscores, and punctuation.
    /// </param>
    /// <returns>
    /// A normalized token string where punctuation and other disallowed characters become
    /// underscores, uppercase letters are snake-cased, and everything else is left as-is or lowercased.
    /// </returns>
    private static string NormalizeToken(string input)
    {
        // If the input is null or empty, there's nothing to do; just bail.
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new System.Text.StringBuilder(input.Length);

        foreach (char c in input)
        {
            // 1. If character is NOT a letter, digit, or underscore
            //    => we’ll convert it to underscore (but avoid doubles).
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                // If this isn’t the very first char AND the last appended char isn’t underscore, append underscore.
                if (sb.Length > 0 && sb[^1] != '_')
                    sb.Append('_');

                // Then skip adding the original c (punctuation/space/dash).
                // This effectively "eats" consecutive disallowed chars without
                // stacking multiple underscores.
                continue;
            }

            // 2. If uppercase [A–Z]
            if (c >= 'A' && c <= 'Z')
            {
                // Insert underscore if:
                //   - We’re not at the first character
                //   - The last appended char isn’t already underscore
                if (sb.Length > 0 && sb[^1] != '_')
                    sb.Append('_');

                // Now append the lowercase equivalent
                // (ASCII offset: 'A' + 32 => 'a')
                sb.Append((char)(c + 32));
                continue;
            }

            // 3. Otherwise, just append as-is. 
            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a dynamic Fluent-style token that incorporates the current location (or event)
    /// and a tile action string, applying normalization and caching to avoid repeated computation.
    /// </summary>
    /// <param name="currentLocation">
    /// The in-game location used to derive a location/event name for the token.
    /// </param>
    /// <param name="tileAction">
    /// The raw tile action string to include in the final token.
    /// </param>
    /// <returns>
    /// A string in the format "dynamic_tile-<normalizedLocationOrEvent>-<normalizedAction>".
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="currentLocation"/> or <paramref name="tileAction"/> is null.
    /// </exception>
    internal static string GetDynamicTileFluentToken(GameLocation currentLocation, string tileAction)
    {
        // Basic sanity checks
        if (currentLocation is null)
            throw new ArgumentNullException(nameof(currentLocation));
        if (tileAction is null)
            throw new ArgumentNullException(nameof(tileAction));

        // Decide which name to use based on whether an event is running
        var locationOrEventName =
            currentLocation.currentEvent is null
                ? currentLocation.NameOrUniqueName
                : !string.IsNullOrEmpty(currentLocation.currentEvent.FestivalName)
                    ? currentLocation.currentEvent.FestivalName
                    : currentLocation.currentEvent.id;

        // Construct a key (just keep it small & simple)
        var key = (locationOrEventName, tileAction);

        // Check the cache
        if (_dynamic_token_cache.TryGetValue(key, out string? cachedResult))
            return cachedResult; // Reuse the stored token

        // We missed the cache—compute it
        string normalizedLocation = NormalizeToken(locationOrEventName);
        string normalizedAction   = NormalizeToken(tileAction);
        string result = $"dynamic_tile-{normalizedLocation}-{normalizedAction}";

        // Store it for next time
        _dynamic_token_cache[key] = result;
        return result;
    }

    /// <summary>
    /// Retrieves information about interactables, NPCs, or other features at a given coordinate in a Beach.
    /// </summary>
    /// <param name="beach">The Beach to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? name, CATEGORY? category) GetBeachInfo(Beach beach, int x, int y, string? tileAction, bool lessInfo = false)
    {
        if (MainClass.ModHelper == null)
        {
            return (null, null);
        }
        if (MainClass.ModHelper.Reflection.GetField<NPC>(beach, "oldMariner").GetValue() is NPC mariner && mariner.Tile == new Vector2(x, y))
        {
            return ("dynamic_tile-beach-old_mariner", CATEGORY.NPCs);
        }
        else if (x == 58 && y == 13)
        {
            if (!beach.bridgeFixed.Value)
            {
                return (Translator.Instance.Translate("prefix-repair", new { content = Translator.Instance.Translate("tile_name-bridge") }), CATEGORY.Pending);
            }
            else
            {
                return ("tile_name-bridge", CATEGORY.Bridges);
            }
        }

        if (Game1.CurrentEvent is not null && Game1.CurrentEvent.id == "13" && x == 53 && y == 8)
        {
            return ("item-haley_bracelet-name", CATEGORY.DroppedItems);
        }
        return (null, null);
    }

    /// <summary>
    /// Retrieves information about interactables or other features at a given coordinate in a BoatTunnel.
    /// </summary>
    /// <param name="boatTunnel">The BoatTunnel to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? name, CATEGORY? category) GetBoatTunnelInfo(BoatTunnel boatTunnel, int x, int y, string? tileAction, bool lessInfo = false)
    {
        // Check if the player has received the specified mail or not
        static bool HasMail(string mail) => Game1.MasterPlayer.hasOrWillReceiveMail(mail);

        // If the position matches one of the interactable elements in the boat tunnel
        if ((x, y) == (4, 9) || (x, y) == (6, 8) || (x, y) == (8, 9))
        {
            string mail = (x, y) switch
            {
                (4, 9) => "willyBoatTicketMachine",
                (6, 8) => "willyBoatHull",
                (8, 9) => "willyBoatAnchor",
                _ => throw new InvalidOperationException("Unexpected (x, y) values"),
            };

            string itemName = Translator.Instance.Translate(
                (x, y) switch
                {
                    (4, 9) => "tile_name-ticket_machine",
                    (6, 8) => "tile_name-boat_hull",
                    (8, 9) => "tile_name-boat_anchor",
                    _ => throw new InvalidOperationException("Unexpected (x, y) values"),
                }
            );

            CATEGORY category = (x, y) == (4, 9)
                ? !HasMail(mail)
                        ? CATEGORY.Pending
                        : !HasMail("willyBoatHull") || !HasMail("willyBoatAnchor") ? CATEGORY.Decor : CATEGORY.Interactables
                : !HasMail(mail) ? CATEGORY.Pending : CATEGORY.Decor;

            return ((!HasMail(mail) ? Translator.Instance.Translate("prefix-repair", new { content = itemName }) : itemName), category);
        }

        return (null, null);
    }

    /// <summary>
    /// Retrieves information about interactables or other features at a given coordinate in a CommunityCenter.
    /// </summary>
    /// <param name="communityCenter">The CommunityCenter to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? name, CATEGORY? category) GetCommunityCenterInfo(CommunityCenter communityCenter, int x, int y, bool lessInfo = false)
    {
        if (communityCenter.missedRewardsChestVisible.Value && x == 22 && y == 10)
        {
            return ("tile_name-missed_reward_chest", CATEGORY.Containers);
        }

        return (null, null);
    }

    /// <summary>
    /// Gets the building information for a given position on a farm.
    /// </summary>
    /// <param name="building">The Building instance.</param>
    /// <param name="x">The x-coordinate of the position.</param>
    /// <param name="y">The y-coordinate of the position.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the door or building found, or (null, null) if no door or building is found.</returns>
    private static (string? name, CATEGORY? category) GetBuildingInfo(Building building, int x, int y, bool lessInfo = false)
    {
        // Internal name; never translated. Mill stays "Mill"
        string type = building.buildingType.Value;
        // Translated name, E.G> "Mill" becomes "Molino" for Spanish.
        // not all buildings have translated names, E.G. "Petbowl" and "Farmhouse" remain untranslated.
        // TODO add translation keys for untranslated building names.
        string name = TokenParser.ParseText(building.GetData().Name);
        int buildingTileX = building.tileX.Value;
        int buildingTileY = building.tileY.Value;
        // Calculate differences in x and y coordinates
        int offsetX = x - buildingTileX;
        int offsetY = y - buildingTileY;

        // Set default category
        CATEGORY category = CATEGORY.Buildings;
        if (type == "Shipping Bin")
        {
            category = CATEGORY.Containers;
        }
        else if (building is PetBowl bowl)
        {
            category = bowl.HasPet()
                ? !bowl.watered.Value
                    ? CATEGORY.Pending
                    : CATEGORY.Interactables
                : CATEGORY.Decor;

            name = Translator.Instance.Translate("tile-pet_bowl-prefix", new
            {
                is_in_use = bowl.HasPet() ? 1 : 0,
                is_empty = !bowl.watered.Value ? 1 : 0,
                name
            });
        }
        else if (building.GetIndoors() is Cabin cabin)
        {
            string ownerName = cabin.owner.displayName;
            if (offsetX is 2 && offsetY is 1)
            {
                name = string.IsNullOrWhiteSpace(ownerName)
                    ? $"{name} {Translator.Instance.Translate("tile-door")}" // Cabin Door
                    : $"{cabin.owner.Name} {name} {Translator.Instance.Translate("tile-door")}"; // [Owner] Cabin Door
                category = CATEGORY.Doors;
                goto PassableTilesCheck;
            }

            if (offsetX is not 4 || offsetY is not 2)
            {
                // Prepend cabin's owner name to the cabin
                if (cabin.HasOwner && !cabin.IsOwnedByCurrentPlayer && !string.IsNullOrWhiteSpace(ownerName))
                    name = $"{cabin.owner.Name} {name}";
                goto PassableTilesCheck;
            }

            // Cabin Mail Box

            category = CATEGORY.Interactables;

            if (!cabin.IsOwnedByCurrentPlayer)
            {
                // Prepend the owner's name to mail box
                name = string.IsNullOrWhiteSpace(ownerName)
                    ? $"{name} {Translator.Instance.Translate("tile_name-mail_box")}" // Cabin Mail Box
                    : $"{cabin.owner.Name} {Translator.Instance.Translate("tile_name-mail_box")}"; // [Owner] Mail Box
                goto PassableTilesCheck;
            }

            // Mail Box (with unread status)
            name = Translator.Instance.Translate("tile_name-mail_box");
            var mailbox = Game1.player.mailbox;
            if (mailbox is not null && mailbox.Count > 0)
            {
                name = Translator.Instance.Translate("tile_name-mail_box-unread_mail_count-prefix", new
                {
                    mail_count = mailbox.Count,
                    content = name
                });
                category = CATEGORY.Ready;
            }
        }
        else if (building.GetIndoors() is FarmHouse farmHouse && farmHouse.HasOwner && !farmHouse.IsOwnedByCurrentPlayer)
        {
            name = $"{farmHouse.owner.displayName} {name}";
        }
        // If the building is a FishPond, prepend the fish name
        else if (building is FishPond fishPond && fishPond.fishType.Value != "0" && fishPond.fishType.Value != "")
        {
            name = $"{ItemRegistry.GetData(fishPond.fishType.Value)?.DisplayName ?? ""}{name}";
            category = CATEGORY.Fishponds;
        }
        // Check if the position matches the human door
        if (building.humanDoor.Value.X == offsetX && building.humanDoor.Value.Y == offsetY)
        {
            name = Translator.Instance.Translate("suffix-building_door", new { content = name });
            category = CATEGORY.Doors;
        }
        // Check if the position matches the animal door. In case of barns, as the animal door is 2 tiles wide, the following if condition checks for both animal door tiles.
        else if ((building.animalDoor.Value.X == offsetX || (type == "Barn" && building.animalDoor.Value.X == offsetX - 1)) && building.animalDoor.Value.Y == offsetY)
        {
            name = Translator.Instance.Translate("tile-building_animal_door-suffix", new
            {
                name, // using inferred member name; silences IDE0037
                is_open = (building.animalDoorOpen.Value) ? 1 : 0,
                less_info = lessInfo ? 1 : 0
            });
            category = CATEGORY.Doors;
        }
        // Special handling for Mill buildings
        else if (type == "Mill")
        {
            if (offsetY == 1)
            {
                // Check if the position matches the input
                if (offsetX == 1)
                {
                    name = Translator.Instance.Translate("suffix-mill_input", new { content = name });
                    category = CATEGORY.Interactables;
                }
                // Check if the position matches the output
                else if (offsetX == 3)
                {
                    name = Translator.Instance.Translate("suffix-mill_output", new { content = name });
                    category = CATEGORY.Interactables;
                }
            }
        }
    // Any building tile not matched will return building's name and Buildings category 

    PassableTilesCheck:
        if (!building.isTilePassable(new Vector2(x, y)))
        {
            return (name, category);
        }
        else
        {
            // Ignore parts of buildings that are outside, I.E. Farmhouse porch.
            return (null, null);
        }
    }

    /// <summary>
    /// Retrieves information about interactables or other features at a given coordinate in a Farm.
    /// </summary>
    /// <param name="farm">The Farm to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? name, CATEGORY? category) GetFarmInfo(Farm farm, int x, int y, string? tileAction, bool lessInfo = false)
    {
        var mainMailboxPos = farm.GetMainMailboxPosition();
        Building building = farm.getBuildingAt(new Vector2(x, y));

        if (mainMailboxPos.X == x && mainMailboxPos.Y == y)
        {
            string mailboxName = Translator.Instance.Translate("tile_name-mail_box");
            CATEGORY mailboxCategory = CATEGORY.Interactables;

            if (!(farm.GetMainFarmHouse().GetIndoors() as FarmHouse)!.IsOwnedByCurrentPlayer)
            {
                mailboxName = $"{(farm.GetMainFarmHouse().GetIndoors() as FarmHouse)!.owner.displayName} {mailboxName}";
                return (mailboxName, mailboxCategory);
            }

            var mailbox = Game1.player.mailbox;
            if (mailbox is not null && mailbox.Count > 0)
            {
                mailboxName = Translator.Instance.Translate("tile_name-mail_box-unread_mail_count-prefix", new
                {
                    mail_count = mailbox.Count,
                    content = mailboxName
                });
                mailboxCategory = CATEGORY.Ready;
            }

            return (mailboxName, mailboxCategory);
        }
        else if ((farm.GetMainFarmHouse().tileX.Value + 1 == x || farm.GetMainFarmHouse().tileX.Value + 2 == x) && farm.GetMainFarmHouse().tileY.Value + 2 == y)
        {
            bool flag = !Game1.player.hasOrWillReceiveMail("TH_LumberPile") && Game1.player.hasOrWillReceiveMail("TH_SandDragon");
            return ("dynamic_tile-farm-lumber_pile", flag ? CATEGORY.Quest : CATEGORY.Decor);
        }
        else if (building is not null) // Check if there is a building at the current position
        {
            return GetBuildingInfo(building, x, y, lessInfo);
        }

        // Speaks the Grandpa Evaluation score i.e., numbers of candles lit on the shrine after year 3
        if (farm.GetGrandpaShrinePosition().X == x && farm.GetGrandpaShrinePosition().Y == y)
        {
            return (Translator.Instance.Translate("dynamic_tile-farm-grandpa_shrine", new
            {
                candles = farm.grandpaScore.Value
            }), CATEGORY.Interactables);
        }

        return (null, null);
    }

    /// <summary>
    /// Retrieves information about interactables or other features at a given coordinate in a FarmHouse.
    /// </summary>
    /// <param name="farmHouse">The FarmHouse to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? name, CATEGORY? category) GetFarmHouseInfo(FarmHouse farmHouse, int x, int y, string? tileAction, bool lessInfo = false)
    {
        if (farmHouse.upgradeLevel >= 1)
        {
            int kitchenX = farmHouse.getKitchenStandingSpot().X;
            int kitchenY = farmHouse.getKitchenStandingSpot().Y - 1;

            if (kitchenX == x && kitchenY == y)
            {
                return ("tile_name-stove", CATEGORY.Interactables);
            }
            else if (kitchenX + 1 == x && kitchenY == y)
            {
                return ("tile_name-sink", CATEGORY.Water);
            }
            else if (farmHouse.fridgePosition.X == x && farmHouse.fridgePosition.Y == y)
            {
                return ("tile_name-fridge", CATEGORY.Containers);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Retrieves information about interactables or other features at a given coordinate in a Forest.
    /// </summary>
    /// <param name="forest">The Forest to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? name, CATEGORY? category) GetForestInfo(Forest forest, int x, int y, string? tileAction, bool lessInfo = false)
    {
        if (forest.travelingMerchantDay) // does cart close or vanish after `Game1.timeOfDay < 2000`?
        {
            Point cartOrigin = forest.GetTravelingMerchantCartTile();
            if (x == cartOrigin.X + 4 && y == cartOrigin.Y + 1)
                return ("tile_name-traveling_cart", CATEGORY.Interactables);
            else if (x == cartOrigin.X && y == cartOrigin.Y + 1)
                return ("tile_name-traveling_cart_pig", CATEGORY.NPCs);
        }
        if (Game1.MasterPlayer.mailReceived.Contains("raccoonTreeFallen") && x == 56 && y == 6)
        {
            return ("tile-forest-giant_tree_sump", forest.stumpFixed.Value ? CATEGORY.Decor : CATEGORY.Quest);
        }

        return (null, null);
    }

    /// <summary>
    /// Retrieves information about interactables, NPCs, or other features at a given coordinate in an IslandFarmHouse.
    /// </summary>
    /// <param name="islandFarmHouse">The IslandFarmHouse to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? name, CATEGORY? category) GetIslandFarmHouseInfo(IslandFarmHouse islandFarmHouse, int x, int y, string? tileAction, bool lessInfo = false)
    {
        int fridgeX = islandFarmHouse.fridgePosition.X;
        int fridgeY = islandFarmHouse.fridgePosition.Y;
        if (fridgeX - 2 == x && fridgeY == y)
        {
            return ("tile_name-stove", CATEGORY.Interactables);
        }
        else if (fridgeX - 1 == x && fridgeY == y)
        {
            return ("tile_name-sink", CATEGORY.Water);
        }
        else if (fridgeX == x && fridgeY == y)
        {
            return ("tile_name-fridge", CATEGORY.Containers);
        }

        return (null, null);
    }

    /// <summary>
    /// Retrieves information about interactables, NPCs, or other features at a given coordinate in an IslandNorth.
    /// </summary>
    /// <param name="islandNorth">The IslandNorth to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? name, CATEGORY? category) GetIslandHutInfo(IslandHut islandHut, int x, int y, string? tileAction, bool lessInfo = false)
    {
        if (x == 10 && y == 8)
        {
            return ("dynamic_tile-island_hut-potted_tree", islandHut.treeHitLocal ? CATEGORY.Decor : CATEGORY.Ready);
        }

        // Return (null, null) if no relevant object is found
        return (null, null);
    }

    /// <summary>
    /// Retrieves information about interactables, NPCs, or other features at a given coordinate in an IslandNorth.
    /// </summary>
    /// <param name="islandNorth">The IslandNorth to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? name, CATEGORY? category) GetIslandNorthInfo(IslandNorth islandNorth, int x, int y, string? tileAction, bool lessInfo = false)
    {
        // Check if the trader is activated and the coordinates match the trader's location
        if (islandNorth.traderActivated.Value && x == 36 && y == 71)
        {
            return ("npc_name-island_trader", CATEGORY.Interactables);
        }
        else if (!islandNorth.caveOpened.Value && y == 47 && (x == 21 || x == 22))
        {
            return ("tile-resource_clump-boulder-name", CATEGORY.ResourceClumps);
        }

        // Return (null, null) if no relevant object is found
        return (null, null);
    }

    /// <summary>
    /// Retrieves information about interactables, NPCs, or other features at a given coordinate in an IslandWest.
    /// </summary>
    /// <param name="islandWest">The IslandWest to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? name, CATEGORY? category) GetIslandWestInfo(IslandWest islandWest, int x, int y, string? tileAction, bool lessInfo = false)
    {
        // Check if the coordinates match the shipping bin's location
        if ((islandWest.shippingBinPosition.X == x || (islandWest.shippingBinPosition.X + 1) == x) && islandWest.shippingBinPosition.Y == y)
        {
            return ("building_name-shipping_bin", CATEGORY.Containers);
        }

        // Return (null, null) if no relevant object is found
        return (null, null);
    }

    /// <summary>
    /// Retrieves information about tiles at a given coordinate in a VolcanoDungeon.
    /// </summary>
    /// <param name="dungeon">The VolcanoDungeon to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name of the tile and the CATEGORY, or (null, null) if no relevant tile is found.</returns>
    private static (string? name, CATEGORY? category) GetVolcanoDungeonInfo(VolcanoDungeon dungeon, int x, int y, string? tileAction, bool lessInfo = false)
    {
        if (!lessInfo)
        {
            if (dungeon.IsCooledLava(x, y))
            {
                return ("tile-cooled_lava-name", CATEGORY.Water);
            }
            else if (StardewValley.Monsters.LavaLurk.IsLavaTile(dungeon, x, y))
            {
                return ("tile-lava-name", CATEGORY.Water);
            }
        }


        // Back[547]: The gate preventing access to the exit {Buildings[0]: Closed & Buildings[-1]:Opened} 
        // Back[496]: Pressure pad (Unpressed)
        // Back[497]: Pressure pad (pressed)
        // Buildings[546]: Left pillar of gate
        // Buildings[548]: Right pillar of gate

        int back = dungeon.getTileIndexAt(new Point(x, y), "Back");

        if (back is 496 or 497)
        {
            return (Translator.Instance.Translate("tile-volcano_dungeon-pressure_pad", new { active = back is 497 ? 1 : 0}), CATEGORY.Interactables);
        }

        if (back is 547 && dungeon.getTileIndexAt(new Point(x, y), "Buildings") is 0)
        {
            return ("tile-volcano_dungeon-gate", CATEGORY.Doors);
        }

        return (null, null);
    }

    /// <summary>
    /// Retrieves information about interactables, NPCs, or other features at a given coordinate in a named IslandLocation.
    /// </summary>
    /// <param name="islandLocation">The named IslandLocation to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? name, CATEGORY? category) GetNamedIslandLocationInfo(IslandLocation islandLocation, int x, int y, string? tileAction, bool lessInfo = false)
    {
        object locationType = islandLocation is not null and IslandLocation ? islandLocation.Name ?? "Undefined Island Location" : islandLocation!.GetType();

        // Implement specific logic for named  IslandLocations here, if necessary

        if (locationType.ToString()!.Contains("qinutroom", StringComparison.OrdinalIgnoreCase))
        {
            if (Game1.player.team.SpecialOrderActive("QiChallenge12") && x == 1 && y == 4)
            {
                return ("dynamic_tile-qi_nut_room-collection_box", CATEGORY.Interactables);
            }
            return (null, null);
        }

        // Unimplemented locations are logged.
        // Check if the location has already been logged
        if (!loggedLocations.Contains(locationType))
        {
            // Log the message
            Log.Debug($"Called GetNamedIslandLocationInfo with unimplemented IslandLocation of type {islandLocation.GetType()} and name {islandLocation.Name}", true);

            // Add the location to the HashSet to prevent logging it again
            loggedLocations.Add(locationType);
        }

        return (null, null);
    }

    /// <summary>
    /// Retrieves the name of the IslandGemBird based on its item index value.
    /// </summary>
    /// <param name="bird">The IslandGemBird instance.</param>
    /// <returns>A string representing the name of the IslandGemBird.</returns>
    private static String GetGemBirdName(IslandGemBird bird)
    {
        // Use a switch expression to return the appropriate bird name based on the item index value
        return bird.itemIndex.Value switch
        {
            "60" => "npc_name-emerald_gem_bird",
            "62" => "npc_name-aquamarine_gem_bird",
            "64" => "npc_name-ruby_gem_bird",
            "66" => "npc_name-amethyst_gem_bird",
            "68" => "npc_name-topaz_gem_bird",
            _ => "npc_name-gem_bird", // Default case for when the item index does not match any of the specified values
        };
    }

    /// <summary>
    /// Gets the parrot perch information at the specified tile coordinates in the given island location.
    /// </summary>
    /// <param name="x">The x-coordinate of the tile to check.</param>
    /// <param name="y">The y-coordinate of the tile to check.</param>
    /// <param name="islandLocation">The IslandLocation where the parrot perch might be found.</param>
    /// <returns>A string containing the parrot perch information if a parrot perch is found at the specified tile; null if no parrot perch is found.</returns>
    private static string? GetParrotPerchAtTile(IslandLocation islandLocation, int x, int y)
    {
        // Use LINQ to find the first parrot perch at the specified tile (x, y) coordinates
        var foundPerch = islandLocation.parrotUpgradePerches.FirstOrDefault(perch => perch.tilePosition.Value.Equals(new Point(x, y)));

        // If a parrot perch was found at the specified tile coordinates
        if (foundPerch != null)
        {
            if (foundPerch.upgradeName.Value == "GoldenParrot") return Translator.Instance.Translate("building-golden_parrot");
            if (islandLocation is IslandWest islandWest)
            {
                if (islandWest.farmhouseMailbox.Value && foundPerch.tilePosition.X == 81 && foundPerch.tilePosition.Y == 40)
                {
                    return "tile_name-mail_box";
                }
            }
            string toSpeak = Translator.Instance.Translate("building-parrot_perch-required_nuts", new { item_count = foundPerch.requiredNuts.Value });

            // Return appropriate string based on the current state of the parrot perch
            return foundPerch.currentState.Value switch
            {
                StardewValley.BellsAndWhistles.ParrotUpgradePerch.UpgradeState.Idle => foundPerch.IsAvailable() ? toSpeak : "building-parrot_perch-upgrade_state_idle",
                StardewValley.BellsAndWhistles.ParrotUpgradePerch.UpgradeState.StartBuilding => "building-parrot_perch-upgrade_state_start_building",
                StardewValley.BellsAndWhistles.ParrotUpgradePerch.UpgradeState.Building => "building-parrot_perch-upgrade_state_building",
                StardewValley.BellsAndWhistles.ParrotUpgradePerch.UpgradeState.Complete => "building-parrot_perch-upgrade_state_complete",
                _ => toSpeak,
            };
        }

        // If no parrot perch was found, return null
        return null;
    }

    /// <summary>
    /// Retrieves information about interactables, NPCs, or other features at a given coordinate in an IslandLocation.
    /// </summary>
    /// <param name="islandLocation">The IslandLocation to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? name, CATEGORY? category) GetIslandLocationInfo(IslandLocation islandLocation, int x, int y, string? tileAction, bool lessInfo = false)
    {
        var nutTracker = Game1.player.team.collectedNutTracker;
        string? parrot = GetParrotPerchAtTile(islandLocation, x, y);
        if (islandLocation.IsBuriedNutLocation(new Point(x, y)) && !nutTracker.Contains($"Buried_{islandLocation.Name}_{x}_{y}"))
        {
            return ("tile_name-diggable_spot", CATEGORY.Interactables);
        }
        else if (islandLocation.locationGemBird.Value is IslandGemBird bird && ((int)bird.position.X / Game1.tileSize) == x && ((int)bird.position.Y / Game1.tileSize) == y)
        {
            return (GetGemBirdName(bird), CATEGORY.NPCs);
        }
        else if (parrot != null)
        {
            if (islandLocation is IslandWest islandWest && islandWest.farmhouseMailbox.Value && parrot == "tile_name-mail_box")
            {
                var mailbox = Game1.player.mailbox;
                string content = Translator.Instance.Translate(parrot);
                if (mailbox != null && mailbox.Count > 0)
                {
                    return (Translator.Instance.Translate("tile_name-mail_box-unread_mail_count-prefix", new { mail_count = mailbox.Count, content }), CATEGORY.Ready);
                }
                return (content, CATEGORY.Interactables);
            }
            else
            {
                return (parrot, CATEGORY.Buildings);
            }
        }

        return islandLocation switch
        {
            IslandHut islandHut => GetIslandHutInfo(islandHut, x, y, tileAction, lessInfo),
            IslandNorth islandNorth => GetIslandNorthInfo(islandNorth, x, y, tileAction, lessInfo),
            IslandWest islandWest => GetIslandWestInfo(islandWest, x, y, tileAction, lessInfo),
            VolcanoDungeon dungeon => GetVolcanoDungeonInfo(dungeon, x, y, tileAction, lessInfo),
            _ => GetNamedIslandLocationInfo(islandLocation, x, y, tileAction, lessInfo)
        };
    }

    /// <summary>
    /// Retrieves the value of the "Action" property from the Buildings layer tile at the given coordinates.
    /// </summary>
    /// <param name="libraryMuseum">The LibraryMuseum containing the tile.</param>
    /// <param name="x">The x-coordinate of the tile.</param>
    /// <param name="y">The y-coordinate of the tile.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>The value of the "Action" property as a string, or null if the property is not found.</returns>
    private static string? GetTileActionPropertyValue(LibraryMuseum libraryMuseum, int x, int y, bool lessInfo = false)
    {
        xTile.Tiles.Tile tile = libraryMuseum.map.GetLayer("Buildings").PickTile(new xTile.Dimensions.Location(x * 64, y * 64), Game1.viewport.Size);
        return tile.Properties.TryGetValue("Action", out xTile.ObjectModel.PropertyValue? value) ? value.ToString() : null;
    }

    /// <summary>
    /// Retrieves information about interactables, NPCs, or other features at a given coordinate in a LibraryMuseum.
    /// </summary>
    /// <param name="libraryMuseum">The LibraryMuseum to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? name, CATEGORY? category) GetLibraryMuseumInfo(LibraryMuseum libraryMuseum, int x, int y, string? tileAction, bool lessInfo = false)
    {
        if (libraryMuseum.museumPieces.TryGetValue(new Vector2(x, y), out string museumPiece))
        {
            string displayName = TokenParser.ParseText(Game1.objectData[museumPiece].DisplayName);
            return (Translator.Instance.Translate("tile-museum_piece_showcase-suffix", new { content = displayName }), CATEGORY.Interactables);
        }

        int booksFound = Game1.netWorldState.Value.LostBooksFound;
        string? action = libraryMuseum.doesTileHaveProperty(x, y, "Action", "Buildings");
        if (action != null && action.Contains("Notes"))
        {
            string? actionPropertyValue = GetTileActionPropertyValue(libraryMuseum, x, y, lessInfo);

            if (actionPropertyValue != null)
            {
                int which = Convert.ToInt32(actionPropertyValue.Split(' ')[1]);
                if (booksFound >= which)
                {
                    string message = Game1.content.LoadString("Strings\\Notes:" + which);
                    return (Translator.Instance.Translate("item-suffix-book", new { content = message.Split('\n')[0] }), CATEGORY.Interactables);
                }
                return ("item-lost_book-name", CATEGORY.Other);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Retrieves information about interactables or other features at a given coordinate in a Town.
    /// </summary>
    /// <param name="town">The Town to search.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? name, CATEGORY? category) GetTownInfo(Town town, int x, int y, string? tileAction, bool lessInfo = false)
    {
        if (SpecialOrder.IsSpecialOrdersBoardUnlocked() && x == 62 && y == 93)
        {
            return ("tile_name-special_quest_board", CATEGORY.Interactables);
        }

        if (Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater") && x == 98 && y == 51)
        {
            return ("tile_name-movie_ticket_machine", CATEGORY.Interactables);
        }

        if (Game1.CurrentEvent is not null && Game1.CurrentEvent.isFestival && x == 0 && y == 54)
        {
            return ("tile-town_festival_exit-name", CATEGORY.Doors);
        }

        if (Utility.getDaysOfBooksellerThisSeason().Contains(Game1.dayOfMonth) && x is 109 or 110 && y is 26)
        {
            return ("tile-town-bookseller", CATEGORY.Interactables);
        }

        if (x is 28 or 29 or 30 && y is 14 && Game1.player.hasQuest("31"))
        {
            return ("tile-town-krobus_hiding_bush", CATEGORY.Quest);
        }

        return (null, null);
    }

    /// <summary>
    /// Retrieves information about interactables or other features at a given coordinate in Railroad.
    /// </summary>
    /// <param name="town">Railroad instance for reference.</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? translationKeyOrName, CATEGORY? category) GetRailroadInfo(Railroad railroad, int x, int y, string? tileAction, bool lessInfo = false)
    {
        if (!railroad.witchStatueGone.Get() && !Game1.MasterPlayer.mailReceived.Contains("witchStatueGone") &&
            x == 54 && y == 35)
        {
            return ("tile-railroad-witch_statue-name", CATEGORY.Interactables);
        }

        return (null, null);
    }

    /// <summary>
    /// Retrieves information about interactables or other features at a given coordinate in the MineShaft.
    /// </summary>
    /// <param name="mineShaft">MineShaft instance for reference</param>
    /// <param name="x">The x-coordinate to search.</param>
    /// <param name="y">The y-coordinate to search.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the object found, or (null, null) if no relevant object is found.</returns>
    private static (string? translationKeyOrName, CATEGORY? category) GetMineShaftInfo(MineShaft mineShaft, int x, int y, string? tileAction, bool lessInfo = false)
    {
        if (mineShaft.getTileIndexAt(new Point(x, y), "Buildings") is 194 or 195 or 224)
        {
            return (mineShaft.getMineArea() is MineShaft.frostArea
                ? "tile-mine_shaft-coal_bag"
                : Translator.Instance.Translate("static_tile-common-minecart", TranslationCategory.StaticTiles), CATEGORY.Interactables);
        }

        if (mineShaft.doesTileHaveProperty(x, y, "Type", "Back") is "Dirt")
        {
            if (mineShaft.doesTileHaveProperty(x, y, "Diggable", "Back") != null)
            {
                bool hasAlreadyDug = mineShaft.terrainFeatures.FieldDict.TryGetValue(new Vector2(x, y), out var tf) && tf.Get() is HoeDirt { crop: null };
                return hasAlreadyDug ? (null, null) : ("tile-mine_shaft-dirt", CATEGORY.Flooring);
            }

            if (mineShaft.getTileIndexAt(new Point(x, y), "Back") is 0)
            {
                return ("tile-mine_shaft-duggy_hole", CATEGORY.Decor);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Gets the feeding bench information for barns and coops.
    /// </summary>
    /// <param name="currentLocation">The current GameLocation instance.</param>
    /// <param name="x">The x coordinate of the tile.</param>
    /// <param name="y">The y coordinate of the tile.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple of (string? name, CATEGORY? category) for the feeding bench, or null if not applicable.</returns>
    private static (string? name, CATEGORY? category)? GetFeedingBenchInfo(GameLocation currentLocation, int x, int y, bool lessInfo = false)
    {
        string locationName = currentLocation.Name;

        if (FeedingBenchBounds.TryGetValue(locationName, out var bounds) && x >= bounds.minX && x <= bounds.maxX && y == bounds.y)
        {
            (string? name, CATEGORY category) = TileInfo.GetObjectAtTile(currentLocation, x, y, true);
            bool hasHay = name != null && name.Contains("hay", StringComparison.OrdinalIgnoreCase);
            category = hasHay ? CATEGORY.Other : CATEGORY.Pending;
            return (Translator.Instance.Translate("tile_name-feeding_bench", new { is_empty = (hasHay ? 0 : 1) }), category);
        }

        return null;
    }

    /// <summary>
    /// Gets information about the current location by its name.
    /// </summary>
    /// <param name="currentLocation">The current GameLocation instance.</param>
    /// <param name="x">The x coordinate of the tile.</param>
    /// <param name="y">The y coordinate of the tile.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">Optional. If true, returns information only if the tile coordinates match the resource clump's origin. Default is false.</param>
    /// <returns>A tuple of (string? name, CATEGORY? category) for the object in the location, or null if not applicable.</returns>
    private static (string? name, CATEGORY? category) GetLocationByNameInfo(GameLocation currentLocation, int x, int y, string? tileAction, bool lessInfo = false)
    {
        object locationType = currentLocation is not null and GameLocation ? currentLocation.Name ?? "Undefined GameLocation" : currentLocation!.GetType();
        string locationName = currentLocation.Name ?? "";

        if (locationName.Contains("coop", StringComparison.OrdinalIgnoreCase) || locationName.Contains("barn", StringComparison.OrdinalIgnoreCase))
        {
            var feedingBenchInfo = GetFeedingBenchInfo(currentLocation, x, y);
            if (feedingBenchInfo.HasValue)
            {
                return feedingBenchInfo.Value;
            } // else if something other than feeding benches in barns and coops...
        } //else if something other than barns and coops...

        if (locationName.Contains("witchhut", StringComparison.OrdinalIgnoreCase) && x == 4 && y == 11 && !Game1.player.mailReceived.Contains("hasPickedUpMagicInk"))
        {
            return ("item_name-magic_ink", CATEGORY.Interactables);
        }

        if (locationName.ToLower().Contains("masterycave"))
        {
            if (x == 10 && y == 9 && !MasteryTrackerMenu.hasCompletedAllMasteryPlaques())
            {
                return ("item-mastery_cave-grandpa_letter", CATEGORY.Interactables);
            }
            else if (x == 4 && y == 6)
            {
                return (Translator.Instance.Translate("dynamic_tile-mastery_cave-pedestal", new { has_hat = MasteryTrackerMenu.hasCompletedAllMasteryPlaques() ? 1 : 0 }), CATEGORY.Decor);
            }
        }

        // Unimplemented locations are logged.
        // Check if the location has already been logged
        if (!loggedLocations.Contains(locationType))
        {
            // Log the message
            Log.Debug($"Called GetLocationByNameInfo with unimplemented GameLocation of type {currentLocation.GetType()} and name {currentLocation.Name}", true);

            // Add the location to the HashSet to prevent logging it again
            loggedLocations.Add(locationType);
        }

        return (null, null);
    }

    /// <summary>
    /// Retrieves information for the Egg Festival event tile interaction.
    /// Throws an exception if the current location isn't actively running the Egg Festival.
    /// </summary>
    /// <param name="currentLocation">
    /// The <see cref="GameLocation"/> where the tile interaction is taking place. Must have an active
    /// event with the ID "festival_spring13".
    /// </param>
    /// <param name="x">The tile's X-coordinate.</param>
    /// <param name="y">The tile's Y-coordinate.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">
    /// Determines whether minimal info should be returned about the tile interaction. Currently not used.
    /// </param>
    /// <returns>
    /// A tuple containing the translation key and a <see cref="CATEGORY"/> if an egg is detected on the tile,
    /// or <c>(null, null)</c> if none is found.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="currentLocation"/> doesn't have an active event, or if it's
    /// not the Egg Festival (event ID <c>"festival_spring13"</c>).
    /// </exception>
    private static (string? translationKeyOrName, CATEGORY? category) GetEggFestivalInfo(GameLocation currentLocation,  int x,  int y,  string? tileAction, bool lessInfo)
    {
        // Ensure we're truly in the Egg Festival. Otherwise, bail in a fiery rage.
        if (currentLocation.currentEvent is null || currentLocation.currentEvent.id != "festival_spring13")
            throw new InvalidOperationException("GetEggFestivalInfo requires an active event");
        var currentEvent = currentLocation.currentEvent!;
        // Check if the event actually has festival props. If none, we eggsit
        if (currentEvent.festivalProps.Count > 0)
        {
            if (eggs == null)
            {
                Log.Trace("Copying eggs list");
                // Make a local copy of the eggs list while it's full
                eggs = new(currentEvent.festivalProps);
                // Potentiallly extend the timer based on config value
                currentEvent.festivalTimer = (int)(52000f * MainClass.Config.EggHuntTimerMultiplier);
            }
            // Create a rectangle corresponding to the tile in question
            Microsoft.Xna.Framework.Rectangle tile = new(x * 64, y * 64, 64, 64);
            for (int i = currentEvent.festivalProps.Count - 1; i >= 0; i--)
            {
                // If the festival prop at index i collides with our tile, we found an egg
                if (currentEvent.festivalProps[i].isColliding(tile))
                {
                    var currentEgg = currentEvent.festivalProps[i];
                    return (Translator.Instance.Translate("dynamic_tile-egg_festival-egg", new { number = eggs.IndexOf(currentEgg)+1 }, TranslationCategory.DynamicTiles), CATEGORY.Forageables);
                }
            }
        }
        else
        {
            // No props? Means the event might be ending or not started correctly. Eggsterminate!
            eggs = null;
        }
        return (null, null);
    }

    /// <summary>
    /// Retrieves information for the Stardew Valley Fair event tile interaction.
    /// Throws an exception if the current location isn't actively running the Fair.
    /// </summary>
    /// <param name="currentLocation">
    /// The <see cref="GameLocation"/> where the tile interaction is taking place. Must have an active
    /// event with the ID "festival_fall16".
    /// </param>
    /// <param name="x">The tile's X-coordinate.</param>
    /// <param name="y">The tile's Y-coordinate.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">
    /// Determines whether minimal info should be returned about the tile interaction. Currently not used.
    /// </param>
    /// <returns>
    /// A tuple containing the translation key and a <see cref="CATEGORY"/> if anything  is detected on the tile,
    /// or <c>(null, null)</c> if nothing is found.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="currentLocation"/> doesn't have an active event, or if it's
    /// not the Fair (event ID <c>"festival_fall16"</c>).
    /// </exception>
    private static (string? translationKeyOrName, CATEGORY? category) GetStardewValleyFairInfo(GameLocation currentLocation,  int x,  int y,  string? tileAction, bool lessInfo)
    {
        // Ensure we're truly in the Stardew Valley Fair. Otherwise, bail in a fiery rage.
        if (currentLocation.currentEvent is null || currentLocation.currentEvent.id != "festival_fall16")
            throw new InvalidOperationException("GetStardewValleyFairInfo requires an active event");
        if (tileAction != null)
        {
            // Borrowed logic from SDV's event.cs
            string[] args = ArgUtility.SplitBySpace(tileAction);
            string arg = ArgUtility.Get(args, 0);
            if (arg == "Message")
            {
                // Message args are E.G. "town-fair.1"  -- including the quotes. So ^2 is the digit before closing quote
                switch (tileAction[^2])
                {
                    case '2':
                        return ("dynamic_tile-stardew_valley_fair-strength_game_sign", CATEGORY.Decor);
                    case '1':
                    case '4':
                    case '5':
                    case '6':
                        // 4 tourists, but 3 of them have ids which are offset.
                        int number = Convert.ToInt32(char.GetNumericValue(tileAction[^2]));
                        if (number > 1)
                            number -= 2;
                    object token = new{ number };
                    return (Translator.Instance.Translate("dynamic_tile-stardew_valley_fair-tourist", token, TranslationCategory.DynamicTiles), CATEGORY.NPCs);
                }
            }
        }
        // More logic borrowed and adapted from Stardew Valley's event.cs
        return currentLocation.getTileIndexAt(x, y, "Buildings", "untitled tile sheet") switch
        {
            175 or 176 => ("dynamic_tile-stardew_valley_fair-free_burgers", CATEGORY.Interactables),
            308 or 309 => ("dynamic_tile-stardew_valley_fair-the_wheel", CATEGORY.Interactables),
            87 or 88 => ("dynamic_tile-stardew_valley_fair-purchase_star_tokens", CATEGORY.Interactables),
            501 or 502 => ("dynamic_tile-stardew_valley_fair-slingshot_game", CATEGORY.Interactables),
            510 or 511 => ("dynamic_tile-stardew_valley_fair-prize_booth", CATEGORY.Interactables),
            349 or 350 or 351 => ("dynamic_tile-stardew_valley_fair-grange_display", CATEGORY.Interactables),
            503 or 504 => ("dynamic_tile-stardew_valley_fair-fishing_challenge", CATEGORY.Interactables),
            539 => ("dynamic_tile-stardew_valley_fair-strength_game_arrow", CATEGORY.Interactables),
            540 => ("dynamic_tile-stardew_valley_fair-strength_game", CATEGORY.Interactables),
            505 or 506 => ("dynamic_tile-stardew_valley_fair-fortune_teller", CATEGORY.Interactables),
            _ => (null, null),
        };
    }

    /// <summary>
    /// Retrieves information for the Festival Of Ice event tile interaction.
    /// Throws an exception if the current location isn't actively running the Festival Of Ice.
    /// </summary>
    /// <param name="currentLocation">
    /// The <see cref="GameLocation"/> where the tile interaction is taking place. Must have an active
    /// event with the ID "festival_winter8".
    /// </param>
    /// <param name="x">The tile's X-coordinate.</param>
    /// <param name="y">The tile's Y-coordinate.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">
    /// Determines whether minimal info should be returned about the tile interaction. Currently not used.
    /// </param>
    /// <returns>
    /// A tuple containing the translation key and a <see cref="CATEGORY"/> if anything  is detected on the tile,
    /// or <c>(null, null)</c> if nothing is found.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="currentLocation"/> doesn't have an active event, or if it's
    /// not the Festival Of Ice (event ID <c>"festival_winter8"</c>).
    /// </exception>
    private static (string? translationKeyOrName, CATEGORY? category) GetFestivalOfIceInfo(GameLocation currentLocation,  int x,  int y,  string? tileAction, bool lessInfo)
    {
        // Ensure we're truly in the Festival Of Ice. Otherwise, bail in an icy rage.
        if (currentLocation.currentEvent is null || currentLocation.currentEvent.id != "festival_winter8")
            throw new InvalidOperationException("GetFestivalOfIceInfo requires an active event");
        if (currentLocation.doesTileHaveProperty(x - 3, y, "Action", "Buildings") == "Shop Festival_FestivalOfIce_TravelingMerchant")
            // Pig is x+3 offset from the cart. Cart is found via tile actions.
        return ("tile_name-traveling_cart_pig", CATEGORY.NPCs);
        return (null, null);
    }

    /// <summary>
    /// Retrieves information for unknown event tile interaction.
    /// Throws an exception if the current location isn't actively running any event.
    /// </summary>
    /// <param name="currentLocation">
    /// The <see cref="GameLocation"/> where the tile interaction is taking place. Must have an active
    /// event.
    /// </param>
    /// <param name="x">The tile's X-coordinate.</param>
    /// <param name="y">The tile's Y-coordinate.</param>
    /// <param name="tileAction">The tile action string associated with the tile, if any.</param>
    /// <param name="lessInfo">
    /// Determines whether minimal info should be returned about the tile interaction. Currently not used.
    /// </param>
    /// <returns>
    /// A tuple containing the translation key and a <see cref="CATEGORY"/> if anything  is detected on the tile,
    /// or <c>(null, null)</c> if nothing is found.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="currentLocation"/> doesn't have an active event</c>).
    /// </exception>
    private static (string? translationKeyOrName, CATEGORY? category) GetUnknownEventInfo(GameLocation currentLocation,  int x,  int y,  string? tileAction, bool lessInfo)
    {
        // If there's somehow no event, bail early
        if (currentLocation.currentEvent is null)
            throw new InvalidOperationException("GetUnknownEventInfo requires an active event");

        // Null-forgiveness since we just checked
        var currentEvent = currentLocation.currentEvent!;

        // Figure out what flavor of "unknown" event this is:
        string eventTypeDescription = "Unknown event";
        if (currentEvent.isFestival)
            eventTypeDescription += $", festival \"{currentEvent.FestivalName}\"";
        else if (currentEvent.isWedding)
            eventTypeDescription += ", wedding";
        else if (currentEvent.isMemory)
            eventTypeDescription += ", memory";

        Log.Info($"{eventTypeDescription} [ID: {currentEvent.id}]", true);

        // Gather up some actor info - the npcs in the event
        var actorList = currentEvent.actors;
        string actorNames = actorList?.Count > 0
            ? string.Join(", ", actorList.Select(npc => npc.displayName ?? npc.Name))
            : "None";

        Log.Debug($@"
            Actors: {actorNames}
            Props Count: {currentEvent.props?.Count ?? 0}
            Festival Props Count: {currentEvent.festivalProps?.Count ?? 0}
            int_useMeForAnything: {currentEvent.int_useMeForAnything}
            int_useMeForAnything2: {currentEvent.int_useMeForAnything2}
            float_useMeForAnything: {currentEvent.float_useMeForAnything}
            specialEventVariable1: {currentEvent.specialEventVariable1}
            specialEventVariable2: {currentEvent.specialEventVariable2}
        ", true);


        return (null, null);
    }

    /// <summary>
    /// Retrieves additional info for generic tile actions. May be extended for use across various locations and events.
    /// </summary>
    /// <param name="currentLocation">
    /// The game location where this tile action is being triggered.
    /// </param>
    /// <param name="x">The X-coordinate of the tile.</param>
    /// <param name="y">The Y-coordinate of the tile.</param>
    /// <param name="tileAction">
    /// The tile action string associated with the coordinates.
    /// </param>
    /// <param name="lessInfo">Flag indicating whether minimal info should be returned. (Not used yet.)</param>
    /// <returns>
    /// A tuple containing a translated token/key and a <see cref="CATEGORY"/> if the action was recognized;
    /// otherwise, <c>(null, null)</c>.
    /// </returns>
    private static (string? translationKeyOrName, CATEGORY? category) GetTileActionInfo(GameLocation currentLocation, int x, int y, string? tileAction, bool lessInfo)
    {
        // Check if there's a tileAction and an active event. 
        // Right now, we only handle festival shops in this generic handler.
        if (tileAction != null && currentLocation.currentEvent != null)
        {
            // Copied and adapted from StardewValley's event.cs
            // ArgUtility.SplitBySpace -> Splits the tileAction by whitespace 
            // to form an argument list. We only care about the first arg, e.g. "OpenShop".
            string[] args = ArgUtility.SplitBySpace(tileAction);
            string arg = ArgUtility.Get(args, 0);
            if (arg == "OpenShop" || arg == "Shop")
            {
                return currentLocation.currentEvent.id switch
                {
                    "festival_spring13" or "festival_spring24" or "festival_summer11" or "festival_summer28" or "festival_fall27" or "festival_winter25" => ("dynamic_tile-festival-pierres_booth", CATEGORY.Interactables),
                    "festival_winter8" => ("tile_name-traveling_cart", CATEGORY.Interactables),
                    // "festival_fall16" handled by location in GetStardewValleyFairInfo
                    _ => (null, null),
                };
            }
        }
        return (null, null);
    } 

    /// <summary>
    /// Attempts to retrieve a default tile action's translated name and category,
    /// using a fluent-style dynamic token and optional debug settings.
    /// </summary>
    /// <param name="currentLocation">
    /// The current <see cref="GameLocation"/> context for generating the token.
    /// </param>
    /// <param name="x">The tile's X-coordinate within the location.</param>
    /// <param name="y">The tile's Y-coordinate within the location.</param>
    /// <param name="tileAction">
    /// A string describing the tile action; if not null, this is used to form a token.
    /// </param>
    /// <param name="lessInfo">
    /// A flag indicating minimal detail return. Not currently used here, but available for expansion.
    /// </param>
    /// <returns>
    /// A tuple where <c>translationKeyOrName</c> may contain the translated token
    /// (or a debug fallback if <c>ReadTileDebug</c> is enabled), and <c>category</c> is derived
    /// from the <c>DynamicTileCategories</c> dictionary if available, or defaults to <c>CATEGORY.Other</c>.
    /// </returns>
    private static (string? translationKeyOrName, CATEGORY? category) GetDefaultTileActionInfo(GameLocation currentLocation, int x, int y, string? tileAction, bool lessInfo)
    {
        if (tileAction == null) return (null, null);
        // Default behavior; translate it if it exists, grab token from json or default to Other.
        // If ReadTileDebug is enabled, return raw keys missing translations.
        string dynamicTileToken = GetDynamicTileFluentToken(currentLocation, tileAction);
        (string? translationKeyOrName, CATEGORY? category) toReturn = (null, null);
        if (Translator.Instance.IsAvailable(dynamicTileToken, TranslationCategory.DynamicTiles) || MainClass.Config.ReadTileDebug)
        {
            // The key does exist or the user has config enabled to show undefined keys
            toReturn.translationKeyOrName = Translator.Instance.Translate(dynamicTileToken, TranslationCategory.DynamicTiles);
            if (DynamicTileCategories.TryGetValue(dynamicTileToken, out string? dynamicCategory))
                toReturn.category = CATEGORY.FromString(dynamicCategory);
            else
                toReturn.category = CATEGORY.Other;
        }
        return toReturn;
    }

    /// <summary>
    /// Retrieves the dynamic tile information for the given coordinates in the specified location.
    /// </summary>
    /// <param name="currentLocation">The current GameLocation instance.</param>
    /// <param name="x">The x-coordinate of the tile.</param>
    /// <param name="y">The y-coordinate of the tile.</param>
    /// <param name="lessInfo">An optional boolean to return less detailed information. Defaults to false.</param>
    /// <returns>A tuple containing the name and CATEGORY of the dynamic tile, or null values if not found.</returns>
    public static (string? name, CATEGORY? category) GetDynamicTileAt(GameLocation currentLocation, int x, int y, bool lessInfo = false)
    {
        (string? translationKeyOrName, CATEGORY? category) = GetDynamicTileWithTranslationKeyOrNameAt(currentLocation, x, y, lessInfo);

        if (translationKeyOrName == null)
            return (null, null);

        translationKeyOrName = Translator.Instance.Translate(translationKeyOrName, TranslationCategory.DynamicTiles, disableWarning: true);

        return (translationKeyOrName, category);
    }

    /// <summary>
    /// Retrieves a dynamic tile's display name (or translation key) and its category, 
    /// taking into account panning spots, tile actions, location-specific logic, and active festival events.
    /// </summary>
    /// <param name="currentLocation">
    /// The current <see cref="GameLocation"/> context where the tile resides.
    /// </param>
    /// <param name="x">The tile's X-coordinate.</param>
    /// <param name="y">The tile's Y-coordinate.</param>
    /// <param name="lessInfo">
    /// A flag indicating whether to return minimal information about the tile.
    /// </param>
    /// <returns>
    /// A tuple containing an optional string and category describing the tile. If no 
    /// recognized dynamic tile is found, returns <c>(null, null)</c>.
    /// </returns>
    internal static (string? translationKeyOrName, CATEGORY? category) GetDynamicTileWithTranslationKeyOrNameAt(GameLocation currentLocation, int x, int y, bool lessInfo = false)
    {
        // 1. Check for panning spots before anything else.
        // (Possibly skip for events or indoor locations?)
        if (currentLocation.orePanPoint.Value != Point.Zero && currentLocation.orePanPoint.Value == new Point(x, y))
        {
            return ("tile_name-panning_spot", CATEGORY.Interactables);
        }

        // Return value tuple
        (string? translationKeyOrName, CATEGORY? category) toReturn = (null, null);

        // 2. Check for tile actions (the "Action" property in Buildings layer).
        //    If we find one, we run our generic tile action handler first.
        string tileAction = currentLocation.doesTileHaveProperty(x, y, "Action", "Buildings");

        if (tileAction != null)
        {
            // Generic action handler
            toReturn = GetTileActionInfo(currentLocation, x, y, tileAction, lessInfo);

            if (toReturn.translationKeyOrName != null)
            {
                // If the tile action was recognized and given a translation, we can bail out early.
                return toReturn;
            }
        }

        // 3. No early tile action match, so we fall back to location- or event-specific logic.
        toReturn = currentLocation switch
        {
            // No event; handle real location
            { currentEvent: null } => currentLocation switch
            {
                Beach beach => GetBeachInfo(beach, x, y, tileAction, lessInfo),
                BoatTunnel boatTunnel => GetBoatTunnelInfo(boatTunnel, x, y, tileAction, lessInfo),
                CommunityCenter communityCenter => GetCommunityCenterInfo(communityCenter, x, y, lessInfo),
                Farm farm => GetFarmInfo(farm, x, y, tileAction, lessInfo),
                FarmHouse farmHouse => GetFarmHouseInfo(farmHouse, x, y, tileAction, lessInfo),
                Forest forest => GetForestInfo(forest, x, y, tileAction, lessInfo),
                IslandFarmHouse islandFarmHouse => GetIslandFarmHouseInfo(islandFarmHouse, x, y, tileAction, lessInfo),
                IslandLocation islandLocation => GetIslandLocationInfo(islandLocation, x, y, tileAction, lessInfo),
                LibraryMuseum libraryMuseum => GetLibraryMuseumInfo(libraryMuseum, x, y, tileAction, lessInfo),
                Town town => GetTownInfo(town, x, y, tileAction, lessInfo),
                Railroad railroad => GetRailroadInfo(railroad, x, y, tileAction, lessInfo),
                MineShaft mineShaft => GetMineShaftInfo(mineShaft, x, y, tileAction, lessInfo),
                _ => GetLocationByNameInfo(currentLocation, x, y, tileAction, lessInfo)
            },
            // Running events
            _ => currentLocation.currentEvent.id switch
            {
                "festival_spring13" => GetEggFestivalInfo(currentLocation, x, y, tileAction, lessInfo),
                "festival_fall16" => GetStardewValleyFairInfo(currentLocation, x, y, tileAction, lessInfo),
                "festival_winter8" => GetFestivalOfIceInfo(currentLocation, x, y, tileAction, lessInfo),
                _ => GetUnknownEventInfo(currentLocation, x, y, tileAction, lessInfo)
            }
        };

        // 4. If the tile action is set but was never handled (translationKeyOrName is null),
        //    we try one last fallback: the "default" tile action info (which might handle 
        //    it or log it if it remains unrecognized).
        if (toReturn.translationKeyOrName == null && tileAction != null)
        {
            // Tile action is set but no handler has handled
            // Run default handler as last resort
            toReturn = GetDefaultTileActionInfo(currentLocation, x, y, tileAction, lessInfo);
            #if DEBUG
            if (toReturn.translationKeyOrName == null)
                Log.Verbose($"Unhandled tile action \"{tileAction}\" at ({x}, {y}) of \"{currentLocation.currentEvent?.id ?? currentLocation.NameOrUniqueName}\".", true); 
            #endif
        }

        // Return the final result, which might be recognized from location/event checks,
        // or from the default tile action fallback, or null if truly unhandled.
        return toReturn;
    }
}