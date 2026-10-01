// Stand-ins for the few Unity types Cutter.cs uses, so the real file can be tested without the game.
namespace UnityEngine
{
    using System;

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float this[int i]
        {
            get => i == 0 ? x : i == 1 ? y : z;
            set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; }
        }
        public static Vector3 zero => new(0, 0, 0);
        public float magnitude => MathF.Sqrt(x * x + y * y + z * z);
        public Vector3 normalized { get { float m = magnitude; return m > 1e-9f ? this / m : zero; } }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float d) => new(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new(a.x / d, a.y / d, a.z / d);
        public static Vector3 Cross(Vector3 a, Vector3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
    }

    public struct Vector3Int : IEquatable<Vector3Int>
    {
        public int x, y, z;
        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3Int Min(Vector3Int a, Vector3Int b) => new(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
        public static Vector3Int Max(Vector3Int a, Vector3Int b) => new(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));
        public bool Equals(Vector3Int o) => x == o.x && y == o.y && z == o.z;
        public override bool Equals(object o) => o is Vector3Int v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
    }

    public static class Mathf
    {
        public static int FloorToInt(float f) => (int)MathF.Floor(f);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
    }
}

namespace Test
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UKCraft;

    public static class Program
    {
        private static int failed;

        private static void Check(bool ok, string what)
        {
            if (!ok) failed++;
            Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {what}");
        }

        private static float Area(Vector3 a, Vector3 b, Vector3 c) => Vector3.Cross(b - a, c - a).magnitude * 0.5f;

        private static float KeptArea()
        {
            float area = 0f;
            foreach (var poly in Cutter.Kept)
                for (int k = 2; k < poly.Count; k++) area += Area(poly[0].p, poly[k - 1].p, poly[k].p);
            return area;
        }

        public static int Main()
        {
            const float S = 2f;

            // 1. one cell under a floor that lies exactly on a grid plane: a 2x2 hole
            var cells = new HashSet<Vector3Int> { new(3, -1, 3) };
            Cutter.Begin(cells, S);
            Vector3 a = new(0, 0, 0), b = new(0, 0, 20), c = new(20, 0, 20), d = new(20, 0, 0);
            bool cut1 = Cutter.Cut(a, b, c); float kept1 = cut1 ? KeptArea() : Area(a, b, c);
            bool cut2 = Cutter.Cut(a, c, d); float kept2 = cut2 ? KeptArea() : Area(a, c, d);
            Check(cut1 || cut2, "floor: something was cut");
            Check(MathF.Abs(400f - kept1 - kept2 - 4f) < 0.01f, $"floor: hole area {400f - kept1 - kept2:0.000} (want 4)");

            // 2. the cell above the floor does not take the floor with it
            Cutter.Begin(new HashSet<Vector3Int> { new(3, 0, 3) }, S);
            Check(!Cutter.Cut(a, b, c) && !Cutter.Cut(a, c, d), "floor: cell above leaves the floor alone");

            // 3. a ceiling (normal down) belongs to the cell above it
            Cutter.Begin(new HashSet<Vector3Int> { new(3, 0, 3) }, S);
            Check(Cutter.Cut(a, c, b) | Cutter.Cut(a, d, c), "ceiling: cell above cuts it");

            // 4. triangle far away is untouched
            Cutter.Begin(cells, S);
            Check(!Cutter.Near(new(100, 0, 100), new(100, 0, 120), new(120, 0, 120)), "far triangle is skipped");

            // 5. random triangles against random cell sets, checked by sampling points on the triangle
            var rng = new Random(42);
            float F(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            int trials = 400, bad = 0, cuts = 0; float worst = 0f;
            for (int t = 0; t < trials; t++)
            {
                var set = new HashSet<Vector3Int>();
                int n = rng.Next(1, 60);
                var center = new Vector3Int(rng.Next(-4, 4), rng.Next(-4, 4), rng.Next(-4, 4));
                for (int i = 0; i < n; i++) set.Add(new Vector3Int(center.x + rng.Next(-3, 4), center.y + rng.Next(-3, 4), center.z + rng.Next(-3, 4)));
                Cutter.Begin(set, S);

                Vector3 p0 = new(F(-14, 14), F(-14, 14), F(-14, 14)), p1 = new(F(-14, 14), F(-14, 14), F(-14, 14)), p2 = new(F(-14, 14), F(-14, 14), F(-14, 14));
                float total = Area(p0, p1, p2);
                if (total < 1f) continue;
                var normal = Vector3.Cross(p1 - p0, p2 - p0).normalized;

                bool cut = Cutter.Near(p0, p1, p2) && Cutter.Cut(p0, p1, p2);
                float kept = cut ? KeptArea() : total;
                if (cut) cuts++;

                // every corner must sit where its barycentric weights say it does
                if (cut)
                    foreach (var poly in Cutter.Kept)
                        foreach (var v in poly)
                        {
                            var q = p0 * v.w.x + p1 * v.w.y + p2 * v.w.z;
                            if ((q - v.p).magnitude > 0.01f || MathF.Abs(v.w.x + v.w.y + v.w.z - 1f) > 0.001f) bad++;
                        }

                int samples = 4000, inside = 0;
                for (int i = 0; i < samples; i++)
                {
                    float u = (float)rng.NextDouble(), w = (float)rng.NextDouble();
                    if (u + w > 1f) { u = 1f - u; w = 1f - w; }
                    var q = p0 + (p1 - p0) * u + (p2 - p0) * w - normal * 0.02f;
                    if (!set.Contains(Cutter.CellAt(q))) inside++;
                }
                float expected = total * inside / samples;
                float err = MathF.Abs(expected - kept) / total;
                worst = MathF.Max(worst, err);
            }
            Check(bad == 0, $"random: barycentric weights match positions ({bad} bad corners)");
            Check(cuts > 50, $"random: {cuts} of {trials} triangles were cut");
            Check(worst < 0.05f, $"random: kept area matches sampling, worst error {worst * 100f:0.0}% of the triangle");

            Console.WriteLine(failed == 0 ? "ALL PASSED" : $"{failed} FAILED");
            return failed;
        }
    }
}
