namespace UKCraft;

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using UnityEngine;

/// <summary>
/// Test channel: drop a text file named ukcraft.cmd into BepInEx/config and the mod runs it, one command per line.
/// It lets a scripted test build, light and blow things up without touching the mouse or keyboard.
/// </summary>
public class Commands : MonoBehaviour
{
    private static readonly string path = Path.Combine(Paths.ConfigPath, "ukcraft.cmd");

    private readonly Queue<string> queue = new();
    private float nextPoll, waitUntil;

    private void Update()
    {
        float now = Time.unscaledTime;
        if (now >= nextPoll)
        {
            nextPoll = now + 0.25f;
            try
            {
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);
                    File.Delete(path);
                    foreach (var l in lines) if (l.Trim().Length > 0 && !l.StartsWith("#")) queue.Enqueue(l.Trim());
                }
            }
            catch (IOException) { }
        }

        while (queue.Count > 0 && Time.unscaledTime >= waitUntil)
        {
            string line = queue.Dequeue();
            try { Run(line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries), line); }
            catch (System.Exception e) { Plugin.Log.LogError($"[cmd] {line}: {e}"); }
        }
    }

    private static float F(string[] a, int i, float fallback = 0f) =>
        a.Length > i && float.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

    /// <summary> A point on the ground ahead of the player, at most dist away. </summary>
    private static Vector3 Ahead(float dist, out Vector3 normal)
    {
        var t = Game.Cam.cam.transform;
        if (Physics.Raycast(t.position, t.forward, out var hit, dist, Game.EnvMask, QueryTriggerInteraction.Ignore))
        {
            normal = hit.normal;
            return hit.point;
        }
        normal = Vector3.up;
        return t.position + t.forward * dist;
    }

    private static Vector3Int Feet()
    {
        var b = Game.Player.playerCollider.bounds;
        return VoxelWorld.CellAt(new Vector3(b.center.x, b.min.y + 0.1f, b.center.z));
    }

    private static bool Block(string name, out BlockId id) => System.Enum.TryParse(name, true, out id) && id < BlockId.Count;

    private void Run(string[] a, string line)
    {
        Plugin.Log.LogInfo($"[cmd] {line}");
        var world = VoxelWorld.I;
        switch (a[0].ToLowerInvariant())
        {
            case "wait": waitUntil = Time.unscaledTime + F(a, 1, 1f); break;
            case "log": Plugin.Log.LogInfo($"[cmd] note: {line.Substring(3).Trim()}"); break;
            case "load": SceneHelper.LoadScene(line.Substring(4).Trim()); break;
            case "probe": Probe.Run(); break;
            case "shot": ScreenCapture.CaptureScreenshot(line.Substring(4).Trim()); break;
            case "hp": Game.Player.hp = (int)F(a, 1, 100f); break;

            case "pos":
                var pl = Game.Player;
                Plugin.Log.LogInfo($"[cmd] pos {pl.transform.position} feet cell {Feet()} yaw {Game.Cam.rotationY} pitch {Game.Cam.rotationX} scene {SceneHelper.CurrentScene}");
                break;
            case "tp":
                Game.Player.transform.position = new Vector3(F(a, 1), F(a, 2), F(a, 3));
                Game.Player.rb.velocity = Vector3.zero;
                break;
            case "look":
                Game.Cam.rotationY = F(a, 1);
                Game.Cam.rotationX = F(a, 2);
                break;

            case "build":
                if (a.Length > 1 && a[1] == "off") BuildMode.I.Leave(); else BuildMode.I.Enter();
                break;
            case "slot": BuildMode.I.Slot = Mathf.Clamp((int)F(a, 1), 0, BuildMode.Items.Length - 1); break;
            case "break":
                if (BuildMode.Look(out var ba)) BuildMode.I.Break(ba); else Plugin.Log.LogInfo("[cmd] nothing in reach");
                break;
            case "use":
                if (BuildMode.Look(out var ua)) BuildMode.I.Use(ua); else Plugin.Log.LogInfo("[cmd] nothing in reach");
                break;

            // set <block> <dx> <dy> <dz>: one block, relative to the cell under the player's feet
            case "set":
                if (Block(a[1], out var sid)) world.Set(Feet() + new Vector3Int((int)F(a, 2), (int)F(a, 3), (int)F(a, 4)), sid);
                break;
            // fill <block> <x0> <y0> <z0> <x1> <y1> <z1>: a box of blocks, relative to the feet cell
            case "fill":
                if (!Block(a[1], out var fid)) break;
                var origin = Feet();
                for (int x = (int)F(a, 2); x <= (int)F(a, 5); x++) for (int y = (int)F(a, 3); y <= (int)F(a, 6); y++) for (int z = (int)F(a, 4); z <= (int)F(a, 7); z++)
                    world.Set(origin + new Vector3Int(x, y, z), fid);
                break;
            // palette: one of every block in a row, relative to the feet cell
            case "palette":
                var start = Feet() + new Vector3Int((int)F(a, 1), (int)F(a, 2), (int)F(a, 3));
                for (var id = BlockId.Air + 1; id < BlockId.Count; id++)
                {
                    int i = (int)id - 1;
                    world.Set(start + new Vector3Int(i % 9, i / 9, 0), id);
                }
                break;
            case "clear": world.Clear(); break;

            // prime <dx> <dy> <dz> [fuse]: light the TNT block at that cell
            case "prime": PrimedTnt.Prime(Feet() + new Vector3Int((int)F(a, 1), (int)F(a, 2), (int)F(a, 3)), F(a, 4, Plugin.TntFuse.Value)); break;
            // tnt [dist] [fuse]: drop lit TNT where the crosshair points
            case "tnt":
                var tp = Ahead(F(a, 1, 20f), out var tn);
                PrimedTnt.Spawn(tp + tn * (VoxelWorld.S * 0.6f), F(a, 2, Plugin.TntFuse.Value));
                break;
            // boom [dist] [blocks]: explosion where the crosshair points
            case "boom":
                var bp = Ahead(F(a, 1, 20f), out _);
                Destruction.Explode(bp, F(a, 2, Plugin.TntRadius.Value), false);
                break;
            // crater [dist] [blocks]: the hole only, without an explosion
            case "crater":
                var cp = Ahead(F(a, 1, 20f), out _);
                Destruction.Crater(cp, F(a, 2, Plugin.TntRadius.Value) * VoxelWorld.S);
                break;
            case "creeper":
                var sp = Ahead(F(a, 1, 20f), out var sn);
                Creeper.Spawn(sp + sn * 0.2f);
                break;

            default: Plugin.Log.LogWarning($"[cmd] unknown command: {line}"); break;
        }
    }
}
