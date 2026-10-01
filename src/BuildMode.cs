namespace UKCraft;

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary> Build mode: hotbar, block placing and breaking, flint and steel, spawn egg. </summary>
public class BuildMode : MonoBehaviour
{
    public static BuildMode I;

    public enum Kind { Block, Flint, Egg }

    public struct Item
    {
        public string name;
        public Kind kind;
        public BlockId block;
        public Tile icon;
    }

    public const int PAGE = 9;

    /// <summary> Everything the hotbar offers. The first page holds the basics, TNT and the tools. </summary>
    public static readonly Item[] Items = BuildItems();

    private static Item[] BuildItems()
    {
        var list = new List<Item>();
        var first = new[] { BlockId.Grass, BlockId.Dirt, BlockId.Stone, BlockId.Cobble, BlockId.Planks, BlockId.Log, BlockId.Tnt };
        foreach (var id in first) list.Add(Block(id));
        list.Add(new Item { name = "Flint and Steel", kind = Kind.Flint, icon = Tile.FlintSteel });
        list.Add(new Item { name = "Creeper Egg", kind = Kind.Egg, icon = Tile.Egg });
        for (var id = BlockId.Air + 1; id < BlockId.Count; id++)
            if (System.Array.IndexOf(first, id) < 0) list.Add(Block(id));
        return list.ToArray();
    }

    private static Item Block(BlockId id) => new() { name = Blocks.Name(id), kind = Kind.Block, block = id, icon = Blocks.Icon(id) };

    public static int Pages => (Items.Length + PAGE - 1) / PAGE;

    public bool Active { get; private set; }
    public int Slot;

    private float nextBreak, nextPlace;
    private bool hinted;
    private LineRenderer outline;

    private void Awake() => I = this;

    #region enter / leave

    public void Enter()
    {
        if (Active || !Game.Playing) return;
        Active = true;
        Blocks.Build();
        try
        {
            MonoSingleton<GunControl>.Instance?.NoWeapon();
            MonoSingleton<FistControl>.Instance?.NoFist();
        }
        catch (System.Exception e) { Plugin.Log.LogWarning($"holster failed: {e.Message}"); }
    }

    public void Leave(bool restore = true)
    {
        if (!Active) return;
        Active = false;
        if (outline != null) outline.enabled = false;
        if (!restore) return;
        try
        {
            MonoSingleton<GunControl>.Instance?.YesWeapon();
            MonoSingleton<FistControl>.Instance?.YesFist();
        }
        catch (System.Exception e) { Plugin.Log.LogWarning($"unholster failed: {e.Message}"); }
    }

    #endregion
    #region targeting

    public struct Aim
    {
        public RaycastHit hit;
        public Vector3Int inside, outside;
        public bool placed;
        public TntMarker tnt;
    }

    /// <summary> What the crosshair points at within reach: the cell behind the surface and the one in front of it. </summary>
    public static bool Look(out Aim aim)
    {
        aim = default;
        var cc = Game.Cam;
        if (cc == null || cc.cam == null) return false;

        var t = cc.cam.transform;
        if (!Physics.Raycast(t.position, t.forward, out var hit, Plugin.Reach.Value, Game.EnvMask, QueryTriggerInteraction.Ignore)) return false;

        aim.hit = hit;
        aim.tnt = hit.collider.GetComponent<TntMarker>();
        aim.placed = aim.tnt != null || hit.collider.GetComponent<ChunkTag>() != null;
        aim.inside = aim.tnt != null ? aim.tnt.cell : VoxelWorld.CellAt(hit.point - hit.normal * 0.05f);
        aim.outside = aim.tnt != null ? aim.tnt.cell + Vector3Int.RoundToInt(hit.normal) : VoxelWorld.CellAt(hit.point + hit.normal * 0.05f);
        return true;
    }

    #endregion
    #region actions

    public void Break(Aim aim)
    {
        if (aim.placed) VoxelWorld.I.Set(aim.inside, BlockId.Air);
        else if (Plugin.MineLevel.Value) LevelCarver.Carve(new HashSet<Vector3Int> { aim.inside });
    }

    public void Use(Aim aim)
    {
        var item = Items[Slot];
        switch (item.kind)
        {
            case Kind.Block:
                if (VoxelWorld.I.Get(aim.outside) != BlockId.Air) return;
                var p = Game.Player;
                var cell = VoxelWorld.CellBounds(aim.outside);
                cell.Expand(-0.1f);
                if (p != null && p.playerCollider != null && p.playerCollider.bounds.Intersects(cell)) return;
                VoxelWorld.I.Set(aim.outside, item.block);
                break;

            case Kind.Flint:
                if (aim.tnt != null) PrimedTnt.Prime(aim.tnt.cell, Plugin.TntFuse.Value);
                break;

            case Kind.Egg:
                Creeper.Spawn(aim.hit.point + aim.hit.normal * 0.2f);
                break;
        }
    }

    #endregion
    #region update

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (!Game.Playing)
        {
            if (outline != null) outline.enabled = false;
            return;
        }

        if (!hinted)
        {
            hinted = true;
            Game.Hud($"UKCRAFT: press <color=orange>{Plugin.BuildKey.Value}</color> to build");
        }

        if (kb[Plugin.BuildKey.Value].wasPressedThisFrame)
        {
            if (Active) Leave(); else Enter();
        }
        if (!Active) return;

        // slot selection: scroll through everything, 1-9 within the page, Q/E to flip pages
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll > 0f) Slot = (Slot + Items.Length - 1) % Items.Length;
        if (scroll < 0f) Slot = (Slot + 1) % Items.Length;
        int page = Slot / PAGE;
        for (int i = 0; i < PAGE; i++)
            if (kb[Key.Digit1 + i].wasPressedThisFrame && page * PAGE + i < Items.Length) Slot = page * PAGE + i;
        if (kb.qKey.wasPressedThisFrame) Slot = Mathf.Min((page + Pages - 1) % Pages * PAGE + Slot % PAGE, Items.Length - 1);
        if (kb.eKey.wasPressedThisFrame) Slot = Mathf.Min((page + 1) % Pages * PAGE + Slot % PAGE, Items.Length - 1);

        bool aiming = Look(out var aim);
        ShowOutline(aiming, aim.inside);
        if (!aiming) return;

        if (mouse.leftButton.wasPressedThisFrame || (mouse.leftButton.isPressed && Time.time >= nextBreak))
        {
            nextBreak = Time.time + 0.22f;
            Break(aim);
        }
        else if (mouse.rightButton.wasPressedThisFrame || (mouse.rightButton.isPressed && Time.time >= nextPlace && Items[Slot].kind == Kind.Block))
        {
            nextPlace = Time.time + 0.2f;
            Use(aim);
        }
    }

    private static readonly Vector3[] edges =
    {
        new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1), new(0, 0, 0), new(0, 1, 0), new(1, 1, 0), new(1, 0, 0),
        new(1, 1, 0), new(1, 1, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1), new(0, 0, 1), new(0, 1, 1), new(0, 1, 0),
    };

    private void ShowOutline(bool show, Vector3Int cell)
    {
        if (outline == null)
        {
            var go = new GameObject("UKCraft outline") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            outline = go.AddComponent<LineRenderer>();
            outline.sharedMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            outline.startColor = outline.endColor = Color.black;
            outline.positionCount = edges.Length;
            outline.useWorldSpace = true;
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        outline.enabled = show;
        if (!show) return;

        float s = VoxelWorld.S;
        outline.startWidth = outline.endWidth = s * 0.03f;
        var center = new Vector3(0.5f, 0.5f, 0.5f);
        for (int i = 0; i < edges.Length; i++)
            outline.SetPosition(i, ((Vector3)cell + center + (edges[i] - center) * 1.01f) * s);
    }

    #endregion
    #region hud

    private GUIStyle label, small;

    private void OnGUI()
    {
        if (!Active || !Game.Playing || Blocks.Atlas == null) return;

        float k = Screen.height / 1080f;
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        }
        label.fontSize = Mathf.RoundToInt(22 * k);
        small.fontSize = Mathf.RoundToInt(14 * k);

        float slot = 58 * k, pad = 5 * k, total = slot * PAGE;
        float x0 = (Screen.width - total) / 2f, y = Screen.height - slot - 36 * k;
        int page = Slot / PAGE;

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x0 - pad, y - pad, total + pad * 2, slot + pad * 2), Texture2D.whiteTexture);

        for (int i = 0; i < PAGE; i++)
        {
            int item = page * PAGE + i;
            var r = new Rect(x0 + i * slot, y, slot, slot);
            GUI.color = item == Slot ? Color.white : new Color(1f, 1f, 1f, 0.18f);
            GUI.DrawTexture(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), Texture2D.whiteTexture);
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            float b = item == Slot ? 4 * k : 2 * k;
            GUI.DrawTexture(new Rect(r.x + b, r.y + b, r.width - b * 2, r.height - b * 2), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (item < Items.Length)
                GUI.DrawTextureWithTexCoords(new Rect(r.x + 9 * k, r.y + 9 * k, r.width - 18 * k, r.height - 18 * k), Blocks.Atlas, Blocks.UVRect(Items[item].icon));
        }

        Shadowed(new Rect(0, y - 44 * k, Screen.width, 34 * k), $"{Items[Slot].name}   <size={Mathf.RoundToInt(14 * k)}>{page + 1}/{Pages}</size>", label);
        Shadowed(new Rect(0, y + slot + 6 * k, Screen.width, 24 * k),
            $"LMB break   RMB {(Items[Slot].kind == Kind.Block ? "place" : "use")}   SCROLL / 1-9 select   Q E page   {Plugin.BuildKey.Value} weapons", small);
    }

    private static void Shadowed(Rect r, string text, GUIStyle style)
    {
        GUI.color = Color.black;
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
        GUI.color = Color.white;
        GUI.Label(r, text, style);
    }

    #endregion
}
