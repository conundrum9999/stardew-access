using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using stardew_access.Features;
using StardewValley;
using System.Reflection;

namespace stardew_access.Patches;

internal class EventPatch : IPatch
{
    // Variable to monitor egg count during Egg Hunt
    private static int LastEggCount = 0;
    // Variable to monitor playerControlSequenceID changes during events
    private static string LastplayerControlSequenceID = "";
    
    public void Apply(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Method(typeof(Event), "draw", [typeof(SpriteBatch)]),
            postfix: new HarmonyMethod(typeof(EventPatch), nameof(DrawPostfix))
        );
    }

    /// <summary>
    /// Postfix for Event.Draw method.
    /// Detects changes during Events
    /// </summary>
    /// <param name="__instance">The instance of the current Event.</param>
    /// <param name="b">The SpriteBatch instance.</param>
    private static void DrawPostfix(
        Event __instance,
        SpriteBatch b
    )
    {
        Log.Trace($"asdf: {__instance.playerControlSequenceID ?? "null"}", true);
        if (__instance.playerControlSequenceID != null && __instance.playerControlSequenceID != LastplayerControlSequenceID)
        {
            #if DEBUG
            Log.Trace($"Event playerControlSequenceID switching from {LastplayerControlSequenceID ?? "null"} to {__instance.playerControlSequenceID} in {__instance.id}");
            #endif
            LastplayerControlSequenceID = __instance.playerControlSequenceID;
        }
        // Switch on the event's id
        switch (__instance  .id)
        {
            case "festival_spring13": // Egg Festival
                
                break;
            default:
                // Do nothing; unhandled
                break;
        }
    }
}