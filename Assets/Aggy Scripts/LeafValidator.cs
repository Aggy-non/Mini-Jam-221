using System;
using System.Collections.Generic;

// ======================================================================
//  LeafValidator.cs   (F: verify automatically)
//  Static checks, no Unity dependency. Each check returns / appends
//  human-readable problems; LeafPlanner decides what to do about them.
// ======================================================================

public static class LeafValidator
{
    private const float Eps = 0.001f;

    /// <summary>Indices of SOLID leaves in this row that can't reach the next row's trunk.</summary>
    public static List<int> FindLeavesWithoutExit(LeafRow row, float nextTrunkX, float reach)
    {
        List<int> result = new List<int>();
        for (int i = 0; i < row.leaves.Count; i++)
        {
            LeafSpec leaf = row.leaves[i];
            if (leaf.kind != LeafKind.Solid) continue;
            if (Math.Abs(leaf.x - nextTrunkX) > reach + Eps) result.Add(i);
        }
        return result;
    }

    /// <summary>Every solid leaf of `previous` must have a solid leaf in `current` within reach.</summary>
    public static void CheckStep(LeafRow previous, LeafRow current, float reach, List<string> problems)
    {
        foreach (LeafSpec from in previous.leaves)
        {
            if (from.kind != LeafKind.Solid) continue;

            bool hasExit = false;
            foreach (LeafSpec to in current.leaves)
            {
                if (to.kind == LeafKind.Solid && Math.Abs(to.x - from.x) <= reach + Eps)
                {
                    hasExit = true;
                    break;
                }
            }

            if (!hasExit)
            {
                problems.Add("Dead end: solid leaf at x=" + from.x.ToString("F2") + " (level " + previous.level
                    + ") has no solid leaf within reach " + reach.ToString("F2") + " in level " + current.level + ".");
            }
        }
    }

    /// <summary>Leaves inside the camera, never overlapping, never closer than clearance.</summary>
    public static void CheckLayout(LeafRow row, LeafPlannerSettings s, List<string> problems)
    {
        for (int i = 0; i < row.leaves.Count; i++)
        {
            LeafSpec a = row.leaves[i];
            float half = a.width * 0.5f;

            if (a.x - half < s.camLeftEdge - Eps || a.x + half > s.camRightEdge + Eps)
            {
                problems.Add("Leaf at x=" + a.x.ToString("F2") + " sticks out of the camera bounds.");
            }

            for (int j = i + 1; j < row.leaves.Count; j++)
            {
                LeafSpec b = row.leaves[j];
                float gap = Math.Abs(a.x - b.x) - (a.width + b.width) * 0.5f;
                if (gap < s.platformClearance - Eps)
                {
                    problems.Add("Leaves at x=" + a.x.ToString("F2") + " and x=" + b.x.ToString("F2")
                        + " are closer than the clearance (gap " + gap.ToString("F2") + ").");
                }
            }
        }
    }

    /// <summary>Inspector settings that make the guarantee impossible or weaken it.</summary>
    public static void CheckSettings(LeafPlannerSettings s, List<string> problems)
    {
        float screenWidth = s.camRightEdge - s.camLeftEdge;

        if (s.platformSize <= 0f) problems.Add("platformSize must be greater than 0.");
        if (s.reach <= 0f) problems.Add("Jump reach is 0 or negative (check playerMaxHorizontalJumpLength and reachSafety).");
        if (screenWidth < s.platformSize) problems.Add("The camera is narrower than one leaf.");

        if (s.reach > 0f && s.platformSize > 0f && s.platformSize + s.platformClearance > s.reach)
        {
            problems.Add("platformSize + platformClearance (" + (s.platformSize + s.platformClearance).ToString("F2")
                + ") is larger than the comfort reach (" + s.reach.ToString("F2")
                + "). Side hops are impossible, so the path will only move straight up. "
                + "Raise the jump length / reachSafety or lower the clearance.");
        }

        if (s.minSolidLeaves < 1) problems.Add("minSolidLeaves is below 1; the path leaf always exists, so 1 is used.");
        if (s.maxSolidLeaves < s.minSolidLeaves) problems.Add("maxSolidLeaves is below minSolidLeaves; minSolidLeaves is used.");
        if (s.maxWitheringLeaves < s.minWitheringLeaves) problems.Add("maxWitheringLeaves is below minWitheringLeaves; minWitheringLeaves is used.");
    }
}