using System.Collections.Generic;
using UnityEngine;

// ======================================================================
//  SpawnLeaf.cs
//  Thin Unity wrapper around LeafPlanner:
//    - timer, camera bounds, instantiating leaves
//    - turns the planner's rows into GameObjects
//  All the placement logic lives in LeafPlanner.cs, the checks in
//  LeafValidator.cs and the rotting behaviour in WitheringLeaf.cs.
// ======================================================================

public class SpawnLeaf : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject prefab;                   // solid leaf
    public GameObject witheringPrefab;          // optional: different art for withering leaves (falls back to prefab)
    public Transform spawnPoint;                // leaves spawn at its Y/Z/rotation; X is chosen by the generator

    [Header("Platform settings")]
    public float platformSize = 1.5f;           // world width of the leaf prefab (right-click the component to auto-detect)
    public float platformClearance = 0.5f;      // minimum gap between leaves in the same row
    public float playerMaxHorizontalJumpLength = 4f; // physics maximum, center to center
    [Range(0.3f, 1f)]
    public float reachSafety = 0.85f;           // the guarantee uses jump length * this, so it still works from a leaf's edge

    [Header("Leaves per level (soft targets, reachability is the hard rule)")]
    public int minSolidLeaves = 1;              // includes the path leaf
    public int maxSolidLeaves = 3;
    public int minWitheringLeaves = 0;
    public int maxWitheringLeaves = 2;
    [Range(1f, 3f)]
    public float funnelWidthMultiplier = 1.5f;  // width of a lone leaf that follows a bigger row (1 = off)

    [Header("Withering leaves")]
    public float witherDelay = 0.8f;            // seconds between landing and letting go
    public float witherFadeTime = 0.35f;
    public Color witheringIdleTint = new Color(0.95f, 0.85f, 0.45f, 1f);
    public Color witheringDecayTint = new Color(0.45f, 0.30f, 0.15f, 1f);
    public string playerTag = "Player";

    [Header("Camera bounds")]
    public Camera cam;                          // leave empty to use Camera.main
    public float edgePadding = 0f;              // extra margin from the screen edge

    [Header("Timing")]
    public float interval = 1f;
    public bool Spawning = true;

    [Header("Debug")]
    public int seed = 0;                        // 0 = new random seed every run; paste a logged seed to replay a layout
    public bool logWarnings = true;
    public Transform leftSide;                  // markers: range the solid extras of the current row were limited to
    public Transform rightSide;

    /// <summary>Fired after each row is spawned. Leaves in the list are in the same order as row.leaves (index 0 = path leaf).</summary>
    public event System.Action<LeafRow, List<GameObject>> RowSpawned;

    private LeafPlanner planner;
    private readonly LeafPlannerSettings settings = new LeafPlannerSettings();
    private float timer = 0f;
    private float camLeftEdge;
    private float camRightEdge;

    // ------------------------------------------------------------------
    // Unity
    // ------------------------------------------------------------------

    private void Start()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || spawnPoint == null || prefab == null)
        {
            Debug.LogError("SpawnLeaf: assign a prefab, a spawn point and a camera.", this);
            enabled = false;
            return;
        }

        UpdateBounds();
        ApplySettings();

        int actualSeed = seed != 0 ? seed : System.Environment.TickCount;
        if (actualSeed == 0) actualSeed = 1;
        planner = new LeafPlanner(actualSeed, spawnPoint.position.x, platformSize);

        if (logWarnings)
        {
            Debug.Log("SpawnLeaf: generator seed " + actualSeed, this);
            List<string> problems = new List<string>();
            LeafValidator.CheckSettings(settings, problems);
            foreach (string p in problems) Debug.LogWarning("SpawnLeaf settings: " + p, this);
        }
    }

    private void Update()
    {
        Timer();
    }

    private void OnValidate()
    {
        minSolidLeaves = Mathf.Max(1, minSolidLeaves);
        maxSolidLeaves = Mathf.Max(minSolidLeaves, maxSolidLeaves);
        minWitheringLeaves = Mathf.Max(0, minWitheringLeaves);
        maxWitheringLeaves = Mathf.Max(minWitheringLeaves, maxWitheringLeaves);
        platformSize = Mathf.Max(0.01f, platformSize);
        platformClearance = Mathf.Max(0f, platformClearance);
    }

    // ------------------------------------------------------------------
    // Public
    // ------------------------------------------------------------------

    public void ChangeSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
    }

    // ------------------------------------------------------------------
    // Spawning
    // ------------------------------------------------------------------

    private void Timer()
    {
        if (!Spawning || planner == null) return;
        timer += Time.deltaTime;

        if (timer >= interval)
        {
            timer -= interval;
            SpawnRow();
        }
    }

    private void SpawnRow()
    {
        UpdateBounds();     // the camera may have moved or been resized
        ApplySettings();    // lets you tweak values in the inspector while playing

        LeafRow row = planner.NextRow(settings);

        List<GameObject> spawned = new List<GameObject>(row.leaves.Count);
        foreach (LeafSpec leaf in row.leaves)
        {
            spawned.Add(CreateLeaf(leaf));
        }

        // Keep the spawn point on the path leaf, like the old script did
        Vector3 sp = spawnPoint.position;
        spawnPoint.position = new Vector3(row.TrunkX, sp.y, sp.z);

        ShowWindow(row);

        if (logWarnings)
        {
            foreach (string w in planner.Warnings) Debug.LogWarning(w, this);
        }

        if (RowSpawned != null) RowSpawned(row, spawned);
    }

    private GameObject CreateLeaf(LeafSpec leaf)
    {
        bool withering = leaf.kind == LeafKind.Withering;
        GameObject source = (withering && witheringPrefab != null) ? witheringPrefab : prefab;

        Vector3 pos = new Vector3(leaf.x, spawnPoint.position.y, spawnPoint.position.z);
        GameObject go = Instantiate(source, pos, spawnPoint.rotation);

        // Funnel leaves are wider than the prefab
        if (platformSize > 0f && Mathf.Abs(leaf.width - platformSize) > 0.001f)
        {
            Vector3 scale = go.transform.localScale;
            scale.x *= leaf.width / platformSize;
            go.transform.localScale = scale;
        }

        if (withering)
        {
            WitheringLeaf rot = go.GetComponent<WitheringLeaf>();
            if (rot == null) rot = go.AddComponent<WitheringLeaf>();
            rot.Configure(witherDelay, witherFadeTime, witheringIdleTint, witheringDecayTint, playerTag);
        }

        return go;
    }

    // ------------------------------------------------------------------
    // Camera bounds / settings
    // ------------------------------------------------------------------

    private void UpdateBounds()
    {
        // Distance from the camera to the leaf plane along the camera's forward axis.
        float depth = Vector3.Dot(spawnPoint.position - cam.transform.position, cam.transform.forward);

        float leftEdge = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth)).x;
        float rightEdge = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth)).x;

        camLeftEdge = leftEdge + edgePadding;
        camRightEdge = rightEdge - edgePadding;
    }

    private void ApplySettings()
    {
        settings.platformSize = platformSize;
        settings.platformClearance = platformClearance;
        settings.reach = playerMaxHorizontalJumpLength * reachSafety;
        settings.camLeftEdge = camLeftEdge;
        settings.camRightEdge = camRightEdge;
        settings.minSolidLeaves = minSolidLeaves;
        settings.maxSolidLeaves = maxSolidLeaves;
        settings.minWitheringLeaves = minWitheringLeaves;
        settings.maxWitheringLeaves = maxWitheringLeaves;
        settings.funnelWidthMultiplier = funnelWidthMultiplier;
        settings.demoteUnsafeLeaves = true;
    }

    private void ShowWindow(LeafRow row)
    {
        if (leftSide != null)
            leftSide.position = new Vector3(row.windowLeft, leftSide.position.y, leftSide.position.z);
        if (rightSide != null)
            rightSide.position = new Vector3(row.windowRight, rightSide.position.y, rightSide.position.z);
    }

    // ------------------------------------------------------------------
    // Editor tools (right-click the component header)
    // ------------------------------------------------------------------

    [ContextMenu("Run Generator Self-Test (10000 rows)")]
    private void RunSelfTest()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || spawnPoint == null)
        {
            Debug.LogError("SpawnLeaf self-test: assign a spawn point and a camera first.", this);
            return;
        }

        UpdateBounds();
        ApplySettings();

        List<string> configProblems = new List<string>();
        LeafValidator.CheckSettings(settings, configProblems);
        foreach (string p in configProblems) Debug.LogWarning("SpawnLeaf settings: " + p, this);

        const int rows = 10000;
        LeafPlanner test = new LeafPlanner(Random.Range(1, int.MaxValue), spawnPoint.position.x, platformSize);
        int problems = 0;
        int solid = 0;
        int withering = 0;
        int shown = 0;

        for (int i = 0; i < rows; i++)
        {
            LeafRow row = test.NextRow(settings);
            foreach (LeafSpec leaf in row.leaves)
            {
                if (leaf.kind == LeafKind.Solid) solid++; else withering++;
            }
            problems += test.Warnings.Count;
            foreach (string w in test.Warnings)
            {
                if (shown++ < 5) Debug.LogWarning(w, this);
            }
        }

        Debug.Log("SpawnLeaf self-test: " + rows + " rows, " + problems + " problems, "
            + (solid / (float)rows).ToString("F2") + " solid and "
            + (withering / (float)rows).ToString("F2") + " withering leaves per row on average, "
            + test.DemotedLeaves + " leaves demoted. (seed " + test.Seed + ")", this);
    }

    [ContextMenu("Detect Platform Size From Prefab")]
    private void DetectPlatformSize()
    {
        if (prefab == null) return;

        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null)
        {
            Debug.LogWarning("SpawnLeaf: the prefab has no SpriteRenderer with a sprite, set Platform Size by hand.", this);
            return;
        }

        platformSize = sr.sprite.bounds.size.x * sr.transform.lossyScale.x;
        Debug.Log("SpawnLeaf: platform size set to " + platformSize, this);
    }
}