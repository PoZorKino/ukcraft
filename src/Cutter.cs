namespace UKCraft;

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cuts one triangle against a set of destroyed grid cells: the triangle is sliced along the block grid, pieces that lie in a
/// destroyed cell are dropped and the rest is returned as convex polygons. Pure math, so it can be tested outside the game.
/// </summary>
public static class Cutter
{
    /// <summary> Corner of a polygon: where it is in the world and where it is on the original triangle (barycentric). </summary>
    public struct PV
    {
        public Vector3 p, w;
    }

    public static HashSet<Vector3Int> Cells;
    public static Vector3 Min, Max;
    public static float S;
    /// <summary> Set for meshes that are mirrored by their transform: their triangles face the other way in the world. </summary>
    public static bool Mirrored;

    /// <summary> What is left of the last triangle that was cut. </summary>
    public static readonly List<List<PV>> Kept = new();

    private static readonly List<PV> polyA = new(), polyB = new(), polyC = new();
    private static readonly Stack<List<PV>> pool = new();

    /// <summary> Sets the cells to destroy and the box around them. </summary>
    public static void Begin(HashSet<Vector3Int> cells, float size)
    {
        Cells = cells;
        S = size;
        var lo = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
        var hi = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        foreach (var c in cells) { lo = Vector3Int.Min(lo, c); hi = Vector3Int.Max(hi, c); }
        // a little wider than the cells, so a surface lying exactly on the outer faces of the box is still looked at
        var margin = new Vector3(0.05f, 0.05f, 0.05f);
        Min = new Vector3(lo.x, lo.y, lo.z) * S - margin;
        Max = new Vector3(hi.x + 1, hi.y + 1, hi.z + 1) * S + margin;
    }

    public static Vector3Int CellAt(Vector3 p) => new(Mathf.FloorToInt(p.x / S), Mathf.FloorToInt(p.y / S), Mathf.FloorToInt(p.z / S));

    /// <summary> Cheap test: can the triangle touch the box around the destroyed cells at all? </summary>
    public static bool Near(Vector3 a, Vector3 b, Vector3 c) => !(
        Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < Min.x || Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > Max.x ||
        Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < Min.y || Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > Max.y ||
        Mathf.Max(a.z, Mathf.Max(b.z, c.z)) < Min.z || Mathf.Min(a.z, Mathf.Min(b.z, c.z)) > Max.z);

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
        Kept.Add(copy);
    }

    /// <summary> Slices the polygon along every grid plane of the given axis and the ones after it; returns true if a piece was destroyed. </summary>
    private static bool Slice(List<PV> poly, int axis, Vector3 normal)
    {
        if (poly.Count < 3) return false;
        if (axis == 3)
        {
            // a surface belongs to the cell behind it, so a floor that sits exactly on a grid plane goes with the block below
            var center = Vector3.zero;
            foreach (var v in poly) center += v.p;
            center = center / poly.Count - normal * 0.02f;
            if (Cells.Contains(CellAt(center))) return true;
            Keep(poly);
            return false;
        }

        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var v in poly) { lo = Mathf.Min(lo, v.p[axis]); hi = Mathf.Max(hi, v.p[axis]); }

        bool removed = false;
        List<PV> rest = Rent(), below = Rent(), above = Rent();
        rest.AddRange(poly);
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

    /// <summary> Cuts a triangle. Returns true if part of it was destroyed; Kept then holds what remains. </summary>
    public static bool Cut(Vector3 a, Vector3 b, Vector3 c)
    {
        foreach (var l in Kept) pool.Push(l);
        Kept.Clear();

        var normal = Vector3.Cross(b - a, c - a).normalized;
        if (Mirrored) normal = normal * -1f;

        // peel off whatever sticks out of the box around the destroyed cells
        polyA.Clear();
        polyA.Add(new PV { p = a, w = new Vector3(1, 0, 0) });
        polyA.Add(new PV { p = b, w = new Vector3(0, 1, 0) });
        polyA.Add(new PV { p = c, w = new Vector3(0, 0, 1) });
        for (int axis = 0; axis < 3 && polyA.Count >= 3; axis++)
        {
            Split(polyA, axis, Min[axis], polyB, polyC);
            Keep(polyB);
            Split(polyC, axis, Max[axis], polyA, polyB);
            Keep(polyB);
        }

        return polyA.Count >= 3 && Slice(polyA, 0, normal);
    }
}
