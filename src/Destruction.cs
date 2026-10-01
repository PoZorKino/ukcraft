namespace UKCraft;

using System.Collections.Generic;
using UnityEngine;

/// <summary> Tags an explosion the mod spawned itself, so the patch on Explosion doesn't carve a second time. </summary>
public class OwnExplosion : MonoBehaviour { }

/// <summary> Turns an explosion into a block-shaped crater, in placed blocks and in the level. </summary>
public static class Destruction
{
    private static readonly HashSet<Vector3Int> cells = new();

    /// <summary> Stable per-cell noise in 0..1, so a crater has the ragged edge Minecraft craters have. </summary>
    private static float Noise(Vector3Int c)
    {
        unchecked
        {
            uint h = (uint)(c.x * 73856093) ^ (uint)(c.y * 19349663) ^ (uint)(c.z * 83492791);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xFFFF) / 65535f;
        }
    }

    /// <summary> Destroys everything within radius (in game units) of the point. </summary>
    public static void Crater(Vector3 center, float radius)
    {
        float s = VoxelWorld.S;
        if (radius < s * 0.5f) return;

        cells.Clear();
        var min = VoxelWorld.CellAt(center - Vector3.one * radius);
        var max = VoxelWorld.CellAt(center + Vector3.one * radius);
        var world = VoxelWorld.I;

        for (int x = min.x; x <= max.x; x++) for (int y = min.y; y <= max.y; y++) for (int z = min.z; z <= max.z; z++)
        {
            var c = new Vector3Int(x, y, z);
            float d = Vector3.Distance(VoxelWorld.CellCenter(c), center);
            if (d > radius * (0.72f + 0.28f * Noise(c))) continue;

            var id = world.Get(c);
            if (Blocks.BlastProof(id)) continue;

            cells.Add(c);
            if (id == BlockId.Tnt) PrimedTnt.Prime(c, Random.Range(0.5f, 1.5f));
            else if (id != BlockId.Air) world.Set(c, BlockId.Air);
        }

        if (Plugin.DestroyLevel.Value && cells.Count > 0) LevelCarver.Carve(cells);
    }

    /// <summary> Spawns one of the game's own explosions (damage, knockback, sound, shake) and craters the world under it. </summary>
    public static void Explode(Vector3 pos, float blocks, bool big)
    {
        var refs = MonoSingleton<DefaultReferenceManager>.Instance;
        var prefab = refs == null ? null : big ? refs.superExplosion : refs.explosion;
        if (prefab != null)
        {
            var go = Object.Instantiate(prefab, pos, Quaternion.identity);
            go.AddComponent<OwnExplosion>();
            foreach (var e in go.GetComponentsInChildren<Explosion>(true))
            {
                e.enemy = true;
                e.canHit = AffectedSubjects.All;
                e.friendlyFire = true;
            }
        }
        Crater(pos, blocks * VoxelWorld.S);
    }
}
