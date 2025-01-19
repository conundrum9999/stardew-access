using HarmonyLib;
using Microsoft.Xna.Framework;
using stardew_access.Features;
using StardewValley;
using System.Reflection;
//using xTile.Dimensions;

namespace stardew_access.Patches;

internal class CharacterPatch : IPatch
{
    public void Apply(Harmony harmony)
    {
        harmony.Patch(
                original: AccessTools.Method(typeof(Character), nameof(Character.MovePosition)),
                postfix: new HarmonyMethod(typeof(CharacterPatch), nameof(CharacterPatch.MovePositionPostfix))
        );
    }

    /// <summary>
    /// Postfix for Character.MovePosition method.
    /// Sets a flag in ObjectTracker if a character within the player's current location has moved.
    /// </summary>
    /// <param name="__instance">The instance of the Character being moved.</param>
    /// <param name="time">The game time at which the movement occurred.</param>
    /// <param name="viewport">The current viewport rectangle.</param>
    /// <param name="currentLocation">The current location of the character.</param>
    private static void MovePositionPostfix(
        Character __instance,
        GameTime time,
        Rectangle viewport,
        GameLocation currentLocation
    )
    {
        // Ensure the character is moving within the player's current location
        if (currentLocation.Equals(Game1.currentLocation))
        {
            // Notify ObjectTracker that a character has moved
            ObjectTracker.InvalidateTrackerData();
        }
    }
}