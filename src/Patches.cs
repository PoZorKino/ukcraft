namespace UKCraft;

using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

/// <summary> Every damaging explosion in the game leaves a crater. </summary>
[HarmonyPatch(typeof(Explosion), "Start")]
public static class ExplosionPatch
{
    private static Vector3 lastPos;
    private static float lastRadius;
    private static int lastFrame = -1;

    private static void Postfix(Explosion __instance)
    {
        if (!Plugin.AllExplosionsDestroy.Value) return;
        if (__instance.harmless || __instance.damage <= 0) return;
        if (__instance.GetComponentInParent<OwnExplosion>() != null) return;

        var pos = __instance.transform.position;
        float radius = __instance.maxSize * Plugin.ExplosionCarveScale.Value;

        // one prefab often holds several Explosion components in the same spot
        if (Time.frameCount - lastFrame <= 1 && (pos - lastPos).sqrMagnitude < 1f && radius <= lastRadius) return;
        lastPos = pos; lastRadius = radius; lastFrame = Time.frameCount;

        Destruction.Crater(pos, radius);
    }
}

/// <summary> Anything that would break a breakable lights TNT instead: bullets, punches, nails, explosions. </summary>
[HarmonyPatch(typeof(Breakable), nameof(Breakable.Break), typeof(float))]
public static class TntHitPatch
{
    private static bool Prefix(Breakable __instance)
    {
        if (!__instance.TryGetComponent<TntMarker>(out var marker)) return true;
        PrimedTnt.Prime(marker.cell, 1f);
        return false;
    }
}

/// <summary> A world you can blow holes in makes any time or score meaningless, so nothing is sent to the leaderboards while the mod is loaded. </summary>
[HarmonyPatch]
public static class LeaderboardPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(LeaderboardController), nameof(LeaderboardController.SubmitLevelScore));
        yield return AccessTools.Method(typeof(LeaderboardController), nameof(LeaderboardController.SubmitCyberGrindScore));
        yield return AccessTools.Method(typeof(LeaderboardController), nameof(LeaderboardController.SubmitFishSize));
    }

    private static bool Prefix() => false;
}
