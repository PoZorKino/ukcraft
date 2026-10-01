namespace UKCraft;

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Cuts block-shaped holes into the level itself. Every triangle that crosses the destroyed cells is sliced along the block grid,
/// the pieces inside destroyed cells are dropped and the rest is kept, for both what you see and what you collide with.
/// </summary>
public static class LevelCarver
{
    /// <summary> Meshes the mod made its own copy of, by the instance id of the renderer or collider that uses them. </summary>
    private static readonly Dictionary<int, Mesh> owned = new();
    /// <summary> Readable copies of the big baked level meshes, made once per scene. </summary>
    private static readonly Dictionary<Mesh, Mesh> bakedCopies = new();
    private static readonly Dictionary<Mesh, MeshData> bakedData = new();
    /// <summary> Which part of a baked level mesh each static renderer draws. </summary>
    private static Dictionary<MeshRenderer, int> bakedSubMesh;
    private static readonly HashSet<Mesh> unreadable = new();

    private static MeshRenderer[] renderers;
    private static float renderersTime = -100f;

    public static int Carves, TrianglesCut;

    public static void Reset()
    {
        foreach (var m in owned.Values) if (m != null) Object.Destroy(m);
        foreach (var pair in bakedCopies) if (pair.Value != null && pair.Value != pair.Key) Object.Destroy(pair.Value);
        owned.Clear();
        bakedCopies.Clear();
        bakedData.Clear();
        unreadable.Clear();
        bakedSubMesh = null;
        renderers = null;
        renderersTime = -100f;
    }

    #region entry

    private static HashSet<Vector3Int> cells;
    private static Vector3 bmin, bmax;
    private static float S;

    public static void Carve(HashSet<Vector3Int> destroyed)
    {
        if (destroyed.Count == 0) return;
        var watch = System.Diagnostics.Stopwatch.StartNew();

        cells = destroyed;
        S = VoxelWorld.S;
        var lo = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
        var hi = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        foreach (var c in destroyed) { lo = Vector3Int.Min(lo, c); hi = Vector3Int.Max(hi, c); }
        bmin = (Vector3)lo * S;
        bmax = (Vector3)(hi + Vector3Int.one) * S;
        var bounds = new Bounds((bmin + bmax) * 0.5f, bmax - bmin);

        int cut = 0, objects = 0;

        // what you collide with
        var hits = Physics.OverlapBox(bounds.center, bounds.extents + Vector3.one * 0.05f, Quaternion.identity, Game.EnvMask, QueryTriggerInteraction.Ignore);
        foreach (var col in hits)
        {
            if (col == null || col.GetComponent<ChunkTag>() != null || col.GetComponent<TntMarker>() != null) continue;
            if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic) continue;
            try
            {
                int n = col is MeshCollider mc ? CarveCollider(mc) : col is BoxCollider bc ? CarveBox(bc) : 0;
                if (n > 0) { cut += n; objects++; }
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"carve collider {col.name}: {e.Message}"); }
        }

        // what you see
        if (renderers == null || Time.unscaledTime - renderersTime > 2f)
        {
            renderers = Object.FindObjectsOfType<MeshRenderer>();
            renderersTime = Time.unscaledTime;
        }
        int mask = Game.EnvMask;
        foreach (var mr in renderers)
        {
            if (mr == null || !mr.enabled || (mask & (1 << mr.gameObject.layer)) == 0 || !mr.gameObject.activeInHierarchy) continue;
            if (!mr.bounds.Intersects(bounds) || mr.GetComponent<ChunkTag>() != null) continue;
            try
            {
                int n = CarveRenderer(mr);
                if (n > 0) { cut += n; objects++; }
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"carve renderer {mr.name}: {e.Message}"); }
        }

        Carves++;
        TrianglesCut += cut;
        if (cut > 0) Plugin.Log.LogInfo($"carved {destroyed.Count} cells: {cut} triangles in {objects} objects, {watch.ElapsedMilliseconds} ms");
    }

    #endregion
    #region colliders

    private static int CarveCollider(MeshCollider mc)
    {
        var src = mc.sharedMesh;
        if (src == null) return 0;

        int id = mc.GetInstanceID();
        if (!owned.TryGetValue(id, out var mesh) || mesh == null)
        {
            mesh = Editable(src);
            if (mesh == null) return 0;
            int first = CarveMesh(mesh, mc.transform.localToWorldMatrix, true);
            if (first == 0) { Object.Destroy(mesh); return 0; }
            owned[id] = mesh;
            Assign(mc, mesh);
            return first;
        }

        int n = CarveMesh(mesh, mc.transform.localToWorldMatrix, true);
        if (n > 0) Assign(mc, mesh);
        return n;
    }

    private static void Assign(MeshCollider mc, Mesh mesh)
    {
        mc.sharedMesh = null;
        if (mesh.triangles.Length == 0) { mc.enabled = false; return; }
        mc.convex = false;
        mc.sharedMesh = mesh;
    }

    /// <summary> A box collider can't have a hole, so it becomes a mesh collider of the same shape first. </summary>
    private static int CarveBox(BoxCollider bc)
    {
        var mesh = Blocks.Box(bc.size, new Tile[6], bc.center);
        mesh.name = "UKCraft box";
        int n = CarveMesh(mesh, bc.transform.localToWorldMatrix, true);
        if (n == 0) { Object.Destroy(mesh); return 0; }

        var mc = bc.gameObject.AddComponent<MeshCollider>();
        mc.sharedMaterial = bc.sharedMaterial;
        owned[mc.GetInstanceID()] = mesh;
        bc.enabled = false;
        Assign(mc, mesh);
        return n;
    }

    #endregion
    #region renderers

    private static int CarveRenderer(MeshRenderer mr)
    {
        if (!mr.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null) return 0;

        int id = mr.GetInstanceID();
        bool batched = mr.isPartOfStaticBatch;
        var matrix = batched ? Matrix4x4.identity : mr.transform.localToWorldMatrix;

        if (owned.TryGetValue(id, out var mesh) && mesh != null) return CarveMesh(mesh, matrix, false);

        mesh = batched ? Extract(mr, mf.sharedMesh) : Editable(mf.sharedMesh);
        if (mesh == null) return 0;

        int n = CarveMesh(mesh, matrix, false);
        if (n == 0) { Object.Destroy(mesh); return 0; }

        owned[id] = mesh;
        var bounds = mr.bounds;
        mf.sharedMesh = mesh;
        if (batched)
        {
            // still drawn in world space with the level's batch material, just from its own mesh now
            StaticSceneOptimizer.SetStaticBatchInfo(mr, 0, 1);
            mr.bounds = bounds;
        }
        return n;
    }

    /// <summary> Pulls the one sub-mesh a static renderer draws out of the baked level mesh, as a mesh of its own. </summary>
    private static Mesh Extract(MeshRenderer mr, Mesh baked)
    {
        if (bakedSubMesh == null)
        {
            bakedSubMesh = new Dictionary<MeshRenderer, int>();
            foreach (var opt in Object.FindObjectsOfType<StaticSceneOptimizer>())
            {
                if (opt.bakedDataAsset == null) continue;
                for (int i = 0; i < opt.staticMRends.Count && i < opt.bakedDataAsset.firstSubMesh.Count; i++)
                    if (opt.staticMRends[i] != null) bakedSubMesh[opt.staticMRends[i]] = opt.bakedDataAsset.firstSubMesh[i];
            }
        }
        if (!bakedSubMesh.TryGetValue(mr, out int sub) || sub < 0 || sub >= baked.subMeshCount) return null;

        if (!bakedCopies.TryGetValue(baked, out var copy))
        {
            copy = baked.isReadable ? baked : Editable(baked);
            bakedCopies[baked] = copy;
        }
        if (copy == null) return null;

        var d = copy.GetSubMesh(sub);
        if (d.topology != MeshTopology.Triangles || d.indexCount == 0) return null;

        if (!bakedData.TryGetValue(baked, out var data))
        {
            data = new MeshData();
            data.Read(copy, false);
            bakedData[baked] = data;
        }
        var indices = copy.GetIndices(sub);

        // keep only the vertices this sub-mesh uses
        var remap = new Dictionary<int, int>();
        var part = new MeshData();
        part.CopyLayout(data);
        var tris = new List<int>(indices.Length);
        foreach (int i in indices)
        {
            if (!remap.TryGetValue(i, out int j))
            {
                j = part.Count;
                remap[i] = j;
                part.Append(data, i);
            }
            tris.Add(j);
        }

        var mesh = new Mesh { name = $"{mr.name} (UKCraft)" };
        part.subMeshes.Add(tris);
        part.Write(mesh);
        return mesh;
    }

    #endregion
    #region readable copies

    /// <summary> A copy of the mesh the mod is allowed to change. Meshes the game stripped from memory are read back from the GPU. </summary>
    private static Mesh Editable(Mesh src)
    {
        if (src.isReadable) return Object.Instantiate(src);
        if (unreadable.Contains(src)) return null;
        try
        {
            var dst = new Mesh { name = src.name + " (UKCraft)", indexFormat = src.indexFormat };
            dst.SetVertexBufferParams(src.vertexCount, src.GetVertexAttributes());
            for (int s = 0; s < src.vertexBufferCount; s++)
            {
                using var vb = src.GetVertexBuffer(s);
                var bytes = new byte[vb.count * vb.stride];
                vb.GetData(bytes);
                dst.SetVertexBufferData(bytes, 0, 0, bytes.Length, s);
            }
            using (var ib = src.GetIndexBuffer())
            {
                dst.SetIndexBufferParams(ib.count, src.indexFormat);
                if (src.indexFormat == IndexFormat.UInt16)
                {
                    var idx = new ushort[ib.count];
                    ib.GetData(idx);
                    dst.SetIndexBufferData(idx, 0, 0, idx.Length);
                }
                else
                {
                    var idx = new uint[ib.count];
                    ib.GetData(idx);
                    dst.SetIndexBufferData(idx, 0, 0, idx.Length);
                }
            }
            dst.subMeshCount = src.subMeshCount;
            for (int i = 0; i < src.subMeshCount; i++) dst.SetSubMesh(i, src.GetSubMesh(i), MeshUpdateFlags.DontRecalculateBounds);
            dst.bounds = src.bounds;
            return dst;
        }
        catch (System.Exception e)
        {
            unreadable.Add(src);
            Plugin.Log.LogWarning($"can't read mesh {src.name}: {e.Message}");
            return null;
        }
    }

    /// <summary> Vertex data of a mesh as plain lists. </summary>
    private class MeshData
    {
        public List<Vector3> pos = new(), normals;
        public List<Vector4> tangents;
        public List<Color> colors;
        public List<Vector4>[] uvs = new List<Vector4>[8];
        public List<List<int>> subMeshes = new();

        public int Count => pos.Count;

        public void Read(Mesh m, bool positionsOnly)
        {
            m.GetVertices(pos);
            if (positionsOnly) return;
            if (m.HasVertexAttribute(VertexAttribute.Normal)) { normals = new(); m.GetNormals(normals); }
            if (m.HasVertexAttribute(VertexAttribute.Tangent)) { tangents = new(); m.GetTangents(tangents); }
            if (m.HasVertexAttribute(VertexAttribute.Color)) { colors = new(); m.GetColors(colors); }
            for (int i = 0; i < 8; i++)
                if (m.HasVertexAttribute(VertexAttribute.TexCoord0 + i)) { uvs[i] = new(); m.GetUVs(i, uvs[i]); }
        }

        public void CopyLayout(MeshData o)
        {
            if (o.normals != null) normals = new();
            if (o.tangents != null) tangents = new();
            if (o.colors != null) colors = new();
            for (int i = 0; i < 8; i++) if (o.uvs[i] != null) uvs[i] = new();
        }

        public void Append(MeshData o, int i)
        {
            pos.Add(o.pos[i]);
            normals?.Add(o.normals[i]);
            tangents?.Add(o.tangents[i]);
            colors?.Add(o.colors[i]);
            for (int k = 0; k < 8; k++) uvs[k]?.Add(o.uvs[k][i]);
        }

        /// <summary> Adds a vertex blended from the three corners of a triangle. </summary>
        public int Blend(int a, int b, int c, Vector3 w)
        {
            pos.Add(pos[a] * w.x + pos[b] * w.y + pos[c] * w.z);
            normals?.Add((normals[a] * w.x + normals[b] * w.y + normals[c] * w.z).normalized);
            tangents?.Add(tangents[a] * w.x + tangents[b] * w.y + tangents[c] * w.z);
            colors?.Add(colors[a] * w.x + colors[b] * w.y + colors[c] * w.z);
            for (int k = 0; k < 8; k++) uvs[k]?.Add(uvs[k][a] * w.x + uvs[k][b] * w.y + uvs[k][c] * w.z);
            return pos.Count - 1;
        }

        public void Write(Mesh m)
        {
            m.Clear();
            m.indexFormat = pos.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.SetVertices(pos);
            if (normals != null) m.SetNormals(normals);
            if (tangents != null) m.SetTangents(tangents);
            if (colors != null) m.SetColors(colors);
            for (int k = 0; k < 8; k++) if (uvs[k] != null) m.SetUVs(k, uvs[k]);
            m.subMeshCount = subMeshes.Count;
            for (int i = 0; i < subMeshes.Count; i++) m.SetTriangles(subMeshes[i], i, false);
            m.RecalculateBounds();
        }
    }

    #endregion
    #region cutting

    /// <summary> Corner of a polygon being cut: where it is in the world and where it is on the original triangle. </summary>
    private struct PV
    {
        public Vector3 p, w;
    }

    private static readonly List<PV> polyA = new(), polyB = new(), polyC = new();
    private static readonly List<List<PV>> kept = new();
    private static readonly Stack<List<PV>> pool = new();

    private static List<PV> Rent()
    {
        var l = pool.Count > 0 ? pool.Pop() : new List<PV>(8);
        l.Clear();
        return l;
    }

    /// <summary> Splits a convex polygon by the plane axis = value into the part below and the part above. </summary>
    private static void Split(List<PV> poly, int axis, float value, List<PV> below, List<PV> above)
    {
        below.Clear(); above.Clear();
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            PV a = poly[i], b = poly[(i + 1) % n];
            float da = a.p[axis] - value, db = b.p[axis] - value;
            if (da >= 0f) above.Add(a); else below.Add(a);
            if ((da >= 0f) != (db >= 0f))
            {
                float t = da / (da - db);
                var m = new PV { p = Vector3.LerpUnclamped(a.p, b.p, t), w = Vector3.LerpUnclamped(a.w, b.w, t) };
                m.p[axis] = value;
                above.Add(m); below.Add(m);
            }
        }
        if (above.Count < 3) above.Clear();
        if (below.Count < 3) below.Clear();
    }

    private static void Keep(List<PV> poly)
    {
        if (poly.Count < 3) return;
        var copy = Rent();
        copy.AddRange(poly);
        kept.Add(copy);
    }

    /// <summary> Slices the polygon along every grid plane of the given axis and the ones after it; returns true if a piece was destroyed. </summary>
    private static bool Slice(List<PV> poly, int axis, Vector3 normal)
    {
        if (poly.Count < 3) return false;
        if (axis == 3)
        {
            var center = Vector3.zero;
            foreach (var v in poly) center += v.p;
            center = center / poly.Count - normal * 0.02f;
            if (cells.Contains(VoxelWorld.CellAt(center))) return true;
            Keep(poly);
            return false;
        }

        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var v in poly) { lo = Mathf.Min(lo, v.p[axis]); hi = Mathf.Max(hi, v.p[axis]); }

        bool removed = false;
        var rest = Rent();
        rest.AddRange(poly);
        var below = Rent();
        var above = Rent();
        for (int k = Mathf.FloorToInt(lo / S) + 1; k * S < hi - 0.0001f && rest.Count >= 3; k++)
        {
            Split(rest, axis, k * S, below, above);
            removed |= Slice(below, axis + 1, normal);
            rest.Clear();
            rest.AddRange(above);
        }
        removed |= Slice(rest, axis + 1, normal);
        pool.Push(rest); pool.Push(below); pool.Push(above);
        return removed;
    }

    /// <summary> Cuts the destroyed cells out of a mesh in place. Returns how many triangles were cut. </summary>
    private static int CarveMesh(Mesh mesh, Matrix4x4 toWorld, bool positionsOnly)
    {
        var data = new MeshData();
        data.Read(mesh, positionsOnly);
        int original = data.Count;

        var world = new Vector3[original];
        for (int i = 0; i < original; i++) world[i] = toWorld.MultiplyPoint3x4(data.pos[i]);

        int cut = 0;
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            if (mesh.GetTopology(s) != MeshTopology.Triangles) { data.subMeshes.Add(new List<int>(mesh.GetIndices(s))); continue; }

            var src = mesh.GetIndices(s);
            var dst = new List<int>(src.Length);
            for (int i = 0; i + 2 < src.Length; i += 3)
            {
                int a = src[i], b = src[i + 1], c = src[i + 2];
                Vector3 pa = world[a], pb = world[b], pc = world[c];

                if (Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x)) < bmin.x || Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x)) > bmax.x ||
                    Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y)) < bmin.y || Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y)) > bmax.y ||
                    Mathf.Max(pa.z, Mathf.Max(pb.z, pc.z)) < bmin.z || Mathf.Min(pa.z, Mathf.Min(pb.z, pc.z)) > bmax.z)
                {
                    dst.Add(a); dst.Add(b); dst.Add(c);
                    continue;
                }

                var normal = Vector3.Cross(pb - pa, pc - pa).normalized;
                foreach (var l in kept) pool.Push(l);
                kept.Clear();

                // peel off whatever sticks out of the box around the destroyed cells
                polyA.Clear();
                polyA.Add(new PV { p = pa, w = new Vector3(1, 0, 0) });
                polyA.Add(new PV { p = pb, w = new Vector3(0, 1, 0) });
                polyA.Add(new PV { p = pc, w = new Vector3(0, 0, 1) });
                for (int axis = 0; axis < 3 && polyA.Count >= 3; axis++)
                {
                    Split(polyA, axis, bmin[axis], polyB, polyC);
                    Keep(polyB);
                    Split(polyC, axis, bmax[axis], polyA, polyB);
                    Keep(polyB);
                }

                if (polyA.Count < 3 || !Slice(polyA, 0, normal))
                {
                    dst.Add(a); dst.Add(b); dst.Add(c);
                    continue;
                }

                cut++;
                foreach (var poly in kept)
                {
                    int first = data.Blend(a, b, c, poly[0].w);
                    int prev = data.Blend(a, b, c, poly[1].w);
                    for (int k = 2; k < poly.Count; k++)
                    {
                        int next = data.Blend(a, b, c, poly[k].w);
                        dst.Add(first); dst.Add(prev); dst.Add(next);
                        prev = next;
                    }
                }
            }
            data.subMeshes.Add(dst);
        }

        if (cut > 0) data.Write(mesh);
        return cut;
    }

    #endregion
}
