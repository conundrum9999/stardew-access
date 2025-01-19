using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using System.Diagnostics;

namespace stardew_access.Features;
using System.Timers;

using Utils;
using Translation;
using static Utils.MiscUtils;
using static Utils.InputUtils;
using static Utils.MovementHelpers;
using static Utils.NPCUtils;

public enum ReadMode
{
    NamePositionDirectionAndDistance = 0,
    NameAndPosition = 1,
    Name = 2,
    Category = 3
}

internal class ObjectTracker : FeatureBase
{
    // For debugging / testing
    #if DEBUG
    readonly Stopwatch stopwatch = new();
    #endif

    // The current  Pathfinder instance
    private  Pathfinder? pathfinder;

    // Determines whether we sort by proximity or not
    private  bool SortByProximity;

    // Store of tracked things
    // will be replaced upon refresh.
    private  readonly Dictionary<string, List<(Vector2 position, string name)>> TrackedObjects = [];

    // Backing field for the currently selected category in the UI.
    private string _selectedCategory = "";

    // Backing field for the currently selected category's index in the list.
    private int _selectedCategoryIndex = -1;

    // Creates sorted array of categories, ignoring those with no objcts
    private string[] GetNonEmptyCategoriesSorted()
    {
        // Allocate maximum potential size (all keys)
        string[] tempKeys = new string[TrackedObjects.Count];
        int count = 0;

        // Use a for loop over the dictionary's enumerator
        foreach (var kvp in TrackedObjects)
        {
            if (kvp.Value.Count > 0) // Check for non-empty list
            {
                tempKeys[count++] = kvp.Key; // Add to array and increment count
            }
        }

        // Resize the array to fit the actual number of keys
        var result = new string[count];
        System.Array.Copy(tempKeys, result, count);

        // Sort the array in-place
        System.Array.Sort(result, StringComparer.Ordinal);

        return result;
    }

    // Represents the currently selected category in the UI.
    internal string? SelectedCategory
    {
        // If _selectedCategory isn't empty and is a key in TrackedObjects, we consider it valid;
        // otherwise, we return null.
        get => !string.IsNullOrEmpty(_selectedCategory) && TrackedObjects.ContainsKey(_selectedCategory)
            ? _selectedCategory
            : null;
        set
        {
            // 1. If the incoming value is null or empty, we clear everything out.
            if (string.IsNullOrEmpty(value))
            {
                // Reset everything
                _selectedCategory = "";
                _selectedCategoryIndex = -1;
                _selectedObject = (Vector2.Zero, "");
                _selectedObjectIndex = -1;
                return;
            }

            // 2. If the value isn't actually changing, do nothing.
            if (_selectedCategory == value)
                return;

            try
            {
                // 3. If the new category isn't in TrackedObjects, it’s invalid
                //    => Throw an error -- this shouldn't happen under normal circumstances.
                if (!TrackedObjects.ContainsKey(value) || TrackedObjects[value].Count == 0)
                {
                    // Because we haven't assigned anything yet,
                    // our underlying values remain unchanged.
                    throw new InvalidOperationException(
                        $"Attempted to set invalid category '{value}'. This should not occur if GetLocationObjects was handled properly."
                    );
                }

                // 4. The category is valid; we can safely set it.
                _selectedCategory = value;

                // Build a temporary array of category keys from the sorted dictionary
                // to figure out the new index of this category.
                // (We assume you're using SortedDictionary, so keys are in alphabetical order).
                var keys = GetNonEmptyCategoriesSorted();
                _selectedCategoryIndex = Array.IndexOf(keys, value);

                // Since we only selected non-empty categories, we
                // always have at least one object, we select index 0 without
                // checking for an empty list.
                var catList = TrackedObjects[_selectedCategory];
                _selectedObjectIndex = 0;
                _selectedObject = catList[0];
            }
            catch (Exception ex)
            {
                #if DEBUG
                Log.Warn($"Error setting SelectedCategory: {ex}", true);
                #else
                Log.Trace($"Error setting SelectedCategory: {ex}", true);
                #endif
            }
        }
    }

    internal int SelectedCategoryIndex
    {
        get
        {
            // Rebuild the key list to ensure it’s current
            var keys = GetNonEmptyCategoriesSorted();
            // If the stored string is valid, we find its index
            if (SelectedCategory == null)
                return -1;
            _selectedCategoryIndex = Array.IndexOf(keys, _selectedCategory);
            return _selectedCategoryIndex;
        }
        set
        {
            var keys = GetNonEmptyCategoriesSorted();
            if (value < 0 || value >= keys.Length)
            {
                // Announce boundary or do nothing
                AnnounceBoundary(value < _selectedCategoryIndex);
                return;
            }

            // This sets the "source of truth" string property, 
            // which also updates the backing index inside that setter.
            SelectedCategory = keys[value];
        }
    }

    // The current backing field for the selected object (tuple).
    private (Vector2 position, string name) _selectedObject = (Vector2.Zero, "");

    // Represents the currently selected object in the UI.
    // This property defers heavily to SelectedObjectIndex as the canonical place
    // for which object in the list is selected. If SelectedObjectIndex is -1, there's no valid object.
    internal (Vector2 position, string name)? SelectedObject
    {
        get
        {
            // Let the index logic do the heavy lifting. If index is -1, we have no valid object.
            int idx = SelectedObjectIndex;
            if (idx == -1)
                return null;

            // Otherwise, we assume _selectedObject is already in sync with that index.
            // (Because SelectedObjectIndex's getter/setter keep them aligned.)
            return _selectedObject;
        }
        set
        {
            // If someone explicitly sets this to null, that means "no selection".
            if (value == null)
            {
                _selectedObject = (Vector2.Zero, "");
                // Also reset the index to -1:
                _selectedObjectIndex = -1;
                return;
            }

            try
            {
                // Check if the current category is valid:
                var catObjects = TrackedObjects[_selectedCategory];

                // Attempt to find this object in the category’s list.
                int foundIndex = catObjects.IndexOf(value.Value);

                if (foundIndex == -1)
                {
                    // The object is not in the list, but the category is still valid.
                    // => set focus to first item.
                    SelectedObjectIndex = 0;
                }
                else
                {
                    // The object is found at a valid index. Update both the index and the object.
                    _selectedObjectIndex = foundIndex;
                    _selectedObject = value.Value;
                }
            }
            catch (KeyNotFoundException ex)
            {
                // The category is invalid => return null
                #if DEBUG
                Log.Error($"Unable to set SelectedObject to \"{value}\" due to {ex}", true);
                #endif
                return;
            }
            catch (Exception ex)
            {
                // Any other unexpected error => log and return.
                #if DEBUG
                Log.Error($"Unable to set SelectedObject to \"{value}\" due to {ex}", true);
                #endif
                return;
            }
        }
    }

    // Private backing field for the "currently selected object" index
    private int _selectedObjectIndex = 0;

    // This property reports (and sets) which index in the current category's list is selected.
    // It uses a "try/fallback" approach:
    // - The getter first tries to see if our stored index still matches our stored _selectedObject.
    // - If that check fails, we fallback to .IndexOf(_selectedObject).
    // - If that still fails (or if the category is invalid), we catch an exception and return -1
    //   (or do whatever "invalid" logic you need).
    // - The setter trusts the incoming value, and if it’s out-of-range or invalid, we handle it in catch blocks
    //   so we can give the user explicit UI feedback about boundaries or invalid categories.
    internal int SelectedObjectIndex
    {
        get
        {
            try
            {
                // Fast-path check:
                // 1. If the object at [category][index] is the same as _selectedObject, we return immediately.
                // 2. Otherwise, we do a fallback .IndexOf(_selectedObject).
                if (TrackedObjects[_selectedCategory][_selectedObjectIndex] == _selectedObject)
                {
                    // Normal case: everything is still valid
                    return _selectedObjectIndex;
                }
                else
                {
                    // Fallback: the object may have shifted in the list
                    int newIndex = TrackedObjects[_selectedCategory].IndexOf(_selectedObject);
                    _selectedObjectIndex = newIndex; 
                    return newIndex;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // The object index is out of range, but the category might still be valid.
                // => Set index to last item.
                _selectedObjectIndex = TrackedObjects[_selectedCategory].Count - 1;
                return _selectedObjectIndex;
            }
            catch (KeyNotFoundException ex)
            {
                // The category key (_selectedCategory) is no longer valid in TrackedObjects => full refresh
                Log.Error($"Unable to retrieve SelectedObjectIndex due to {ex}");
                // Return -1 to indicate no object.
                return -1;
            }
            catch (Exception ex)
            {
                // Fallback for unexpected exceptions.
                Log.Error($"Unable to retrieve SelectedObjectIndex due to {ex}", true);
                return -1;
            }
        }
        set
        {
            // We "trust" the incoming value to be valid.
            // We'll catch errors and do boundary announcements or resets as needed.
            try
            {
                // Attempt to retrieve the object at `value` to verify it's valid and in range.
                var testObj = TrackedObjects[_selectedCategory][value]; 
                // If that succeeds, we can safely update both the index and the selected object.
                _selectedObjectIndex = value;
                _selectedObject = testObj; 
            }
            catch (ArgumentOutOfRangeException)
            {
                // The user tried to go beyond the first or last item in the current list.
                // The old index/object remain in place; we just announce the boundary.
                AnnounceBoundary(value < 0);
            }
            catch (KeyNotFoundException)
            {
                // The category key is no longer valid => return
                return;
            }
        }
    }

    // For boundary announcements:
    private static void AnnounceBoundary(bool isStart)
    {
        MainClass.ScreenReader.TranslateAndSay(
            isStart 
                ? "feature-object_tracker-start_of_list" 
                : "feature-object_tracker-end_of_list",
            true
        );
    }

    // Provides direct access to the position (Vector2?) of the currently selected object in the UI.
    // Returns null if no valid object is selected.
    //internal Vector2? SelectedObjectPosition => _selectedObject.position != Vector2.Zero ? _selectedObject.position : null;

    // Provides direct access to the name (string?) of the currently selected object in the UI.
    // Returns null if no valid object is selected.
    //internal string? SelectedObjectName => _selectedObject.name != "" ? _selectedObject.name : null;

    private Vector2? SelectedCoordinates = null;
    
    
    private Dictionary<string, Dictionary<string, Dictionary<int, (string? name, string? category)>>>
    favorites = new(StringComparer.OrdinalIgnoreCase);
    private const int PressInterval = 500; // Milliseconds
    private readonly Timer lastPressTimer = new(PressInterval);
    private readonly Timer navigationTimer = new(PressInterval);
    private int lastFavoritePressed = 0;
    private int sameFavoritePressed = 0;
    private int navigateToFavorite = -1;
    private int _favoriteStack;
    public int FavoriteStack
    {
        get { return _favoriteStack; }
        set { _favoriteStack = Math.Max(0, value); }
    }
    bool SaveCoordinatesToggle = false;

        // Indicates whether debounce is active
        private bool _debounced = false;
    // Time of last refresh
    private static double LastRefreshTime = 0;

    // Private constant defining the refresh interval (e.g., 500ms)
    private const double RefreshInterval = 1000; // in milliseconds

    // Lock object for thread safety
    private static readonly object LockObj = new();

    /// <summary>
    /// Sets the refresh flag to true, indicating that the data is stale and due for refresh.
    /// </summary>
    public static void InvalidateTrackerData()
    {
        lock (LockObj)
        {
            Instance.ShouldRefresh = true;
            Instance._debounced = true;
        }
    }

    // Indicates that the tracked data is stale and should be refreshed
    private bool _shouldRefresh = false;

    /// <summary>
    /// Gets whether tracked item counts have changed or a character has moved and the refresh interval has elapsed.
    /// </summary>
    private bool ShouldRefresh
    {
        get
        {
            lock (LockObj)
            {
                if (_shouldRefresh && 
                    (!_debounced || (Game1.currentGameTime.TotalGameTime.TotalMilliseconds  - LastRefreshTime) >= RefreshInterval))
                {
                    return true;
                }
                return false;
            }
        }
        set
        {
            lock (LockObj)
            {
                _shouldRefresh = value;
                if (!value)
                {
                    // Update the last refresh time when resetting the flag
                    LastRefreshTime = Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
                }
            }
        }
    }

    private static ObjectTracker? instance;
    public new static ObjectTracker Instance
    {
        get
        {
            instance ??= new ObjectTracker();
            return instance;
        }
    }

    public ObjectTracker()
    {
        SortByProximity = MainClass.Config.OTSortByProximity;
        lastPressTimer.Elapsed += OnLastPressTimerElapsed;
        lastPressTimer.AutoReset = false; // So it only triggers once per start
        navigationTimer.Elapsed += OnNavigationTimerElapsed;
        navigationTimer.AutoReset = false;
        LoadFavorites();
    }

    private void OnLastPressTimerElapsed(object? sender, ElapsedEventArgs? e) => FavoriteKeysReset();

    private void OnNavigationTimerElapsed(object? sender, ElapsedEventArgs? e)
    {
        if (navigateToFavorite > 0)
        {
            SetFromFavorites(navigateToFavorite);
            navigateToFavorite = -1;
            MoveToCurrentlySelectedObject();
        }
    }

    internal override void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        // The event with id 13 is the Haley's six heart event, the one at the beach requiring the player to find the bracelet
        // *** Exiting here will cause GridMovement and ObjectTracker functionality to not work during this event, making the bracelet impossible to track ***
        if (!Context.IsPlayerFree && !(Game1.CurrentEvent is not null && Game1.CurrentEvent.id == "13"))
            return; // so ... why are we exiting here? _^_

        if (Game1.activeClickableMenu != null && pathfinder != null && pathfinder.IsActive)
        {
            #if DEBUG
            Log.Verbose(
                "ObjectTracker->Update: a menu has opened, canceling auto walking.");
            #endif
            pathfinder.StopPathfinding();
            return;
        }

        if (e.IsMultipleOf(15)) // ~250 ms
            Tick();
    }

    internal override bool OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        base.OnButtonPressed(sender, e);
        bool cancelAutoWalkingPressed = MainClass.Config.OTCancelAutoWalking.JustPressed();
        
        if (pathfinder != null && pathfinder.IsActive)
        {
            if (cancelAutoWalkingPressed)
            {
                #if DEBUG
                Log.Verbose("ObjectTracker->HandleKeys: cancel auto walking pressed, canceling auto walking for object tracker.");
                #endif
                pathfinder.StopPathfinding();
                MainClass.ModHelper!.Input.Suppress(e.Button);
                return true;
            }
            else if (IsAnyMovementKeyPressed())
            {
                #if DEBUG
                Log.Verbose("ObjectTracker->HandleKeys: movement key pressed, canceling auto walking for object tracker.");
                #endif
                pathfinder.StopPathfinding();
                MainClass.ModHelper!.Input.Suppress(e.Button);
                return true;
            }
            else if (IsUseToolKeyActive())
            {
                #if DEBUG
                Log.Verbose("ObjectTracker->HandleKeys: use tool button pressed, canceling auto walking for object tracker.");
                #endif
                pathfinder.StopPathfinding();
                MainClass.ModHelper!.Input.Suppress(e.Button);
                Game1.pressUseToolButton();
                return true;
            }
            else if (IsDoActionKeyActive())
            {
                #if DEBUG
                Log.Verbose("ObjectTracker->HandleKeys: action button pressed, canceling auto walking for object tracker.");
                #endif
                pathfinder.StopPathfinding();
                MainClass.ModHelper!.Input.Suppress(e.Button);
                Game1.pressActionButton(Game1.input.GetKeyboardState(), Game1.input.GetMouseState(),
                    Game1.input.GetGamePadState());
                return true;
            }
        }
        return false;
    }

    internal override void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        base.OnButtonsChanged(sender, e);
        
        if (!Context.IsPlayerFree)
            return;
        Instance.HandleKeys(sender, e);
    }

    internal override void OnWarped(object? sender, WarpedEventArgs e)
    {
        // reset the objects being tracked
        GetLocationObjects(resetFocus: true);
        // setting this directly instead of calling InvalidateTrackerData()
        // causes an immediate refresh bypassing debounce.
        ShouldRefresh = true;
        // reset favorites stack to the first stack for new location.
        FavoriteStack = 0;
    }

    internal override void OnBuildingListChanged(object? sender, BuildingListChangedEventArgs e)
    {
        if (e.IsCurrentLocation)
            InvalidateTrackerData();
    }

    internal override void OnDebrisListChanged(object? sender, DebrisListChangedEventArgs e)
    {
        if (e.IsCurrentLocation)
            InvalidateTrackerData();
    }

    internal override void OnFurnitureListChanged(object? sender, FurnitureListChangedEventArgs e)
    {
        if (e.IsCurrentLocation)
            InvalidateTrackerData();
    }

    internal override void OnLargeTerrainFeatureListChanged(object? sender, LargeTerrainFeatureListChangedEventArgs e)
    {
        if (e.IsCurrentLocation)
            InvalidateTrackerData();
    }

    internal override void OnNpcListChanged(object? sender, NpcListChangedEventArgs e)
    {
        if (e.IsCurrentLocation)
            InvalidateTrackerData();
    }

    internal override void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
    {
        if (e.IsCurrentLocation)
            InvalidateTrackerData();
    }

    internal override void OnTerrainFeatureListChanged(object? sender, TerrainFeatureListChangedEventArgs e)
    {
        if (e.IsCurrentLocation)
            InvalidateTrackerData();
    }

    /*private void UpdateSpecialAction()
    {
        // Early exit if no location
        if (Game1.currentLocation == null) return;
        // Handle any special event refresh logic here
        if (Game1.currentLocation!.currentEvent != null)
        {
            switch(Game1.currentLocation!.currentEvent!.id)
            {
                case "festival_spring13":
                    UpdateAndRunIfChanged(ref objectCounts[6], Game1.currentLocation.currentEvent.festivalProps.Count, () => { Log.Debug("Eggs count has changed."); _shouldRefresh = true; });
                    return;
            }
        }
        else
        {
            switch (Game1.currentLocation)
            {
                case  IslandHut islandHut:
                    UpdateAndRunIfChanged(ref objectCounts[6], islandHut.treeHitLocal ? 1 : 0, () => { Log.Debug("Potted tree state has changed."); _shouldRefresh = true; });
                    return;
            }
        }
    }*/

    private void Tick()
    {
        if (!MainClass.Config.OTAutoRefreshing || Game1.currentLocation == null) return;

        // If a change was detected, refresh the objects
        if (ShouldRefresh)
        {
            GetLocationObjects(resetFocus: false);
        }
        else
        {
            _debounced = false;
        }
    }

    private bool RetryPathfinding(int attemptNumber, int maxRetries, Vector2? lastTargetedTile)
    {
        if ((SelectedCoordinates.HasValue || SelectedObject.HasValue) && attemptNumber < maxRetries)
        {
            /* this was broken anyway; will investigate better way to handle. Leaving as reminder.
             * if (TrackedObjects.TryGetValue("characters", out var characters))
            {
                foreach (var kvp in characters)
                {
                    NPC? character = kvp.Value.character;
                    GhostNPC(character, sameTile: true);
                }
            }*/
            return true;
        }
        GetLocationObjects(resetFocus: true);
        return false;
    }

    private void StopPathfinding(Vector2? lastTargetedTile)
    {
        FixCharacterMovement();
        if (lastTargetedTile != null) FacePlayerToTargetTile(lastTargetedTile.Value);
        ReadCurrentlySelectedObject();
        GetLocationObjects(resetFocus: SortByProximity);
        pathfinder?.Dispose();
    }

    /// <summary>
    /// Reads the currently selected object or coordinates based on the specified read mode.
    /// </summary>
    /// <param name="readMode">Determines the level of detail to speak.</param>
    private void ReadCurrentlySelectedObject(ReadMode readMode = ReadMode.NamePositionDirectionAndDistance)
    {
        // Cache properties to prevent changes during execution
        var selectedObject = SelectedObject;
        var selectedCategory = SelectedCategory;
        var selectedCoordinates = SelectedCoordinates;

        switch (readMode)
        {
            case ReadMode.NamePositionDirectionAndDistance:
            case ReadMode.NameAndPosition:
                ReadNameAndPosition();
                break;

            case ReadMode.Name:
            case ReadMode.Category:
                ReadCategoryAndName();
                break;

            default:
                Log.Error($"Unhandled ReadMode: {readMode}");
                break;
        }

        void ReadNameAndPosition()
        {
            string destinationName = "";
            Vector2 destinationTile;
            string translationKey;
            if (selectedCoordinates.HasValue)
            {
                destinationTile = selectedCoordinates.Value;
                translationKey = "feature-object_tracker-read_selected_coordinates";
            }
            else if (selectedObject.HasValue)
            {
                destinationName = selectedObject.Value.name;
                destinationTile = selectedObject.Value.position;
                translationKey = "feature-object_tracker-read_selected_object";
            }
            else
                return;
            Vector2 playerTile = Game1.player.Tile;
            string direction = GetDirection(playerTile, destinationTile);
            string distance = GetDistance(playerTile, destinationTile).ToString();
            object translationTokens = new
            {
                object_name = destinationName,
                coordinates_x = destinationTile.X.ToString(),
                coordinates_y = destinationTile.Y.ToString(),
                only_tile = (int)readMode,
                player_x = (int)playerTile.X,
                player_y = (int)playerTile.Y,
                direction,
                distance
            };
            MainClass.ScreenReader.TranslateAndSay(
                translationKey, 
                translationTokens: translationTokens,
                interrupt: true
            );
        }

        void ReadCategoryAndName()
        {
            string toSpeak;
            if (string.IsNullOrEmpty(selectedCategory))
                toSpeak = "feature-object_tracker-no_categories_found";
            else if (!selectedObject.HasValue)
                toSpeak = "feature-object_tracker-no_objects_found";
            else
                switch (readMode)
                {
                    case ReadMode.Name:
                        toSpeak = selectedObject.Value.name;
                        break;
                    case ReadMode.Category:
                        // This ensures we get the translated category name
                        string category = CATEGORY.FromString(selectedCategory).ToString();
                        // If config is enabled, speak both category and object name, properly punctuated for the language using fluent
                        // otherwise just speak category (which is already translated)
                        toSpeak = MainClass.Config.OTReadSelectedCategoryAndObject
                            ? Translator.Instance.Translate("feature-object_tracker-read_category_and_object", new
                            {
                                category_name = category,
                                object_name = selectedObject.Value.name
                            })
                            : category
                        ;
                        break;
                    default:
                        return;
                }

            MainClass.ScreenReader.Say(toSpeak, interrupt: false);
        }
    }

    /// <summary>
    /// Ensures focus is placed on the first category/object. 
    /// If resetCategory is true, we reset the category to the first one; otherwise we keep the current category.
    /// </summary>
    /// <param name="resetCategory">
    /// If true, forces resetting to the first category. Otherwise, we keep the current category if valid.
    /// </param>
    private void SetFocusToFirstObject(bool resetCategory = false)
    {
        // 1. Check if we have any tracked objects at all.
        if (TrackedObjects == null)
        {
            MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-no_objects_found", interrupt: true);
            return;
        }

        if (TrackedObjects.Count == 0)
        {
            MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-no_categories_found", interrupt: true);
            return;
        }

        // 2. If we must reset the category (or if there's no valid category),
        //    set the category index to 0. This call automatically sets 
        //    SelectedCategory AND the first object in it (object index = 0).
        if (SelectedCategory == null || resetCategory)
        {
            SelectedCategoryIndex = 0;
        }
        else
        {
            // 3. Otherwise, we keep the same category—so we just ensure
            //    the object index is set to 0, in case it isn’t already.
            //    (Because setting SelectedCategoryIndex above would do it
            //    if we changed categories, but here the category stays the same.)
            SelectedObjectIndex = 0;
        }

        // 4. Log or debug the final results.
        #if DEBUG
        string outputCategory = SelectedCategory ?? "No Category";
        string outputObject = SelectedObject?.name ?? "No Object";
        Log.Debug($"Category: {outputCategory} | Object: {outputObject}");
        #endif
    }

    #if DEBUG
    private int refreshCounter = 0;
    private double _average = 0;
    private void AddToAverage(double value)
    {
        refreshCounter++;
        _average += (value - _average) / refreshCounter;
    }
    #endif

    /// <summary>
    /// Updates the tracked objects and refreshes the UI focus as needed.
    /// </summary>
    /// <param name="resetFocus">
    /// Determines whether to reset the entire UI focus (category and object).
    /// If false, focus will be preserved unless the selected category or object becomes invalid.
    /// </param>
    internal void GetLocationObjects(bool resetFocus = true)
    {
        #if DEBUG
        stopwatch.Restart();
        #endif
        // Populate the TrackedObjects dictionary based on the current radar search.
        try
        {
            Radar.SearchLocation(TrackedObjects);
        }
        catch (Exception ex)
        {
            // make sure to clear out stale results
            foreach (var catList in TrackedObjects.Values)
                catList.Clear();
            // Log the error so we can diagnose why the radar call failed
            Log.Error($"Radar search encountered an exception: {ex}", true);
            return;
        }
        #if DEBUG
        stopwatch.Stop(); // Stop timing
        AddToAverage(stopwatch.ElapsedMilliseconds);
        Log.Trace($"OTRefresh executed in {stopwatch.ElapsedMilliseconds} ms; average is {_average}.");
        Game1.playSound("dwop");
        #endif

        // Cache the current UI focus selections for category and object.
        var selectedCategory = SelectedCategory;
        var selectedObject = SelectedObject;

        // If the selected category is invalid, force a full UI focus reset.
        if (!resetFocus && selectedCategory == null)
        {
            resetFocus = true;
        }

        // Reset the UI focus entirely (category and object) if necessary.
        if (resetFocus)
        {
            SetFocusToFirstObject();
        }
        // If the category is valid but the selected object is invalid, reset object focus only.
        else if (selectedObject == null)
        {
            SetFocusToFirstObject(false);
        }

        ShouldRefresh = false;
    }

    internal void HandleKeys(object? sender, ButtonsChangedEventArgs e)
    {
        bool cycleUpCategoryPressed = MainClass.Config.OTCycleUpCategory.JustPressed();
        bool cycleDownCategoryPressed = MainClass.Config.OTCycleDownCategory.JustPressed();
        bool cycleUpObjectPressed = MainClass.Config.OTCycleUpObject.JustPressed();
        bool cycleDownObjectPressed = MainClass.Config.OTCycleDownObject.JustPressed();
        bool readSelectedObjectPressed = MainClass.Config.OTReadSelectedObject.JustPressed();
        bool switchSortingModePressed = MainClass.Config.OTSwitchSortingMode.JustPressed();
        bool moveToSelectedObjectPressed = MainClass.Config.OTMoveToSelectedObject.JustPressed();
        bool readSelectedObjectTileLocationPressed = MainClass.Config.OTReadSelectedObjectTileLocation.JustPressed();
        
        int favoriteKeyJustPressed = 0;

        if (MainClass.Config.OTFavorite1.JustPressed()) favoriteKeyJustPressed = 1;
        else if (MainClass.Config.OTFavorite2.JustPressed()) favoriteKeyJustPressed = 2;
        else if (MainClass.Config.OTFavorite3.JustPressed()) favoriteKeyJustPressed = 3;
        else if (MainClass.Config.OTFavorite4.JustPressed()) favoriteKeyJustPressed = 4;
        else if (MainClass.Config.OTFavorite5.JustPressed()) favoriteKeyJustPressed = 5;
        else if (MainClass.Config.OTFavorite6.JustPressed()) favoriteKeyJustPressed = 6;
        else if (MainClass.Config.OTFavorite7.JustPressed()) favoriteKeyJustPressed = 7;
        else if (MainClass.Config.OTFavorite8.JustPressed()) favoriteKeyJustPressed = 8;
        else if (MainClass.Config.OTFavorite9.JustPressed()) favoriteKeyJustPressed = 9;
        else if (MainClass.Config.OTFavorite10.JustPressed()) favoriteKeyJustPressed = 10;
        else if (MainClass.Config.OTFavoriteDecreaseStack.JustPressed()) favoriteKeyJustPressed = 11;
        else if (MainClass.Config.OTFavoriteIncreaseStack.JustPressed()) favoriteKeyJustPressed = 12;
        else if (MainClass.Config.OTFavoriteSaveCoordinatesToggle.JustPressed()) favoriteKeyJustPressed = 13;
        else if (MainClass.Config.OTFavoriteSaveDefault.JustPressed()) favoriteKeyJustPressed = 14;

        if (favoriteKeyJustPressed > 0)
        {
            HandleFavorite(favoriteKeyJustPressed);
            foreach(var button in e.Pressed)
                MainClass.ModHelper!.Input.Suppress(button);
        }
        else 
        {
            if (e.Pressed.Any()) FavoriteKeysReset();
            if (cycleUpCategoryPressed)
            {
                SelectedCategoryIndex--;
                ReadCurrentlySelectedObject(ReadMode.Category);
            }
            else if (cycleDownCategoryPressed)
            {
                SelectedCategoryIndex++;
                ReadCurrentlySelectedObject(ReadMode.Category);
            }
            else if (cycleUpObjectPressed)
            {
                SelectedObjectIndex--;
                ReadCurrentlySelectedObject(ReadMode.Name);
            }
            else if (cycleDownObjectPressed)
            {
                SelectedObjectIndex++;
                ReadCurrentlySelectedObject(ReadMode.Name);
            }

            if (readSelectedObjectPressed || moveToSelectedObjectPressed || readSelectedObjectTileLocationPressed || switchSortingModePressed)
            {
                if (switchSortingModePressed)
                {
                    SortByProximity = !SortByProximity;
                    MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-sort_by_proximity", true,
                        translationTokens: new { is_enabled = SortByProximity ? 1 : 0 });
                }
                GetLocationObjects(resetFocus: false);
                if (readSelectedObjectPressed)
                {
                    ReadCurrentlySelectedObject();
                }
                if (moveToSelectedObjectPressed)
                {
                    MoveToCurrentlySelectedObject();
                }
                if (readSelectedObjectTileLocationPressed)
                {
                    ReadCurrentlySelectedObject(ReadMode.NameAndPosition);
                }
            }
        }
    }

    /// <summary>
    /// Attempts to move the player to a tile determined by either an override coordinate (SelectedCoordinates)
    /// or the currently selected object's position (SelectedObject), if either is available.
    /// </summary>
    /// <remarks>
    /// 1. If both <see cref="SelectedCoordinates"/> and <see cref="SelectedObject"/> are null, 
    ///    the function announces that no path could be found and returns early.
    /// 2. If <see cref="SelectedCoordinates"/> is set, it has priority over any object's position;
    ///    afterward, it is reset to null.
    /// 3. The method calls <see cref="ReadCurrentlySelectedObject"/> to provide audible feedback on 
    ///    what is being targeted before pathfinding.
    /// 4. If a valid tile is found, an announcement is made and the pathfinder is re-initialized
    ///    (disposing any prior instance) to move the player from their current location to the target tile.
    /// </remarks>
    internal void MoveToCurrentlySelectedObject()
    {
        // Determine the target tile from either an override or the selected object.
        Vector2? targetTile = SelectedCoordinates ?? (SelectedObject?.position);

        // If neither override nor object gave us a tile, there’s nowhere to go.
        if (targetTile == null)
        {
            MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-could_not_find_path", true);
            return;
        }

        Log.Trace($"Attempting pathfinding to ({targetTile.Value.X}, {targetTile.Value.Y}).");

        // (If there's nothing valid, this method won't speak.)
        ReadCurrentlySelectedObject();

        // Ensure the SelectedCoordinates are set for next time.
        SelectedCoordinates = null;

        // Announce that we’re moving to the chosen tile.
        MainClass.ScreenReader.TranslateAndSay(
            "feature-object_tracker-moving_to", 
            interrupt: true,
            translationTokens: new
            {
                object_x = (int)targetTile.Value.X,
                object_y = (int)targetTile.Value.Y
            }
        );

        // Set up the pathfinder; any existing one is disposed first.
        targetTile = GetClosestTilePath(targetTile);
        pathfinder?.Dispose();
        pathfinder = new(RetryPathfinding, StopPathfinding);

        // Start pathfinding from the player's current location to the destination tile.
        if (targetTile != null)
        {
            Farmer player = Game1.player;
            pathfinder.StartPathfinding(player, Game1.currentLocation, targetTile.Value.ToPoint());
        }
    }

    internal void SaveToFavorites(int hotkey)
    {
        string location = Game1.currentLocation.currentEvent is not null ? Game1.currentLocation.currentEvent.FestivalName : Game1.currentLocation.NameOrUniqueName;
        string currentSaveFileName = MainClass.GetCurrentSaveFileName();
        var selectedObject = SelectedObject;
        if (!selectedObject.HasValue) return;
        if (!favorites.ContainsKey(currentSaveFileName))
        {
            favorites[currentSaveFileName] = [];
        }
        if (!favorites[currentSaveFileName].ContainsKey(location))
        {
            favorites[currentSaveFileName][location] = [];
        }

        if (SaveCoordinatesToggle)
        {
            favorites[MainClass.GetCurrentSaveFileName()][location][hotkey] = (Vector2ToString(CurrentPlayer.FacingTile), "coordinates");
        } else {
            favorites[MainClass.GetCurrentSaveFileName()][location][hotkey] = (selectedObject.Value.name, SelectedCategory);
        }
        SaveFavorites();
    }

    internal (string?, string?) GetFromFavorites(int hotkey)
    {
        string location = Game1.currentLocation.currentEvent is not null ? Game1.currentLocation.currentEvent.FestivalName : Game1.currentLocation.NameOrUniqueName;
        if (!favorites.TryGetValue(MainClass.GetCurrentSaveFileName(), out var _saveFileFavorites) || _saveFileFavorites != null)
        {
            LoadDefaultFavorites();
            SaveFavorites();
        }
        if (favorites.TryGetValue(MainClass.GetCurrentSaveFileName(), out var saveFileFavorites) && saveFileFavorites != null)
        {
            if (saveFileFavorites.TryGetValue(location, out var locationFavorites) && locationFavorites.TryGetValue(hotkey, out var value))
            {
                return value;
            }
        }

        return (null, null);
    }

    internal void SetFromFavorites(int hotkey)
    {
        var (obj, category) = GetFromFavorites(hotkey);
        if (category != null && obj != null)
        {
            SelectedCategory = category;
            SelectedCoordinates = StringToVector2(obj);
            if (TrackedObjects.TryGetValue(_selectedCategory, out var catObjects))
            {
                foreach (var item in catObjects)
                { 
                    if (item.name == obj)
                        SelectedObject = item;
                }
            }
        }
    }

    internal void DeleteFavorite(int favoriteNumber)
    {
        string currentLocation = Game1.currentLocation.currentEvent is not null ? Game1.currentLocation.currentEvent.FestivalName : Game1.currentLocation.NameOrUniqueName;

        // Try to get the sub-dictionary for the current location
        if (favorites.TryGetValue(MainClass.GetCurrentSaveFileName(), out var saveFileFavorites) && saveFileFavorites != null)
        {
            if (saveFileFavorites.TryGetValue(currentLocation, out var locationFavorites))
            {
                // Remove the favorite entry if it exists
                locationFavorites.Remove(favoriteNumber);
                if (locationFavorites.Count == 0)
                {
                    // If empty, remove the location from the favorites
                    favorites.Remove(currentLocation);
                }
                SaveFavorites();
            }
        }
    }

    private void FavoriteKeysReset()
    {
        lastFavoritePressed = 0;
        sameFavoritePressed = -1;
        lastPressTimer.Stop();
        SelectedCoordinates = null;
    }

    private void HandleFavorite(int favKeyNum)
    {
        if (lastFavoritePressed == favKeyNum)
        {
            sameFavoritePressed++;
        }
        else
        {
            sameFavoritePressed = 1;
            lastFavoritePressed = favKeyNum;
            lastPressTimer.Stop();
            lastPressTimer.Start();
        }

        if (favKeyNum > 10)
        {
            switch (favKeyNum)
            {
                case 11:
                    FavoriteStack--;
                    MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-read_favorite_stack", true,
                        new {
                            stack_number = FavoriteStack + 1
                        }
                    );
                    break;
                case 12:
                    FavoriteStack++;
                    MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-read_favorite_stack", true,
                        new {
                            stack_number = FavoriteStack + 1
                        }
                    );
                    break;
                case 13:
                    SaveCoordinatesToggle = !SaveCoordinatesToggle;
                    if (!SaveCoordinatesToggle)
                    {
                        SetFocusToFirstObject(true);
                    }
                    MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-save_coordinates_toggle", true,
                            translationTokens: new { is_enabled = SaveCoordinatesToggle ? 1 : 0 });
                    break;
                case 14:
                    if (sameFavoritePressed <= 1)
                        SetAsDefaultFavorites();
                    else
                        ClearDefaultFavorites();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(favKeyNum), favKeyNum, "The favorite key number cannot be greater than 12.");
            }
        } else {

            int favorite_number = favKeyNum + (FavoriteStack * 10);
            string? targetObject, targetCategory;
            (targetObject, targetCategory) = GetFromFavorites(favorite_number);
            bool isFavoriteSet = targetObject != null && targetCategory != null;
            // Logic for handling single, double, or triple presses
            if (sameFavoritePressed == 1)
            {
                // Handle single press
                if (isFavoriteSet)
                {
                    MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-read_favorite", true,
                        new
                        {
                            favorite_number,
                            target_object = targetObject,
                            target_category = targetCategory
                        }
                    );
                }
                else
                {
                    MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-favorite_unset", true,
                        new
                        {
                            favorite_number
                        }
                    );
                }
                lastPressTimer.Start();
            }
            else if (sameFavoritePressed == 2)
            {
                if (isFavoriteSet)
                {
                    // start a timer that will begin movement in `PressInterval` ms, allowing time for 3rd press to cancel it.
                    navigateToFavorite = favorite_number;
                    navigationTimer.Start();
                }
                else
                {
                    // this slot is unset; save current tracker target here
                    // Only if `SelectedObject` and `SelectedCategory` are both not null
                    if (SelectedObject != null && SelectedCategory != null)
                    {
                        SaveToFavorites(favorite_number);
                        if (SaveCoordinatesToggle)
                        {
                            MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-favorite_save_coordinates", true,
                                new
                                {
                                    coordinates = Vector2ToString(CurrentPlayer.FacingTile),
                                    location_name = Game1.currentLocation.currentEvent is not null ? Game1.currentLocation.currentEvent.FestivalName : Game1.currentLocation.NameOrUniqueName,
                                    favorite_number
                                }
                            );
                        } else {
                            MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-favorite_save", true,
                                new
                                {
                                    selected_object = SelectedObject,
                                    selected_category = SelectedCategory,
                                    location_name = Game1.currentLocation.currentEvent is not null ? Game1.currentLocation.currentEvent.FestivalName : Game1.currentLocation.NameOrUniqueName,
                                    favorite_number
                                }
                            );
                        }
                    }
                    else
                    {
                        MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-no_destination_selected", true);
                    }
                }
            }
            else if (sameFavoritePressed >= 3)
            {
                navigationTimer.Stop();
                navigateToFavorite = -1;
                DeleteFavorite(favorite_number);
                MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-favorite_cleared", true,
                    new
                    {
                        location_name = Game1.currentLocation.currentEvent is not null ? Game1.currentLocation.currentEvent.FestivalName : Game1.currentLocation.NameOrUniqueName,
                        favorite_number
                    }
                );
            }
        }
    }

    internal void LoadFavorites()
    {
        try
        {
            if (JsonLoader.TryLoadJsonFile("favorites.json", out JToken? jsonToken, "assets/TileData") && jsonToken is not null)
            {
                try
                {
                    favorites = jsonToken.ToObject<Dictionary<string, Dictionary<string, Dictionary<int, (string?, string?)>>>>() ?? [];
                }
                catch (JsonSerializationException)
                {
                    // Attempt to load in the old format
                    var oldFormatFavorites = jsonToken.ToObject<Dictionary<string, Dictionary<int, (string?, string?)>>>();
                    if (oldFormatFavorites != null)
                    {
                        favorites = new Dictionary<string, Dictionary<string, Dictionary<int, (string?, string?)>>>
                        {
                            [""] = oldFormatFavorites
                        };
                        Log.Alert("Loaded and converted favorites from the old format to the new format.");
                        LoadDefaultFavorites();
                        SaveFavorites();
                    }
                    else
                    {
                        favorites = [];
                    }
                }

                if (!favorites.TryGetValue(MainClass.GetCurrentSaveFileName(), out var saveFileFavorites) && favorites.TryGetValue("", out var defaultFavorites) && defaultFavorites != null)
                {
                    LoadDefaultFavorites();
                }
            }
            else
            {
                throw new FileNotFoundException("Could not load favorites.json or the file is empty.");
            }
        }
        catch (Exception ex)
        {
            Log.Warn(ex.Message);
            favorites = [];
        }
    }

    private void LoadDefaultFavorites()
    {
        if (favorites.TryGetValue("", out var defaultFavorites) && defaultFavorites != null)
        {
            favorites[MainClass.GetCurrentSaveFileName()] = defaultFavorites;
        }
    }
    
    private void SetAsDefaultFavorites()
    {
        if (favorites.TryGetValue(MainClass.GetCurrentSaveFileName(), out var saveFileFavorites) && saveFileFavorites != null)
        {
            favorites[""] = saveFileFavorites;
            MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-favorite_set_as_default", true);
            SaveFavorites();
        }
    }
    
    private void ClearDefaultFavorites()
    {
        favorites.Remove("");
        MainClass.ScreenReader.TranslateAndSay("feature-object_tracker-favorite_default_cleared", true);
    }

    internal void SaveFavorites()
    {
        #if DEBUG
        Log.Verbose("Saving favorites");
        #endif
        JsonLoader.SaveJsonFile("favorites.json", favorites, "assets/TileData");
    }
    
    private static string Vector2ToString(Vector2 coordinates)
    {
        return $"{coordinates.X}, {coordinates.Y}";
    }   

    private static Vector2? StringToVector2(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return null;
        string[] coordinates = input.Split(',');
        if (coordinates.Length != 2)
            return null;
        if (float.TryParse(coordinates[0].Trim(), out float x) && float.TryParse(coordinates[1].Trim(), out float y))
        {
            return new Vector2(x, y);
        }
        return null;
    }

}
