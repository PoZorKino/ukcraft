namespace UKCraft;

using UnityEngine;

public enum BlockId : byte
{
    Air, Grass, Dirt, Stone, Cobble, Planks, Log, Leaves, Sand, Gravel, Glass, Bricks, StoneBricks, Sandstone, Snow, Ice,
    CoalOre, IronOre, GoldOre, DiamondOre, IronBlock, GoldBlock, DiamondBlock, Bookshelf, Netherrack, Glowstone, Obsidian, Bedrock,
    WoolWhite, WoolRed, WoolOrange, WoolYellow, WoolGreen, WoolBlue, WoolPurple, WoolBlack, Tnt, Count
}

public enum Tile
{
    GrassTop, GrassSide, Dirt, Stone, Cobble, Planks, LogSide, LogTop, Leaves, Sand, Gravel, Glass, Bricks, StoneBricks,
    SandstoneSide, SandstoneTop, Snow, Ice, CoalOre, IronOre, GoldOre, DiamondOre, IronBlock, GoldBlock, DiamondBlock,
    Bookshelf, Netherrack, Glowstone, Obsidian, Bedrock,
    WoolWhite, WoolRed, WoolOrange, WoolYellow, WoolGreen, WoolBlue, WoolPurple, WoolBlack,
    TntSide, TntTop, TntBottom, CreeperSkin, CreeperFace, FlintSteel, Egg, White, Count
}

/// <summary> Block definitions and the texture atlas. Every texture is drawn here in code: no Minecraft files are used. </summary>
public static class Blocks
{
    public const int TILE = 16, PER_ROW = 8;

    public static Texture2D Atlas { get; private set; }

    private struct Def
    {
        public string name;
        public Tile top, bottom, side;
        public bool blastProof;
    }

    private static readonly Def[] defs = new Def[(int)BlockId.Count];

    private static void D(BlockId id, string name, Tile side, Tile? top = null, Tile? bottom = null, bool blastProof = false) =>
        defs[(int)id] = new Def { name = name, side = side, top = top ?? side, bottom = bottom ?? top ?? side, blastProof = blastProof };

    static Blocks()
    {
        D(BlockId.Air, "Air", Tile.White);
        D(BlockId.Grass, "Grass", Tile.GrassSide, Tile.GrassTop, Tile.Dirt);
        D(BlockId.Dirt, "Dirt", Tile.Dirt);
        D(BlockId.Stone, "Stone", Tile.Stone);
        D(BlockId.Cobble, "Cobblestone", Tile.Cobble);
        D(BlockId.Planks, "Planks", Tile.Planks);
        D(BlockId.Log, "Log", Tile.LogSide, Tile.LogTop);
        D(BlockId.Leaves, "Leaves", Tile.Leaves);
        D(BlockId.Sand, "Sand", Tile.Sand);
        D(BlockId.Gravel, "Gravel", Tile.Gravel);
        D(BlockId.Glass, "Glass", Tile.Glass);
        D(BlockId.Bricks, "Bricks", Tile.Bricks);
        D(BlockId.StoneBricks, "Stone Bricks", Tile.StoneBricks);
        D(BlockId.Sandstone, "Sandstone", Tile.SandstoneSide, Tile.SandstoneTop);
        D(BlockId.Snow, "Snow", Tile.Snow);
        D(BlockId.Ice, "Ice", Tile.Ice);
        D(BlockId.CoalOre, "Coal Ore", Tile.CoalOre);
        D(BlockId.IronOre, "Iron Ore", Tile.IronOre);
        D(BlockId.GoldOre, "Gold Ore", Tile.GoldOre);
        D(BlockId.DiamondOre, "Diamond Ore", Tile.DiamondOre);
        D(BlockId.IronBlock, "Iron Block", Tile.IronBlock);
        D(BlockId.GoldBlock, "Gold Block", Tile.GoldBlock);
        D(BlockId.DiamondBlock, "Diamond Block", Tile.DiamondBlock);
        D(BlockId.Bookshelf, "Bookshelf", Tile.Bookshelf, Tile.Planks);
        D(BlockId.Netherrack, "Netherrack", Tile.Netherrack);
        D(BlockId.Glowstone, "Glowstone", Tile.Glowstone);
        D(BlockId.Obsidian, "Obsidian", Tile.Obsidian, blastProof: true);
        D(BlockId.Bedrock, "Bedrock", Tile.Bedrock, blastProof: true);
        D(BlockId.WoolWhite, "White Wool", Tile.WoolWhite);
        D(BlockId.WoolRed, "Red Wool", Tile.WoolRed);
        D(BlockId.WoolOrange, "Orange Wool", Tile.WoolOrange);
        D(BlockId.WoolYellow, "Yellow Wool", Tile.WoolYellow);
        D(BlockId.WoolGreen, "Green Wool", Tile.WoolGreen);
        D(BlockId.WoolBlue, "Blue Wool", Tile.WoolBlue);
        D(BlockId.WoolPurple, "Purple Wool", Tile.WoolPurple);
        D(BlockId.WoolBlack, "Black Wool", Tile.WoolBlack);
        D(BlockId.Tnt, "TNT", Tile.TntSide, Tile.TntTop, Tile.TntBottom);
    }

    public static string Name(BlockId id) => defs[(int)id].name;

    /// <summary> Tile shown in the hotbar. </summary>
    public static Tile Icon(BlockId id) => defs[(int)id].side;

    /// <summary> Blocks an explosion can't remove. </summary>
    public static bool BlastProof(BlockId id) => defs[(int)id].blastProof;

    /// <summary> Face order: +X, -X, +Y, -Y, +Z, -Z. </summary>
    public static Tile Face(BlockId id, int face) => face == 2 ? defs[(int)id].top : face == 3 ? defs[(int)id].bottom : defs[(int)id].side;

    /// <summary> UV rectangle of a tile: (uMin, vMin, uMax, vMax). </summary>
    public static Vector4 UV(Tile t)
    {
        const float inset = 0.02f / (TILE * PER_ROW);
        int col = (int)t % PER_ROW, row = (int)t / PER_ROW;
        float u = col / (float)PER_ROW, v = 1f - (row + 1) / (float)PER_ROW, s = 1f / PER_ROW;
        return new Vector4(u + inset, v + inset, u + s - inset, v + s - inset);
    }

    public static Rect UVRect(Tile t)
    {
        var r = UV(t);
        return new Rect(r.x, r.y, r.z - r.x, r.w - r.y);
    }

    #region atlas

    private static System.Random rng;
    private static Color32[] px;

    private static int R(int n) => rng.Next(n);

    private static Color32 C(int r, int g, int b, int a = 255) => new((byte)Mathf.Clamp(r, 0, 255), (byte)Mathf.Clamp(g, 0, 255), (byte)Mathf.Clamp(b, 0, 255), (byte)a);

    /// <summary> Shade of a base color, jittered by up to +-j. </summary>
    private static Color32 J(int r, int g, int b, int j)
    {
        int d = R(j * 2 + 1) - j;
        return C(r + d, g + d, b + d);
    }

    /// <summary> Sets a pixel of a tile; y = 0 is the top row. </summary>
    private static void P(Tile t, int x, int y, Color32 c)
    {
        int size = TILE * PER_ROW, col = (int)t % PER_ROW, row = (int)t / PER_ROW;
        px[(size - 1 - (row * TILE + y)) * size + col * TILE + x] = c;
    }

    private static Color32 G(Tile t, int x, int y)
    {
        int size = TILE * PER_ROW, col = (int)t % PER_ROW, row = (int)t / PER_ROW;
        return px[(size - 1 - (row * TILE + y)) * size + col * TILE + x];
    }

    private delegate Color32 Painter(int x, int y);

    private static void Fill(Tile t, Painter f)
    {
        for (int y = 0; y < TILE; y++) for (int x = 0; x < TILE; x++) P(t, x, y, f(x, y));
    }

    /// <summary> Stone with a handful of ore nuggets in it. </summary>
    private static void Ore(Tile t, int r, int g, int b)
    {
        Fill(t, (x, y) => G(Tile.Stone, x, y));
        int[,] spots = { { 3, 3 }, { 10, 2 }, { 6, 8 }, { 12, 10 }, { 3, 12 }, { 9, 13 } };
        for (int i = 0; i < spots.GetLength(0); i++)
        {
            int sx = spots[i, 0], sy = spots[i, 1];
            P(t, sx, sy, J(r, g, b, 8)); P(t, sx + 1, sy, J(r, g, b, 8)); P(t, sx, sy + 1, J(r, g, b, 8));
            if (i % 2 == 0) P(t, sx + 1, sy + 1, J(r, g, b, 8));
            P(t, sx + 2, sy + 1, J(r - 50, g - 50, b - 50, 4));
        }
    }

    /// <summary> Beveled block of metal or gems. </summary>
    private static void Metal(Tile t, int r, int g, int b) => Fill(t, (x, y) =>
    {
        if (x == 0 || y == 0) return C(r + 28, g + 28, b + 28);
        if (x == TILE - 1 || y == TILE - 1) return C(r - 46, g - 46, b - 46);
        return y % 5 == 4 ? J(r - 18, g - 18, b - 18, 3) : J(r, g, b, 5);
    });

    private static void Wool(Tile t, int r, int g, int b) => Fill(t, (x, y) =>
    {
        int weave = (x + y * 2) % 4 == 0 ? -12 : (x * 3 + y) % 5 == 0 ? 8 : 0;
        return J(r + weave, g + weave, b + weave, 4);
    });

    public static void Build()
    {
        if (Atlas != null) return;

        int size = TILE * PER_ROW;
        rng = new System.Random(1337);
        px = new Color32[size * size];

        Fill(Tile.Dirt, (x, y) => R(9) == 0 ? J(96, 67, 45, 6) : R(7) == 0 ? J(160, 118, 84, 6) : J(134, 96, 67, 10));
        Fill(Tile.GrassTop, (x, y) => R(6) == 0 ? J(84, 150, 58, 8) : J(106, 172, 70, 12));
        Fill(Tile.GrassSide, (x, y) =>
        {
            int edge = 3 + ((x * 7 + 3) % 5 == 0 ? 2 : (x * 3) % 4 == 0 ? 1 : 0);
            return y < edge ? J(100, 166, 66, 12) : G(Tile.Dirt, x, y);
        });
        Fill(Tile.Stone, (x, y) => J(126, 126, 126, 6));
        for (int i = 0; i < 9; i++)
        {
            int bx = R(TILE), by = R(TILE), len = 2 + R(4), sh = R(2) == 0 ? 108 : 140;
            for (int k = 0; k < len; k++) P(Tile.Stone, (bx + k) % TILE, by, J(sh, sh, sh, 4));
        }

        // cobblestone: random cell centers, dark seams where two cells meet
        var pts = new Vector2Int[10];
        var shade = new int[10];
        for (int i = 0; i < pts.Length; i++) { pts[i] = new Vector2Int(R(TILE), R(TILE)); shade[i] = 105 + R(50); }
        Fill(Tile.Cobble, (x, y) =>
        {
            int best = 0, bd = 999, sd = 999;
            for (int i = 0; i < pts.Length; i++)
            {
                int dx = Mathf.Abs(x - pts[i].x), dy = Mathf.Abs(y - pts[i].y);
                dx = Mathf.Min(dx, TILE - dx); dy = Mathf.Min(dy, TILE - dy);
                int d = dx * dx + dy * dy;
                if (d < bd) { sd = bd; bd = d; best = i; } else if (d < sd) sd = d;
            }
            return sd - bd <= 3 ? J(70, 70, 70, 5) : J(shade[best], shade[best], shade[best], 6);
        });

        Fill(Tile.Planks, (x, y) =>
        {
            int board = y / 4;
            bool seam = y % 4 == 3 || x == (board * 5 + 3) % TILE;
            return seam ? J(110, 85, 48, 4) : J(168 - board * 4, 134 - board * 3, 80 - board * 2, 6);
        });
        Fill(Tile.LogSide, (x, y) => (x * 5 + 1) % 4 == 0 ? J(74, 58, 34, 5) : J(104, 82, 50, 7));
        Fill(Tile.LogTop, (x, y) =>
        {
            int d = Mathf.Max(Mathf.Abs(x * 2 - 15), Mathf.Abs(y * 2 - 15)) / 2;
            return d >= 7 ? J(104, 82, 50, 6) : d % 2 == 0 ? J(184, 148, 92, 5) : J(150, 118, 70, 5);
        });
        Fill(Tile.Leaves, (x, y) =>
        {
            int r = R(10);
            return r == 0 ? J(26, 62, 22, 5) : r < 4 ? J(72, 142, 50, 8) : J(48, 108, 36, 8);
        });
        Fill(Tile.Sand, (x, y) => R(8) == 0 ? J(198, 186, 142, 5) : J(220, 208, 164, 6));
        Fill(Tile.Gravel, (x, y) =>
        {
            int r = R(4);
            return r == 0 ? J(104, 98, 98, 6) : r == 1 ? J(160, 152, 150, 6) : J(134, 126, 124, 8);
        });
        Fill(Tile.Glass, (x, y) =>
        {
            bool frame = x == 0 || y == 0 || x == TILE - 1 || y == TILE - 1;
            bool streak = x + y == 6 || x + y == 7 || x + y == 21;
            return frame ? C(214, 240, 246) : streak ? C(240, 252, 255) : C(150, 205, 222);
        });
        Fill(Tile.Bricks, (x, y) =>
        {
            int row = y / 4;
            bool mortar = y % 4 == 3 || (x + (row % 2) * 4) % 8 == 7;
            return mortar ? J(176, 170, 160, 6) : J(150 + (row * 13 + x / 8 * 7) % 14, 84, 68, 7);
        });
        Fill(Tile.StoneBricks, (x, y) =>
        {
            int row = y / 8, bx = (x + (row % 2) * 8) % 16, by = y % 8;
            if (by == 7 || bx == 15) return J(84, 84, 84, 4);
            if (by == 0 || bx == 0) return J(146, 146, 146, 4);
            return J(122, 122, 122, 6);
        });
        Fill(Tile.SandstoneTop, (x, y) => J(218, 206, 160, 5));
        Fill(Tile.SandstoneSide, (x, y) => y < 3 ? J(204, 190, 140, 4) : y == 3 || y == 9 || y == 14 ? J(186, 172, 124, 4) : J(220, 208, 164, 5));
        Fill(Tile.Snow, (x, y) => R(9) == 0 ? J(222, 236, 240, 3) : J(242, 250, 252, 3));
        Fill(Tile.Ice, (x, y) => (x + y) % 7 == 0 || (x - y + 16) % 11 == 0 ? J(196, 220, 255, 4) : J(150, 190, 248, 5));

        Ore(Tile.CoalOre, 44, 44, 44);
        Ore(Tile.IronOre, 216, 175, 147);
        Ore(Tile.GoldOre, 250, 236, 80);
        Ore(Tile.DiamondOre, 96, 236, 244);
        Metal(Tile.IronBlock, 216, 216, 216);
        Metal(Tile.GoldBlock, 246, 226, 70);
        Metal(Tile.DiamondBlock, 100, 220, 214);

        int[,] books = { { 150, 40, 36 }, { 46, 96, 50 }, { 50, 64, 150 }, { 110, 76, 40 }, { 190, 170, 80 }, { 110, 50, 130 } };
        Fill(Tile.Bookshelf, (x, y) =>
        {
            if (y < 2 || y == 7 || y == 8 || y == 15) return G(Tile.Planks, x, y);
            if (x % 2 == 1 && (x + y / 8) % 3 == 0) return C(40, 30, 20);
            int b = (x / 2 * 5 + y / 8 * 3) % books.GetLength(0);
            bool band = y == 4 || y == 11;
            return band ? C(230, 220, 190) : J(books[b, 0], books[b, 1], books[b, 2], 6);
        });
        Fill(Tile.Netherrack, (x, y) =>
        {
            int r = R(6);
            return r == 0 ? J(72, 28, 28, 5) : r == 1 ? J(146, 74, 70, 6) : J(112, 54, 52, 7);
        });
        Fill(Tile.Glowstone, (x, y) =>
        {
            int r = R(7);
            return r == 0 ? J(136, 96, 58, 6) : r < 3 ? J(255, 240, 196, 4) : J(246, 208, 150, 8);
        });
        Fill(Tile.Obsidian, (x, y) => R(11) == 0 ? J(58, 40, 86, 8) : J(22, 18, 32, 6));
        Fill(Tile.Bedrock, (x, y) =>
        {
            int r = R(5);
            return r == 0 ? J(134, 134, 134, 8) : r < 3 ? J(52, 52, 52, 6) : J(88, 88, 88, 8);
        });

        Wool(Tile.WoolWhite, 232, 234, 234);
        Wool(Tile.WoolRed, 162, 40, 36);
        Wool(Tile.WoolOrange, 240, 118, 22);
        Wool(Tile.WoolYellow, 248, 198, 42);
        Wool(Tile.WoolGreen, 110, 184, 28);
        Wool(Tile.WoolBlue, 54, 60, 160);
        Wool(Tile.WoolPurple, 122, 44, 172);
        Wool(Tile.WoolBlack, 30, 30, 34);

        // TNT
        string[] letters =
        {
            "###.#..#.###",
            ".#..##.#..#.",
            ".#..#.##..#.",
            ".#..#..#..#.",
            ".#..#..#..#.",
        };
        Fill(Tile.TntSide, (x, y) =>
        {
            if (y >= 5 && y <= 11)
            {
                int lx = x - 2, ly = y - 6;
                bool ink = ly >= 0 && ly < 5 && lx >= 0 && lx < 12 && letters[ly][lx] == '#';
                return ink ? C(34, 30, 30) : J(232, 230, 224, 4);
            }
            return x % 4 == 3 ? J(150, 38, 18, 5) : J(214, 62, 30, 8);
        });
        Fill(Tile.TntTop, (x, y) =>
        {
            int cx = x % 4, cy = y % 4;
            bool core = (cx == 1 || cx == 2) && (cy == 1 || cy == 2);
            return cx == 3 || cy == 3 ? J(150, 38, 18, 5) : core ? J(120, 116, 110, 6) : J(214, 62, 30, 8);
        });
        Fill(Tile.TntBottom, (x, y) => x % 4 == 3 || y % 4 == 3 ? J(150, 38, 18, 5) : J(200, 56, 28, 8));

        // creeper
        Fill(Tile.CreeperSkin, (x, y) =>
        {
            int r = R(10);
            return r == 0 ? J(214, 232, 206, 6) : r < 3 ? J(46, 110, 44, 8) : r < 5 ? J(120, 200, 100, 8) : J(84, 164, 72, 10);
        });
        string[] face =
        {
            "........",
            "........",
            ".##..##.",
            ".##..##.",
            "...##...",
            "..####..",
            "..####..",
            "..#..#..",
        };
        Fill(Tile.CreeperFace, (x, y) => face[y / 2][x / 2] == '#' ? C(14, 18, 14) : G(Tile.CreeperSkin, x, y));

        // hotbar icons
        Fill(Tile.FlintSteel, (x, y) =>
        {
            bool steel = (x >= 3 && x <= 5 && y >= 2 && y <= 12) || (y >= 2 && y <= 4 && x >= 3 && x <= 10) || (y >= 10 && y <= 12 && x >= 3 && x <= 9);
            bool flint = x >= 9 && x <= 13 && y >= 7 && y <= 13 && x - y <= 3 && y - x <= 3;
            return steel ? J(196, 196, 204, 5) : flint ? J(52, 52, 58, 5) : C(0, 0, 0, 0);
        });
        Fill(Tile.Egg, (x, y) =>
        {
            float dx = (x - 7.5f) / 5.5f, dy = (y - 8.5f) / 6.5f;
            if (dx * dx + dy * dy > 1f) return C(0, 0, 0, 0);
            return (x * 3 + y * 5) % 7 < 2 ? C(16, 20, 16) : J(84, 180, 72, 8);
        });
        Fill(Tile.White, (x, y) => C(255, 255, 255));

        Atlas = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UKCraft atlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Atlas.SetPixels32(px);
        Atlas.Apply(false, false);
        Object.DontDestroyOnLoad(Atlas);
        px = null;
    }

    #endregion
    #region materials

    private static Material blockMat, flashMat;

    /// <summary> Material shared by every block. Uses the game's own level shader so blocks get the same look as the level. </summary>
    public static Material BlockMaterial
    {
        get
        {
            if (blockMat != null) return blockMat;
            Build();

            var refs = MonoSingleton<DefaultReferenceManager>.Instance;
            var shader = refs != null && refs.masterShader != null ? refs.masterShader : Shader.Find("Unlit/Texture");
            blockMat = new Material(shader) { name = "UKCraft blocks", mainTexture = Atlas };
            if (blockMat.HasProperty("_Color")) blockMat.SetColor("_Color", Color.white);
            Object.DontDestroyOnLoad(blockMat);
            return blockMat;
        }
    }

    /// <summary> Plain white, drawn over primed TNT and a fusing creeper when they flash. </summary>
    public static Material FlashMaterial
    {
        get
        {
            if (flashMat != null) return flashMat;
            flashMat = new Material(Shader.Find("Hidden/Internal-Colored")) { name = "UKCraft flash" };
            flashMat.SetColor("_Color", Color.white);
            Object.DontDestroyOnLoad(flashMat);
            return flashMat;
        }
    }

    #endregion
    #region box meshes

    // corners of each face, wound so the normal points out; uv order is (0,0) (0,1) (1,1) (1,0)
    public static readonly Vector3[][] Corners =
    {
        new[] { new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1) },
        new[] { new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0) },
        new[] { new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) },
        new[] { new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1) },
        new[] { new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1) },
        new[] { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0) },
    };
    public static readonly Vector3Int[] Dirs =
    {
        new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1),
    };
    public static readonly float[] Shade = { 0.7f, 0.7f, 1f, 0.5f, 0.85f, 0.85f };

    /// <summary> Builds a textured box centered on the offset. tiles has six entries in face order. </summary>
    public static Mesh Box(Vector3 size, Tile[] tiles, Vector3 offset = default)
    {
        var v = new Vector3[24]; var n = new Vector3[24]; var uv = new Vector2[24]; var c = new Color[24]; var t = new int[36];
        for (int f = 0; f < 6; f++)
        {
            var r = UV(tiles[f]);
            for (int i = 0; i < 4; i++)
            {
                v[f * 4 + i] = Vector3.Scale(Corners[f][i] - Vector3.one * 0.5f, size) + offset;
                n[f * 4 + i] = Dirs[f];
                c[f * 4 + i] = new Color(Shade[f], Shade[f], Shade[f], 1f);
            }
            uv[f * 4] = new Vector2(r.x, r.y); uv[f * 4 + 1] = new Vector2(r.x, r.w); uv[f * 4 + 2] = new Vector2(r.z, r.w); uv[f * 4 + 3] = new Vector2(r.z, r.y);
            int o = f * 6, b = f * 4;
            t[o] = b; t[o + 1] = b + 1; t[o + 2] = b + 2; t[o + 3] = b; t[o + 4] = b + 2; t[o + 5] = b + 3;
        }
        var m = new Mesh { vertices = v, normals = n, uv = uv, colors = c, triangles = t };
        m.RecalculateBounds();
        return m;
    }

    public static Tile[] Tiles(BlockId id)
    {
        var t = new Tile[6];
        for (int f = 0; f < 6; f++) t[f] = Face(id, f);
        return t;
    }

    #endregion
}
