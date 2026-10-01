namespace UKCraft;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

[BepInPlugin(GUID, "UKCraft", VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string GUID = "dexx.ukcraft", VERSION = "0.1.0";

    public static Plugin Instance;
    public static ManualLogSource Log;

    public static ConfigEntry<float> BlockSize, Reach, TntRadius, CreeperRadius, ExplosionCarveScale, TntFuse;
    public static ConfigEntry<bool> AllExplosionsDestroy, DestroyLevel, MineLevel;
    public static ConfigEntry<UnityEngine.InputSystem.Key> BuildKey;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        BlockSize = Config.Bind("World", "BlockSize", 2f, "Edge length of one block in game units. V1 is about 1.75 blocks tall at 2.");
        Reach = Config.Bind("World", "Reach", 14f, "How far you can place and break blocks, in game units.");
        BuildKey = Config.Bind("Controls", "BuildKey", UnityEngine.InputSystem.Key.B, "Toggles build mode.");
        TntRadius = Config.Bind("Explosions", "TntRadius", 4f, "TNT crater radius in blocks.");
        CreeperRadius = Config.Bind("Explosions", "CreeperRadius", 3f, "Creeper crater radius in blocks.");
        TntFuse = Config.Bind("Explosions", "TntFuse", 4f, "TNT fuse in seconds.");
        DestroyLevel = Config.Bind("Explosions", "DestroyLevel", true, "Explosions cut holes in the level itself, not only in placed blocks.");
        MineLevel = Config.Bind("World", "MineLevel", true, "Breaking in build mode also digs block-sized holes in the level itself.");
        AllExplosionsDestroy = Config.Bind("Explosions", "AllExplosionsDestroy", true, "Every damaging explosion in the game (rockets, grenades, enemies) destroys the world too.");
        ExplosionCarveScale = Config.Bind("Explosions", "ExplosionCarveScale", 0.45f, "Crater radius of a game explosion, as a fraction of its blast size.");

        new Harmony(GUID).PatchAll();

        var go = new GameObject("UKCraft") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        go.AddComponent<VoxelWorld>();
        go.AddComponent<BuildMode>();
        go.AddComponent<Commands>();

        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (mode != LoadSceneMode.Single) return;
            VoxelWorld.I.Clear();
            LevelCarver.Reset();
            BuildMode.I.Leave(false);
            Log.LogInfo($"scene loaded: {scene.name}");
        };

        Log.LogInfo($"UKCraft {VERSION} loaded");
    }
}

/// <summary> Safe access to the bits of the game the mod touches. </summary>
public static class Game
{
    public static NewMovement Player => MonoSingleton<NewMovement>.Instance;
    public static CameraController Cam => MonoSingleton<CameraController>.Instance;

    public static int EnvMask => LayerMaskDefaults.Get(LMD.Environment);

    /// <summary> True while the player exists, is alive and the game is not paused. </summary>
    public static bool Playing
    {
        get
        {
            var p = Player;
            if (p == null || p.dead || !p.gameObject.activeInHierarchy) return false;
            var o = MonoSingleton<OptionsManager>.Instance;
            return o == null || !o.paused;
        }
    }

    public static void Hud(string msg)
    {
        var h = MonoSingleton<HudMessageReceiver>.Instance;
        if (h != null) h.SendHudMessage(msg);
    }
}
