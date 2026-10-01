namespace UKCraft;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary> Dumps what the current level is made of to the log. Used to check the mod's assumptions against the real game. </summary>
public static class Probe
{
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== probe: scene {SceneManager.GetActiveScene().name} ===");

        var layers = new StringBuilder("layers:");
        for (int i = 0; i < 32; i++) if (LayerMask.LayerToName(i) != "") layers.Append($" {i}={LayerMask.LayerToName(i)}");
        sb.AppendLine(layers.ToString());

        int mask = Game.EnvMask;
        var mrs = Object.FindObjectsOfType<MeshRenderer>().Where(r => (mask & (1 << r.gameObject.layer)) != 0).ToArray();
        sb.AppendLine($"env mesh renderers: {mrs.Length}, enabled {mrs.Count(r => r.enabled)}, static batched {mrs.Count(r => r.isPartOfStaticBatch)}");

        var meshes = new Dictionary<Mesh, int>();
        foreach (var r in mrs)
            if (r.enabled && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
                meshes[mf.sharedMesh] = meshes.TryGetValue(mf.sharedMesh, out int n) ? n + 1 : 1;
        sb.AppendLine($"distinct visual meshes: {meshes.Count}, readable {meshes.Keys.Count(m => m.isReadable)}");
        foreach (var pair in meshes.OrderByDescending(p => p.Key.vertexCount).Take(8))
        {
            var m = pair.Key;
            var attrs = string.Join(",", m.GetVertexAttributes().Select(a => $"{a.attribute}:{a.format}x{a.dimension}@{a.stream}"));
            sb.AppendLine($"  mesh '{m.name}' verts {m.vertexCount} subs {m.subMeshCount} readable {m.isReadable} users {pair.Value} [{attrs}]");
        }

        var mats = new Dictionary<string, int>();
        foreach (var r in mrs)
            foreach (var mat in r.sharedMaterials)
                if (mat != null)
                {
                    string key = $"{mat.shader.name} [{string.Join(" ", mat.shaderKeywords)}]";
                    mats[key] = mats.TryGetValue(key, out int n) ? n + 1 : 1;
                }
        foreach (var pair in mats.OrderByDescending(p => p.Value).Take(8)) sb.AppendLine($"  material x{pair.Value}: {pair.Key}");

        var cols = Object.FindObjectsOfType<Collider>().Where(c => !c.isTrigger && (mask & (1 << c.gameObject.layer)) != 0).ToArray();
        var mcs = cols.OfType<MeshCollider>().Where(c => c.sharedMesh != null).ToArray();
        sb.AppendLine($"env colliders: {cols.Length}: mesh {mcs.Length} (convex {mcs.Count(c => c.convex)}, readable {mcs.Count(c => c.sharedMesh.isReadable)}), box {cols.OfType<BoxCollider>().Count()}, other {cols.Count(c => c is not MeshCollider and not BoxCollider)}");
        foreach (var c in mcs.OrderByDescending(c => c.sharedMesh.vertexCount).Take(5))
            sb.AppendLine($"  collider '{c.name}' mesh '{c.sharedMesh.name}' verts {c.sharedMesh.vertexCount} readable {c.sharedMesh.isReadable}");

        foreach (var opt in Object.FindObjectsOfType<StaticSceneOptimizer>())
            sb.AppendLine($"static scene optimizer: {opt.staticMRends.Count} renderers, baked data {(opt.bakedDataAsset == null ? "none" : $"{opt.bakedDataAsset.bakedMeshes.Count} meshes")}, compute {opt.usedComputeShadersAtStart}");

        var refs = MonoSingleton<DefaultReferenceManager>.Instance;
        if (refs != null)
        {
            sb.AppendLine($"master shader: {(refs.masterShader == null ? "null" : refs.masterShader.name)}");
            foreach (var prefab in new[] { refs.explosion, refs.superExplosion })
                if (prefab != null)
                    foreach (var e in prefab.GetComponentsInChildren<Explosion>(true))
                        sb.AppendLine($"  explosion prefab '{prefab.name}/{e.name}': maxSize {e.maxSize} damage {e.damage} speed {e.speed} enemy {e.enemy} canHit {e.canHit}");
            sb.AppendLine($"filth prefab: {(refs.filth == null ? "null" : refs.filth.name)}");
        }

        var p = Game.Player;
        if (p != null)
        {
            var b = p.playerCollider.bounds;
            sb.AppendLine($"player pos {p.transform.position} collider {b.min} .. {b.max} layer {p.gameObject.layer}");
        }
        sb.AppendLine($"blocks placed {VoxelWorld.I.BlockCount}, carves {LevelCarver.Carves}, triangles cut {LevelCarver.TrianglesCut}");
        sb.Append("=== end probe ===");
        Plugin.Log.LogInfo(sb.ToString());
    }
}
