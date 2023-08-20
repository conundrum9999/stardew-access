using Microsoft.Xna.Framework;
using static stardew_access.Log;
using stardew_access.Translation;
using stardew_access.Utils;
using StardewModdingAPI;
using StardewValley;

namespace stardew_access.Features
{
    /// <summary>
    /// Reads the name and information about a tile.
    /// </summary>
    internal class ReadTile
    {
        private bool isBusy; // To pause execution of run method between fixed intervals
        private readonly int delay; // Length of each interval (in ms)
        private bool shouldPause; // To pause the execution
        private Vector2 prevTile;

        public ReadTile()
        {
            isBusy = false;
            delay = 100;
        }

        public void Update()
        {
            if (this.isBusy)
                return;

            if (this.shouldPause)
                return;

            this.isBusy = true;
            this.Run();
            Task.Delay(delay).ContinueWith(_ => { this.isBusy = false; });
        }

        /// <summary>
        /// Pauses the feature for the provided time.
        /// </summary>
        /// <param name="time">The amount of time we want to pause the execution (in ms).<br/>Default is 2500 (2.5s).</param>
        public void PauseUntil(int time = 2500)
        {
            this.shouldPause = true;
            Task.Delay(time).ContinueWith(_ => { this.shouldPause = false; });
        }

        /// <summary>
        /// Pauses the feature
        /// </summary>
        public void Pause()
        {
            this.shouldPause = true;
        }

        /// <summary>
        /// Resumes the feature
        /// </summary>
        public void Resume()
        {
            this.shouldPause = false;
        }

        public void Run(bool manuallyTriggered = false, bool playersPosition = false)
        {
            try
            {
                Vector2 tile;

                #region Get Tile
                int x, y;
                if (!playersPosition)
                {
                    // Grab tile
                    tile = CurrentPlayer.FacingTile;
                }
                else
                {
                    // Player's standing tile
                    tile = CurrentPlayer.Position;
                }
                x = (int)tile.X;
                y = (int)tile.Y;
                #endregion

                // The event with id 13 is the Haley's six heart event, the one at the beach requiring the player to find the bracelet
                if (Context.IsPlayerFree || (Game1.CurrentEvent is not null && Game1.CurrentEvent.id == 13))
                {
                    if (!manuallyTriggered && prevTile != tile)
                    {
                        if (MainClass.ScreenReader != null)
                            MainClass.ScreenReader.PrevTextTile = "";
                    }

                    var currentLocation = Game1.currentLocation;
                    bool isColliding = TileInfo.IsCollidingAtTile(currentLocation, x, y);

                    (string? name, string? category) = TileInfo.GetNameWithCategoryNameAtTile(tile, currentLocation);

                    #region Narrate toSpeak
                    if (name != null)
                        if (MainClass.ScreenReader != null)
                            if (manuallyTriggered)
                                MainClass.ScreenReader.Say(Translator.Instance.Translate("feature-read_tile-manually_triggered_info", new {tile_name = name, tile_category = category}), true);
                            else
                                MainClass.ScreenReader.SayWithTileQuery(name, x, y, true);
                            #if DEBUG
                            if (manuallyTriggered)
                            {
                                var backTileIndex = Game1.currentLocation.Map.GetLayer("Back")?.Tiles[x, y]?.TileIndex ?? -1;
                                var buildingsTileIndex = Game1.currentLocation.Map.GetLayer("Buildings")?.Tiles[x, y]?.TileIndex ?? -1;
                                var pathsTileIndex = Game1.currentLocation.Map.GetLayer("Paths")?.Tiles[x, y]?.TileIndex ?? -1;
                                var frontTileIndex = Game1.currentLocation.Map.GetLayer("Front")?.Tiles[x, y]?.TileIndex ?? -1;
                                var alwaysFrontTileIndex = Game1.currentLocation.Map.GetLayer("AlwaysFront")?.Tiles[x, y]?.TileIndex ?? -1;
                                MainClass.ScreenReader?.Say($"Tile indexes: back {backTileIndex}, buildings {buildingsTileIndex}, paths {pathsTileIndex}, front {frontTileIndex}, always front {alwaysFrontTileIndex}", false);
                            }
                            #endif
                    #endregion

                    #region Play colliding sound effect
                    if (isColliding && prevTile != tile)
                    {
                        Game1.playSound("colliding");
                    }
                    #endregion

                    if (!manuallyTriggered)
                        prevTile = tile;
                }

            }
            catch (Exception e)
            {
                Log.Error($"Error in Read Tile:\n{e.Message}\n{e.StackTrace}");
            }
        }
    }
}
