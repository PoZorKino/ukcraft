namespace UKCraft;

using HarmonyLib;
using UnityEngine;

/// <summary>
/// The creeper. Underneath it is one of the game's own Filth, so it takes damage from every weapon, gives style and finds its way
/// through levels; the body is hidden and replaced with a box model, and the bite is replaced with a fuse.
/// </summary>
public class Creeper : MonoBehaviour
{
    public const float FUSE = 1.5f;

    private static Mesh head, body, leg;

    private EnemyIdentifier eid;
    private Enemy mach;
    private Transform model, headT;
    private readonly Transform[] legs = new Transform[4];
    private readonly MeshRenderer[] parts = new MeshRenderer[6];

    private float fuse, walk, baseSpeed = -1f;
    private bool fusing, done;

    public static Creeper Spawn(Vector3 pos)
    {
        var refs = MonoSingleton<DefaultReferenceManager>.Instance;
        if (refs == null || refs.filth == null)
        {
            Plugin.Log.LogWarning("no filth prefab to build a creeper from");
            return null;
        }

        var rot = Quaternion.identity;
        var p = Game.Player;
        if (p != null)
        {
            var to = p.transform.position - pos;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) rot = Quaternion.LookRotation(to);
        }

        var go = Instantiate(refs.filth, pos, rot);
        go.name = "UKCraft Creeper";
        return go.AddComponent<Creeper>();
    }

    private void Start()
    {
        eid = GetComponent<EnemyIdentifier>();
        mach = GetComponent<Enemy>();
        if (TryGetComponent<ZombieMelee>(out var melee)) melee.harmless = true;

        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = true;
        BuildModel();
    }

    /// <summary> Size of one Minecraft pixel in the local space of the Filth underneath. </summary>
    private float Pixel => VoxelWorld.S / 16f / Mathf.Max(0.01f, transform.lossyScale.y);

    private void BuildModel()
    {
        Blocks.Build();
        if (head == null)
        {
            var skin = new[] { Tile.CreeperSkin, Tile.CreeperSkin, Tile.CreeperSkin, Tile.CreeperSkin, Tile.CreeperSkin, Tile.CreeperSkin };
            var face = (Tile[])skin.Clone();
            face[4] = Tile.CreeperFace;
            head = Blocks.Box(new Vector3(8, 8, 8), face);
            body = Blocks.Box(new Vector3(8, 12, 4), skin);
            leg = Blocks.Box(new Vector3(4, 6, 4), skin, new Vector3(0, -3, 0));
            DontDestroyOnLoad(head); DontDestroyOnLoad(body); DontDestroyOnLoad(leg);
        }

        model = new GameObject("creeper model").transform;
        model.SetParent(transform, false);
        model.localScale = Vector3.one * Pixel;

        Part(0, "body", body, new Vector3(0, 12, 0));
        headT = Part(1, "head", head, new Vector3(0, 22, 0));
        legs[0] = Part(2, "leg", leg, new Vector3(-2, 6, 4));
        legs[1] = Part(3, "leg", leg, new Vector3(2, 6, 4));
        legs[2] = Part(4, "leg", leg, new Vector3(-2, 6, -4));
        legs[3] = Part(5, "leg", leg, new Vector3(2, 6, -4));
    }

    private Transform Part(int i, string name, Mesh mesh, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(model, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        parts[i] = go.AddComponent<MeshRenderer>();
        parts[i].sharedMaterial = Blocks.BlockMaterial;
        return go.transform;
    }

    private void Update()
    {
        if (done || eid == null || model == null) return;
        if (eid.dead)
        {
            // shot dead: no explosion, just like the real thing
            done = true;
            Destroy(model.gameObject);
            return;
        }

        var p = Game.Player;
        float s = VoxelWorld.S, dist = p == null ? float.MaxValue : Vector3.Distance(p.transform.position, transform.position);

        // starts hissing within 3 blocks, gives up beyond 7
        if (!fusing && dist < s * 3f) { fusing = true; Sfx.Play(gameObject, PrimedTnt.Hiss, 0.9f); }
        if (fusing && dist > s * 7f) fusing = false;
        fuse = Mathf.Clamp(fuse + (fusing ? Time.deltaTime : -Time.deltaTime), 0f, FUSE);

        var nma = mach != null ? mach.nma : null;
        if (nma != null)
        {
            if (baseSpeed < 0f && nma.speed > 0f) baseSpeed = nma.speed;
            if (fusing) { nma.speed = 0f; if (nma.enabled) nma.velocity = Vector3.zero; }
            else if (baseSpeed > 0f && nma.speed == 0f) nma.speed = baseSpeed;

            float speed = nma.enabled ? nma.velocity.magnitude : 0f;
            walk += Time.deltaTime * Mathf.Min(speed, 12f) * 1.2f;
            float swing = Mathf.Sin(walk) * 30f * Mathf.Clamp01(speed / 2f);
            legs[0].localRotation = legs[3].localRotation = Quaternion.Euler(swing, 0, 0);
            legs[1].localRotation = legs[2].localRotation = Quaternion.Euler(-swing, 0, 0);
        }
        if (p != null)
        {
            var eye = Game.Cam != null ? Game.Cam.transform.position : p.transform.position;
            var to = eye - headT.position;
            if (to.sqrMagnitude > 0.01f) headT.rotation = Quaternion.Slerp(headT.rotation, Quaternion.LookRotation(to), Time.deltaTime * 8f);
        }

        // flash and swell while the fuse burns
        bool white = fuse > 0f && fuse % 0.3f < 0.15f;
        foreach (var r in parts) if (r != null) r.sharedMaterial = white ? Blocks.FlashMaterial : Blocks.BlockMaterial;
        model.localScale = Vector3.one * (Pixel * (1f + 0.25f * fuse / FUSE));

        if (fuse >= FUSE)
        {
            done = true;
            var pos = transform.position + Vector3.up * (s * 0.8f);
            Destroy(gameObject);
            Destruction.Explode(pos, Plugin.CreeperRadius.Value, false);
        }
    }
}

/// <summary> A creeper never bites or dives: the Filth underneath only walks. </summary>
[HarmonyPatch(typeof(ZombieMelee))]
public static class CreeperPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("DiveCheck")]
    [HarmonyPatch(nameof(ZombieMelee.JumpAttack))]
    [HarmonyPatch(nameof(ZombieMelee.Swing))]
    private static bool NoAttack(ZombieMelee __instance) => __instance.GetComponent<Creeper>() == null;
}
