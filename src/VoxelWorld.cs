namespace UKCraft;

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary> Marks a chunk collider so raycasts can tell placed blocks from the level. </summary>
public class ChunkTag : MonoBehaviour { }

/// <summary> Sits on the collider of a TNT block. The Breakable next to it makes every weapon in the game able to set it off. </summary>
public class TntMarker : MonoBehaviour
{
    public Vector3Int cell;
}

/// <summary> Sparse grid of placed blocks, stored and meshed in 16x16x16 chunks. </summary>
public class VoxelWorld : MonoBehaviour
{
    public static VoxelWorld I;

    public const int CS = 16;
    public const int ENV_LAYER = 8;

    public static float S => Plugin.BlockSize.Value;

    private class Chunk
    {
        public Vector3Int key;
        public byte[] blocks = new byte[CS * CS * CS];
        public int count;
        public bool dirty;
        public GameObject go;
        public MeshFilter mf;
        public MeshCollider mc;
        public Mesh visual, collision;
    }

    private readonly Dictionary<Vector3Int, Chunk> chunks = new();
    private readonly Dictionary<Vector3Int, GameObject> tnts = new();
    private Transform root;

    public int BlockCount { get; private set; }

    private void Awake() => I = this;

    #region coordinates

    public static Vector3Int CellAt(Vector3 p) => new(Mathf.FloorToInt(p.x / S), Mathf.FloorToInt(p.y / S), Mathf.FloorToInt(p.z / S));

    public static Vector3 CellCenter(Vector3Int c) => new Vector3(c.x + 0.5f, c.y + 0.5f, c.z + 0.5f) * S;

    public static Bounds CellBounds(Vector3Int c) => new(CellCenter(c), Vector3.one * S);

    private static int Div(int a) => a >= 0 ? a / CS : (a - CS + 1) / CS;

    private static Vector3Int Key(Vector3Int c) => new(Div(c.x), Div(c.y), Div(c.z));

    private static int Index(Vector3Int c, Vector3Int key) => (c.x - key.x * CS) + (c.y - key.y * CS) * CS + (c.z - key.z * CS) * CS * CS;

    #endregion
    #region access

    public BlockId Get(Vector3Int c)
    {
        var key = Key(c);
        return chunks.TryGetValue(key, out var ch) ? (BlockId)ch.blocks[Index(c, key)] : BlockId.Air;
    }

    public void Set(Vector3Int c, BlockId id)
    {
        var key = Key(c);
        if (!chunks.TryGetValue(key, out var ch))
        {
            if (id == BlockId.Air) return;
            ch = NewChunk(key);
        }

        int i = Index(c, key);
        var old = (BlockId)ch.blocks[i];
        if (old == id) return;

        ch.blocks[i] = (byte)id;
        if (old == BlockId.Air) { ch.count++; BlockCount++; }
        if (id == BlockId.Air) { ch.count--; BlockCount--; }
        ch.dirty = true;

        if (old == BlockId.Tnt && tnts.TryGetValue(c, out var marker))
        {
            tnts.Remove(c);
            if (marker != null) Destroy(marker);
        }
        if (id == BlockId.Tnt) tnts[c] = NewTntCollider(c);

        // faces of the neighbours across a chunk border change too
        foreach (var d in Blocks.Dirs)
        {
            var nk = Key(c + d);
            if (nk != key && chunks.TryGetValue(nk, out var n)) n.dirty = true;
        }
    }

    public void Clear()
    {
        foreach (var ch in chunks.Values)
        {
            if (ch.go != null) Destroy(ch.go);
            if (ch.visual != null) Destroy(ch.visual);
            if (ch.collision != null) Destroy(ch.collision);
        }
        chunks.Clear();
        tnts.Clear();
        BlockCount = 0;
        if (root != null) Destroy(root.gameObject);
        root = null;
    }

    private Transform Root
    {
        get
        {
            if (root == null) root = new GameObject("UKCraft world").transform;
            return root;
        }
    }

    private Chunk NewChunk(Vector3Int key)
    {
        var go = new GameObject($"UKCraft chunk {key.x} {key.y} {key.z}") { layer = ENV_LAYER };
        go.transform.SetParent(Root, false);
        go.transform.position = (Vector3)key * (CS * S);

        var ch = new Chunk { key = key, go = go };
        ch.mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Blocks.BlockMaterial;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        ch.mc = go.AddComponent<MeshCollider>();
        go.AddComponent<ChunkTag>();

        ch.visual = new Mesh { name = "UKCraft chunk", indexFormat = IndexFormat.UInt32 };
        ch.collision = new Mesh { name = "UKCraft chunk collision", indexFormat = IndexFormat.UInt32 };
        ch.mf.sharedMesh = ch.visual;

        chunks[key] = ch;
        return ch;
    }

    private GameObject NewTntCollider(Vector3Int c)
    {
        var go = new GameObject("UKCraft TNT") { layer = ENV_LAYER };
        go.transform.SetParent(Root, false);
        go.transform.position = CellCenter(c);
        go.AddComponent<BoxCollider>().size = Vector3.one * S;
        go.AddComponent<TntMarker>().cell = c;

        var b = go.AddComponent<Breakable>();
        b.weak = true;
        b.activateOnBreak = new GameObject[0];
        b.destroyOnBreak = new GameObject[0];
        b.destroyEvent = new UltrakillEvent();
        return go;
    }

    #endregion
    #region meshing

    private readonly List<Vector3> v = new(), n = new(), cv = new();
    private readonly List<Vector2> uv = new();
    private readonly List<Color> col = new();
    private readonly List<int> tri = new(), ctri = new();

    private void LateUpdate()
    {
        foreach (var ch in chunks.Values) if (ch.dirty) Rebuild(ch);
    }

    private void Rebuild(Chunk ch)
    {
        ch.dirty = false;
        v.Clear(); n.Clear(); uv.Clear(); col.Clear(); tri.Clear(); cv.Clear(); ctri.Clear();

        if (ch.count > 0)
        {
            var origin = ch.key * CS;
            for (int z = 0; z < CS; z++) for (int y = 0; y < CS; y++) for (int x = 0; x < CS; x++)
            {
                var id = (BlockId)ch.blocks[x + y * CS + z * CS * CS];
                if (id == BlockId.Air) continue;

                var local = new Vector3Int(x, y, z);
                for (int f = 0; f < 6; f++)
                {
                    var other = Get(origin + local + Blocks.Dirs[f]);
                    if (other != BlockId.Air) continue;

                    var r = Blocks.UV(Blocks.Face(id, f));
                    int b = v.Count;
                    for (int i = 0; i < 4; i++)
                    {
                        v.Add((Blocks.Corners[f][i] + local) * S);
                        n.Add(Blocks.Dirs[f]);
                        col.Add(new Color(Blocks.Shade[f], Blocks.Shade[f], Blocks.Shade[f], 1f));
                    }
                    uv.Add(new Vector2(r.x, r.y)); uv.Add(new Vector2(r.x, r.w)); uv.Add(new Vector2(r.z, r.w)); uv.Add(new Vector2(r.z, r.y));
                    tri.Add(b); tri.Add(b + 1); tri.Add(b + 2); tri.Add(b); tri.Add(b + 2); tri.Add(b + 3);

                    // TNT has its own box collider, so weapons hit that instead of the chunk
                    if (id == BlockId.Tnt) continue;
                    b = cv.Count;
                    for (int i = 0; i < 4; i++) cv.Add((Blocks.Corners[f][i] + local) * S);
                    ctri.Add(b); ctri.Add(b + 1); ctri.Add(b + 2); ctri.Add(b); ctri.Add(b + 2); ctri.Add(b + 3);
                }
            }
        }

        ch.visual.Clear();
        ch.visual.SetVertices(v);
        ch.visual.SetNormals(n);
        ch.visual.SetUVs(0, uv);
        ch.visual.SetColors(col);
        ch.visual.SetTriangles(tri, 0);
        ch.visual.RecalculateBounds();

        ch.mc.sharedMesh = null;
        ch.collision.Clear();
        if (ctri.Count > 0)
        {
            ch.collision.SetVertices(cv);
            ch.collision.SetTriangles(ctri, 0);
            ch.collision.RecalculateBounds();
            ch.mc.sharedMesh = ch.collision;
        }
    }

    #endregion
}
