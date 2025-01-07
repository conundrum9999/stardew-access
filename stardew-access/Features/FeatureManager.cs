using stardew_access.Patches;
using stardew_access.Utils;
using StardewModdingAPI.Events;
using StardewValley;

namespace stardew_access.Features;

public class FeatureManager
{
    private static readonly List<FeatureBase> AllFeatures =
    [
        KonamiCode.Instance,
        PlayerTriggered.Instance,
        ReadTile.Instance,
        TileViewer.Instance,
        GridMovement.Instance,
        ObjectTracker.Instance,
        GameStateNarrator.Instance,
        Warnings.Instance,
        Radar.Instance,
    ];

    internal static void  OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        foreach (FeatureBase feature in AllFeatures)
        {
            try
            {
                feature.OnUpdateTicked(sender, e);
            }
            catch (Exception exception)
            {
                Log.Error(
                    $"An error occurred in OnUpdateTicked of {feature.GetType().FullName} feature:\n{exception.Message}\n{exception.StackTrace}");
                throw;
            }
        }
    }

    internal static void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        #region Simulate left and right clicks

        if (!TextBoxPatch.IsAnyTextBoxActive)
        {
            if (Game1.activeClickableMenu != null)
            {
                MouseUtils.SimulateMouseClicks(
                    (x, y) => Game1.activeClickableMenu.receiveLeftClick(x, y),
                    (x, y) => Game1.activeClickableMenu.receiveRightClick(x, y)
                );
            }
            else if (Game1.currentMinigame != null)
            {
                MouseUtils.SimulateMouseClicks(
                    (x, y) => Game1.currentMinigame.receiveLeftClick(x, y),
                    (x, y) => Game1.currentMinigame.receiveRightClick(x, y)
                );
            }
        }

        #endregion

        foreach (FeatureBase feature in AllFeatures)
        {
            try
            {
                if (feature.OnButtonPressed(sender, e)) break;
            }
            catch (Exception exception)
            {
                Log.Error(
                    $"An error occurred in OnButtonPressed of {feature.GetType().FullName} feature:\n{exception.Message}\n{exception.StackTrace}");
                throw;
            }
        }
    }

    internal static void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        foreach (FeatureBase feature in AllFeatures)
        {
            try
            {
                feature.OnButtonsChanged(sender, e);
            }
            catch (Exception exception)
            {
                Log.Error(
                    $"An error occurred in OnButtonsChanged of {feature.GetType().FullName} feature:\n{exception.Message}\n{exception.StackTrace}");
                throw;
            }
        }
    }

    internal static void OnWarped(object? sender, WarpedEventArgs e)
    {
        foreach (FeatureBase feature in AllFeatures)
        {
            try
            {
                feature.OnWarped(sender, e);
            }
            catch (Exception exception)
            {
                Log.Error(
                    $"An error occurred in OnWarped of {feature.GetType().FullName} feature:\n{exception.Message}\n{exception.StackTrace}");
                throw;
            }
        }
    }

    internal static void OnBuildingListChanged(object? sender, BuildingListChangedEventArgs e)
    {
        foreach (FeatureBase feature in AllFeatures)
        {
            try
            {
                feature.OnBuildingListChanged(sender, e);
            }
            catch (Exception exception)
            {
                Log.Error(
                    $"An error occurred in OnBuildingListChanged of {feature.GetType().FullName} feature:\n{exception.Message}\n{exception.StackTrace}");
                throw;
            }
        }
    }

    internal static void OnDebrisListChanged(object? sender, DebrisListChangedEventArgs e)
    {
        foreach (FeatureBase feature in AllFeatures)
        {
            try
            {
                feature.OnDebrisListChanged(sender, e);
            }
            catch (Exception exception)
            {
                Log.Error(
                    $"An error occurred in OnDebrisListChanged of {feature.GetType().FullName} feature:\n{exception.Message}\n{exception.StackTrace}");
                throw;
            }
        }
    }

    internal static void OnFurnitureListChanged(object? sender, FurnitureListChangedEventArgs e)
    {
        foreach (FeatureBase feature in AllFeatures)
        {
            try
            {
                feature.OnFurnitureListChanged(sender, e);
            }
            catch (Exception exception)
            {
                Log.Error(
                    $"An error occurred in OnFurnitureListChanged of {feature.GetType().FullName} feature:\n{exception.Message}\n{exception.StackTrace}");
                throw;
            }
        }
    }

    internal static void OnLargeTerrainFeatureListChanged(object? sender, LargeTerrainFeatureListChangedEventArgs e)
    {
        foreach (FeatureBase feature in AllFeatures)
        {
            try
            {
                feature.OnLargeTerrainFeatureListChanged(sender, e);
            }
            catch (Exception exception)
            {
                Log.Error(
                    $"An error occurred in OnLargeTerrainFeatureListChanged of {feature.GetType().FullName} feature:\n{exception.Message}\n{exception.StackTrace}");
                throw;
            }
        }
    }

    internal static void OnNpcListChanged(object? sender, NpcListChangedEventArgs e)
    {
        foreach (FeatureBase feature in AllFeatures)
        {
            try
            {
                feature.OnNpcListChanged(sender, e);
            }
            catch (Exception exception)
            {
                Log.Error(
                    $"An error occurred in OnNpcListChanged of {feature.GetType().FullName} feature:\n{exception.Message}\n{exception.StackTrace}");
                throw;
            }
        }
    }

    internal static void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
    {
        foreach (FeatureBase feature in AllFeatures)
        {
            try
            {
                feature.OnObjectListChanged(sender, e);
            }
            catch (Exception exception)
            {
                Log.Error(
                    $"An error occurred in On of {feature.GetType().FullName} feature:\n{exception.Message}\n{exception.StackTrace}");
                throw;
            }
        }
    }

    internal static void OnTerrainFeatureListChanged(object? sender, TerrainFeatureListChangedEventArgs e)
    {
        foreach (FeatureBase feature in AllFeatures)
        {
            try
            {
                feature.OnTerrainFeatureListChanged(sender, e);
            }
            catch (Exception exception)
            {
                Log.Error(
                    $"An error occurred in OnTerrainFeatureListChanged of {feature.GetType().FullName} feature:\n{exception.Message}\n{exception.StackTrace}");
                throw;
            }
        }
    }
}