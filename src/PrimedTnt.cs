namespace UKCraft;

using UnityEngine;

/// <summary> A lit TNT block: hops, falls, flashes white and then blows a crater. </summary>
public class PrimedTnt : MonoBehaviour
{
    private static Mesh mesh;
    private static AudioClip hiss;

    private float fuse, age;
    private Vector3 velocity;
    private MeshRenderer mr;

    /// <summary> Lights the TNT block in the given cell. Does nothing if there is no TNT there. </summary>
    public static void Prime(Vector3Int cell, float fuse)
    {
        var world = VoxelWorld.I;
        if (world.Get(cell) != BlockId.Tnt) return;
        world.Set(cell, BlockId.Air);
        Spawn(VoxelWorld.CellCenter(cell), fuse);
    }

    public static PrimedTnt Spawn(Vector3 pos, float fuse)
    {
        if (mesh == null)
        {
            mesh = Blocks.Box(Vector3.one, Blocks.Tiles(BlockId.Tnt));
            DontDestroyOnLoad(mesh);
        }

        var go = new GameObject("UKCraft primed TNT");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * (VoxelWorld.S * 0.98f);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var tnt = go.AddComponent<PrimedTnt>();
        tnt.mr = go.AddComponent<MeshRenderer>();
        tnt.mr.sharedMaterial = Blocks.BlockMaterial;
        tnt.fuse = fuse;
        tnt.velocity = new Vector3(Random.Range(-0.6f, 0.6f), 5f, Random.Range(-0.6f, 0.6f));

        Sfx.Play(go, Hiss, 0.7f);
        return tnt;
    }

    public static AudioClip Hiss => hiss != null ? hiss : hiss = Sfx.Noise("UKCraft fuse", 1.2f, 0.35f);

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        // simple box physics against the level and placed blocks
        velocity.y -= 40f * dt;
        var step = velocity * dt;
        float dist = step.magnitude;
        if (dist > 0.0001f)
        {
            var half = Vector3.one * (VoxelWorld.S * 0.47f);
            if (Physics.BoxCast(transform.position, half, step / dist, out var hit, Quaternion.identity, dist + 0.02f, Game.EnvMask, QueryTriggerInteraction.Ignore))
            {
                transform.position += step / dist * Mathf.Max(0f, hit.distance - 0.02f);
                velocity = Vector3.zero;
            }
            else transform.position += step;
        }

        // flash twice a second, swell just before the end
        mr.sharedMaterial = age % 0.5f < 0.25f ? Blocks.FlashMaterial : Blocks.BlockMaterial;
        float left = fuse - age;
        if (left < 0.35f) transform.localScale = Vector3.one * (VoxelWorld.S * Mathf.Lerp(1.25f, 0.98f, Mathf.Clamp01(left / 0.35f)));

        if (left <= 0f || transform.position.y < -5000f)
        {
            var pos = transform.position;
            Destroy(gameObject);
            if (left <= 0f) Destruction.Explode(pos, Plugin.TntRadius.Value, true);
        }
    }
}

/// <summary> Small sounds synthesized in code. </summary>
public static class Sfx
{
    /// <summary> White noise that fades in and out: a fuse hiss. </summary>
    public static AudioClip Noise(string name, float seconds, float lowpass)
    {
        const int rate = 22050;
        int count = (int)(seconds * rate);
        var data = new float[count];
        var rng = new System.Random(7);
        float prev = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float env = Mathf.Min(1f, t * 12f) * Mathf.Min(1f, (1f - t) * 4f);
            float white = (float)rng.NextDouble() * 2f - 1f;
            prev += (white - prev) * (1f - lowpass);
            data[i] = (white - prev) * env * 0.6f;
        }
        var clip = AudioClip.Create(name, count, 1, rate, false);
        clip.SetData(data, 0);
        Object.DontDestroyOnLoad(clip);
        return clip;
    }

    public static AudioSource Play(GameObject on, AudioClip clip, float volume)
    {
        var src = on.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = 1f;
        src.minDistance = 8f;
        src.maxDistance = 80f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.Play();
        return src;
    }
}
