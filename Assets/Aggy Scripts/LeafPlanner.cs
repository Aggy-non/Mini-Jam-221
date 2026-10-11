using System;
using System.Collections.Generic;

// ======================================================================
//  LeafPlanner.cs
//  Pure C# (no UnityEngine) so it can be tested headlessly and seeded.
//
//  THE RULE THIS FILE ENFORCES
//  ---------------------------
//  Every SOLID leaf in row k is within `reach` (center to center) of the
//  path leaf ("trunk") of row k+1. The trunk of row k+1 is chosen one row
//  EARLY (lookahead), so when row k is built we already know where its
//  extras have to be. A row may therefore shrink from 3 leaves to 1 and
//  every leaf in the bigger row can still reach the single leaf above it.
//
//  Extra, WITHERING leaves are placed anywhere in the free space. They are
//  allowed to be dead ends because they disappear after the spider lands
//  on them (see WitheringLeaf.cs).
// ======================================================================

public enum LeafKind
{
    Solid,      // guaranteed to have an exit, stays
    Withering   // may be a dead end, rots away once stepped on
}

public class LeafSpec
{
    public float x;
    public float width;
    public LeafKind kind;
    public bool isTrunk;
}

public class LeafRow
{
    public int level;
    public readonly List<LeafSpec> leaves = new List<LeafSpec>();

    // Where the NEXT row's path leaf will be, and the range solid extras had to stay in
    public float nextTrunkX;
    public float windowLeft;
    public float windowRight;

    public LeafSpec Trunk { get { return leaves[0]; } }
    public float TrunkX { get { return leaves[0].x; } }
}

public class LeafPlannerSettings
{
    public float platformSize = 1f;
    public float platformClearance = 0.25f;

    // Comfort reach, center to center. The guarantee is made with THIS number,
    // not with the raw physics maximum.
    public float reach = 3f;

    // Camera edges in world space
    public float camLeftEdge = -5f;
    public float camRightEdge = 5f;

    // Soft targets: the planner places fewer if the space or reach window can't hold them
    public int minSolidLeaves = 1;      // includes the path leaf
    public int maxSolidLeaves = 3;
    public int minWitheringLeaves = 0;
    public int maxWitheringLeaves = 2;

    // When a row of several solid leaves is followed by a row with a single solid leaf,
    // that single leaf is the only exit, so it is made wider. 1 = off.
    public float funnelWidthMultiplier = 1.5f;

    // If the validator finds a solid leaf without an exit it becomes a withering leaf
    public bool demoteUnsafeLeaves = true;
}

public class LeafPlanner
{
    private struct PlannedTrunk
    {
        public int level;
        public float x;
        public float width;
        public int solidCount;
        public int witherCount;
    }

    private struct Region
    {
        public float start;   // left edge
        public float width;
    }

    private const float Eps = 0.0001f;

    private readonly Random rng;
    private readonly Queue<PlannedTrunk> planned = new Queue<PlannedTrunk>();

    private float lastTrunkX;
    private float lastTrunkWidth;
    private int lastSolidCount = 1;
    private int nextPlanLevel = 0;
    private LeafRow previousRow;

    public readonly List<string> Warnings = new List<string>();
    public int Seed { get; private set; }
    public int RowsGenerated { get; private set; }
    public int DemotedLeaves { get; private set; }

    public LeafPlanner(int seed, float startX, float startWidth)
    {
        Seed = seed;
        rng = new Random(seed);
        lastTrunkX = startX;
        lastTrunkWidth = startWidth;
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>Builds the next row to spawn. Warnings from the validator end up in Warnings.</summary>
    public LeafRow NextRow(LeafPlannerSettings s)
    {
        Warnings.Clear();

        // Keep one row of lookahead: we need the NEXT trunk before we can place this row's extras
        while (planned.Count < 2) planned.Enqueue(PlanNext(s));

        PlannedTrunk current = planned.Dequeue();
        PlannedTrunk next = planned.Peek();

        LeafRow row = BuildRow(current, next, s);
        RepairAndValidate(row, next, s);

        previousRow = row;
        RowsGenerated++;
        return row;
    }

    // ------------------------------------------------------------------
    // Planning the trunk (one row ahead)
    // ------------------------------------------------------------------

    private PlannedTrunk PlanNext(LeafPlannerSettings s)
    {
        int minSolid = Math.Max(1, s.minSolidLeaves);
        int maxSolid = Math.Max(minSolid, s.maxSolidLeaves);
        int minWither = Math.Max(0, s.minWitheringLeaves);
        int maxWither = Math.Max(minWither, s.maxWitheringLeaves);

        PlannedTrunk p = new PlannedTrunk();
        p.level = nextPlanLevel++;
        p.solidCount = rng.Next(minSolid, maxSolid + 1);
        p.witherCount = rng.Next(minWither, maxWither + 1);

        // Funnel: many leaves below, only one above -> make the single exit easier to land on
        float width = s.platformSize;
        if (s.funnelWidthMultiplier > 1.001f && p.solidCount == 1 && lastSolidCount > 1)
        {
            width *= s.funnelWidthMultiplier;
        }
        float screenWidth = s.camRightEdge - s.camLeftEdge;
        if (width > screenWidth) width = screenWidth;

        p.width = width;
        p.x = PickTrunkX(lastTrunkX, lastTrunkWidth, width, s);

        lastTrunkX = p.x;
        lastTrunkWidth = p.width;
        lastSolidCount = p.solidCount;
        return p;
    }

    // Same left / center / right idea as before, relative to the previous trunk.
    private float PickTrunkX(float prevX, float prevWidth, float newWidth, LeafPlannerSettings s)
    {
        float prevHalf = prevWidth * 0.5f;
        float newHalf = newWidth * 0.5f;
        float lo = s.camLeftEdge + newHalf;     // limits for the new leaf's CENTER
        float hi = s.camRightEdge - newHalf;
        if (lo > hi) { lo = hi = (s.camLeftEdge + s.camRightEdge) * 0.5f; }

        // Closest center-to-center distance that still leaves `clearance` between the edges
        float minOffset = prevHalf + newHalf + s.platformClearance;

        // Side hops are only offered if they are inside the reach AND inside the walls
        bool canLeft = minOffset <= s.reach && prevX - minOffset >= lo;
        bool canRight = minOffset <= s.reach && prevX + minOffset <= hi;

        int optionCount = 1 + (canLeft ? 1 : 0) + (canRight ? 1 : 0);
        int pick = rng.Next(optionCount);
        int choice = pick == 0 ? 0 : (pick == 1 ? (canLeft ? 1 : 2) : 2);   // 0 center, 1 left, 2 right

        switch (choice)
        {
            case 1: // left
                {
                    float far = Math.Max(prevX - s.reach, lo);
                    float near = prevX - minOffset;
                    return RandomRange(far, near);
                }
            case 2: // right
                {
                    float near = prevX + minOffset;
                    float far = Math.Min(prevX + s.reach, hi);
                    return RandomRange(near, far);
                }
            default: // center: straight above, inside the previous leaf's span
                {
                    float spread = Math.Min(prevHalf, s.reach);
                    float cl = Math.Max(prevX - spread, lo);
                    float cr = Math.Min(prevX + spread, hi);
                    if (cl > cr) return Clamp(prevX, lo, hi);
                    return RandomRange(cl, cr);
                }
        }
    }

    // ------------------------------------------------------------------
    // Building a row
    // ------------------------------------------------------------------

    private LeafRow BuildRow(PlannedTrunk cur, PlannedTrunk next, LeafPlannerSettings s)
    {
        LeafRow row = new LeafRow();
        row.level = cur.level;
        row.nextTrunkX = next.x;

        LeafSpec trunk = new LeafSpec();
        trunk.x = cur.x;
        trunk.width = cur.width;
        trunk.kind = LeafKind.Solid;
        trunk.isTrunk = true;
        row.leaves.Add(trunk);

        float half = s.platformSize * 0.5f;

        // Solid extras must stay inside the window around the NEXT trunk. This is the guarantee.
        float winLeft = Math.Max(next.x - s.reach, s.camLeftEdge + half);
        float winRight = Math.Min(next.x + s.reach, s.camRightEdge - half);
        row.windowLeft = winLeft;
        row.windowRight = winRight;

        int extraSolid = cur.solidCount - 1;
        if (extraSolid > 0 && winLeft <= winRight)
        {
            // window is in CENTER coordinates, regions are in EDGE coordinates
            List<Region> regions = FreeRegions(row, winLeft - half, winRight + half, s);
            PlaceInRegions(row, regions, extraSolid, LeafKind.Solid, s);
        }

        // Withering extras go anywhere that is still free. They don't need an exit.
        if (cur.witherCount > 0)
        {
            List<Region> regions = FreeRegions(row, s.camLeftEdge, s.camRightEdge, s);
            PlaceInRegions(row, regions, cur.witherCount, LeafKind.Withering, s);
        }

        return row;
    }

    // Free space between lo and hi once every leaf already in the row (plus clearance) is cut out.
    private List<Region> FreeRegions(LeafRow row, float lo, float hi, LeafPlannerSettings s)
    {
        List<float[]> spans = new List<float[]>();
        foreach (LeafSpec leaf in row.leaves)
        {
            float h = leaf.width * 0.5f;
            spans.Add(new float[] { leaf.x - h - s.platformClearance, leaf.x + h + s.platformClearance });
        }
        spans.Sort(delegate (float[] a, float[] b) { return a[0].CompareTo(b[0]); });

        List<Region> regions = new List<Region>();
        float cursor = lo;
        foreach (float[] span in spans)
        {
            if (span[1] <= cursor) continue;
            if (span[0] > cursor) AddRegion(regions, cursor, Math.Min(span[0], hi), s);
            cursor = Math.Max(cursor, span[1]);
            if (cursor >= hi) break;
        }
        if (cursor < hi) AddRegion(regions, cursor, hi, s);
        return regions;
    }

    private void AddRegion(List<Region> regions, float start, float end, LeafPlannerSettings s)
    {
        float width = end - start;
        if (width + Eps < s.platformSize) return;   // can't hold even one leaf
        Region r = new Region();
        r.start = start;
        r.width = width;
        regions.Add(r);
    }

    // How many leaves fit in a region (with clearance between them)
    private int GetCapacity(float regionWidth, LeafPlannerSettings s)
    {
        float step = s.platformSize + s.platformClearance;
        if (step <= 0f || regionWidth + Eps < s.platformSize) return 0;
        // n * size + (n - 1) * clearance <= width   ->   n <= (width + clearance) / step
        return (int)Math.Floor((regionWidth + s.platformClearance) / step + Eps);
    }

    // Splits `count` leaves over the regions in proportion to their width (largest remainder
    // method, ties go to the wider region), clamps to capacity, then places them randomly.
    // With two regions this reproduces the earlier rule: widths 1:3 -> 3 leaves = 1 + 2, 2 leaves = 0 + 2.
    private void PlaceInRegions(LeafRow row, List<Region> regions, int count, LeafKind kind, LeafPlannerSettings s)
    {
        if (regions.Count == 0 || count <= 0) return;

        int n = regions.Count;
        int[] cap = new int[n];
        int totalCap = 0;
        float totalWidth = 0f;
        for (int i = 0; i < n; i++)
        {
            cap[i] = GetCapacity(regions[i].width, s);
            if (cap[i] > 0)
            {
                totalCap += cap[i];
                totalWidth += regions[i].width;
            }
        }
        if (totalCap == 0) return;

        int toPlace = Math.Min(count, totalCap);
        float[] quota = new float[n];
        int[] alloc = new int[n];
        int assigned = 0;
        for (int i = 0; i < n; i++)
        {
            if (cap[i] <= 0) continue;
            quota[i] = toPlace * regions[i].width / totalWidth;
            alloc[i] = Math.Min(cap[i], (int)Math.Floor(quota[i] + Eps));
            assigned += alloc[i];
        }

        while (assigned < toPlace)
        {
            int best = -1;
            for (int i = 0; i < n; i++)
            {
                if (alloc[i] >= cap[i]) continue;
                if (best == -1) { best = i; continue; }
                float ri = quota[i] - alloc[i];
                float rb = quota[best] - alloc[best];
                if (ri > rb + Eps || (Math.Abs(ri - rb) <= Eps && regions[i].width > regions[best].width)) best = i;
            }
            alloc[best]++;
            assigned++;
        }

        for (int i = 0; i < n; i++)
        {
            if (alloc[i] > 0) PlaceLeavesInRegion(row, regions[i], alloc[i], kind, s);
        }
    }

    // n leaves at random spots in a region, never overlapping, always >= clearance apart.
    // Pack them tight, then push each one right by a random shift taken from the leftover
    // space. Shifts are sorted, so the order (and the gaps) can only grow.
    private void PlaceLeavesInRegion(LeafRow row, Region region, int n, LeafKind kind, LeafPlannerSettings s)
    {
        float w = s.platformSize;
        float step = w + s.platformClearance;
        float required = n * w + (n - 1) * s.platformClearance;
        float slack = Math.Max(0f, region.width - required);

        float[] shifts = new float[n];
        for (int i = 0; i < n; i++) shifts[i] = (float)(rng.NextDouble() * slack);
        Array.Sort(shifts);

        for (int i = 0; i < n; i++)
        {
            LeafSpec leaf = new LeafSpec();
            leaf.width = w;
            leaf.x = region.start + shifts[i] + i * step + w * 0.5f;
            leaf.kind = kind;
            leaf.isTrunk = false;
            row.leaves.Add(leaf);
        }
    }

    // ------------------------------------------------------------------
    // Validation (F) and repair (B)
    // ------------------------------------------------------------------

    private void RepairAndValidate(LeafRow row, PlannedTrunk next, LeafPlannerSettings s)
    {
        // 1. Every solid leaf must reach the next row's trunk. Anything that can't becomes withering.
        List<int> noExit = LeafValidator.FindLeavesWithoutExit(row, next.x, s.reach);
        foreach (int i in noExit)
        {
            LeafSpec leaf = row.leaves[i];
            if (leaf.isTrunk || !s.demoteUnsafeLeaves)
            {
                Warn(row, "Solid leaf at x=" + leaf.x.ToString("F2") + " has no exit (distance to next trunk "
                    + Math.Abs(leaf.x - next.x).ToString("F2") + " > reach " + s.reach.ToString("F2") + ")."
                    + (leaf.isTrunk ? " It is the path leaf, so it can't be demoted." : ""));
            }
            else
            {
                leaf.kind = LeafKind.Withering;
                DemotedLeaves++;
                Warn(row, "Leaf at x=" + leaf.x.ToString("F2") + " had no exit and was demoted to a withering leaf.");
            }
        }

        // 2. Layout sanity: inside the camera, no overlaps, clearance respected
        List<string> layout = new List<string>();
        LeafValidator.CheckLayout(row, s, layout);
        foreach (string msg in layout) Warn(row, msg);

        // 3. Previous row -> this row: every solid leaf up there must still have an exit here
        if (previousRow != null)
        {
            List<string> step = new List<string>();
            LeafValidator.CheckStep(previousRow, row, s.reach, step);
            foreach (string msg in step) Warn(row, msg);
        }
    }

    private void Warn(LeafRow row, string message)
    {
        Warnings.Add("[SpawnLeaf seed " + Seed + ", level " + row.level + "] " + message);
    }

    // ------------------------------------------------------------------
    // Small helpers (System only, so this file has no Unity dependency)
    // ------------------------------------------------------------------

    private float RandomRange(float a, float b)
    {
        return a + (float)rng.NextDouble() * (b - a);
    }

    private static float Clamp(float v, float lo, float hi)
    {
        return v < lo ? lo : (v > hi ? hi : v);
    }
}