using StardewModdingAPI.Events;

namespace stardew_access.Features;

public abstract class FeatureBase
{
    public static FeatureBase Instance => throw new Exception("Override Instance property!!");

    internal abstract void OnUpdateTicked(object? sender, UpdateTickedEventArgs e);

    internal virtual bool OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        return false;
    }

    internal virtual void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
    }

    internal virtual void OnWarped(object? sender, WarpedEventArgs e)
    {
    }

    internal virtual void OnBuildingListChanged(object? sender, BuildingListChangedEventArgs e)
    {
    }

    internal virtual void OnDebrisListChanged(object? sender, DebrisListChangedEventArgs e)
    {
    }

    internal virtual void OnFurnitureListChanged(object? sender, FurnitureListChangedEventArgs e)
    {
    }


    internal virtual void OnLargeTerrainFeatureListChanged(object? sender, LargeTerrainFeatureListChangedEventArgs e)
    {
    }

    internal virtual void OnNpcListChanged(object? sender, NpcListChangedEventArgs e)
    {
    }

    internal virtual void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
    {
    }


    internal virtual void OnTerrainFeatureListChanged(object? sender, TerrainFeatureListChangedEventArgs e)
    {
    }
}