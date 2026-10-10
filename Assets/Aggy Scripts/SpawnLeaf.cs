using System.Collections.Generic;
using UnityEngine;

public class SpawnLeaf : MonoBehaviour
{
    public GameObject prefab;
    public Transform spawnPoint;

    [Header("Platform settings")]
    public float platformSize;
    public float platformClearance;             // minimum gap between platforms
    public float playerMaxHorizontalJumpLength; // center to center
    public int amountOfLeavesPerYLevel = 1;

    [Header("Camera bounds")]
    public Camera cam;                          // leave empty to use Camera.main
    public float edgePadding = 0f;              // extra margin from the screen edge

    [Header("Timing")]
    public float interval = 1f;
    public bool Spawning = true;

    [Header("Debug")]
    public Transform leftSide;
    public Transform rightSide;

    private float platformXPosition;            // X of the previous path leaf
    private float timer = 0f;
    private int leafLevel = 0;
    private int lastPathLevel = -1;             // last level a path leaf was calculated for

    // Camera edges in world space (edgePadding already applied)
    private float camLeftEdge;
    private float camRightEdge;
    // Limits for leaf CENTERS (edges pulled in by half a leaf)
    private float minX;
    private float maxX;

    private void Start()
    {
        if (cam == null) cam = Camera.main;
        UpdateBounds();

        UpdatePreviousSpawnPoint();
        findRange(platformXPosition - (platformSize / 2), platformXPosition + (platformSize / 2));
    }

    private void Update()
    {
        Timer();
    }

    public void CreateLeaf(Transform leafSpawnPoint)
    {
        Instantiate(prefab, leafSpawnPoint.position, leafSpawnPoint.rotation);
    }

    public void CreateLeafAt(float x)
    {
        // Same Y / Z / rotation as the path leaf, only X differs
        Vector3 pos = new Vector3(x, spawnPoint.position.y, spawnPoint.position.z);
        Instantiate(prefab, pos, spawnPoint.rotation);
    }

    public void ChangeSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
    }

    public void Timer()
    {
        if (!Spawning) return;
        timer += Time.deltaTime;

        if (timer >= interval)
        {
            timer -= interval;
            SpawnLeaves(amountOfLeavesPerYLevel, leafLevel);
            leafLevel++;
        }
    }

    public void SpawnLeaves(int numberOfLeaves, int levelNumber)
    {
        // 1. Path leaf: the one the player is guaranteed to be able to reach
        CalculateNewLeafPosition(levelNumber);
        CreateLeaf(spawnPoint);
        UpdatePreviousSpawnPoint();

        // 2. Everything else goes randomly into the free space around the path leaf
        SpawnExtraLeaves(numberOfLeaves - 1);
    }

    // ------------------------------------------------------------------
    // Camera bounds
    // ------------------------------------------------------------------
    private void UpdateBounds()
    {
        // Distance from the camera to the leaf plane along the camera's forward axis.
        float depth = Vector3.Dot(spawnPoint.position - cam.transform.position, cam.transform.forward);

        float leftEdge = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth)).x;
        float rightEdge = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth)).x;

        camLeftEdge = leftEdge + edgePadding;
        camRightEdge = rightEdge - edgePadding;

        // Leaf positions are centers, so pull the limits in by half a leaf
        minX = camLeftEdge + platformSize / 2f;
        maxX = camRightEdge - platformSize / 2f;
    }

    // ------------------------------------------------------------------
    // Path leaf
    // ------------------------------------------------------------------
    private void CalculateNewLeafPosition(int currentLeafLevel)
    {
        // The path leaf was already placed for this level
        if (currentLeafLevel == lastPathLevel) return;
        lastPathLevel = currentLeafLevel;

        UpdateBounds(); // keeps walls correct if the camera moves or the window is resized

        float minOffset = platformSize + platformClearance;                    // closest valid center-to-center distance
        float maxOffset = Mathf.Max(minOffset, playerMaxHorizontalJumpLength); // farthest the spider can reach
        float half = platformSize / 2f;

        // Only offer directions that still fit inside the walls
        List<int> options = new List<int> { 2 }; // center is always possible
        if (platformXPosition - minOffset >= minX) options.Add(1); // left
        if (platformXPosition + minOffset <= maxX) options.Add(3); // right

        int position = options[Random.Range(0, options.Count)];

        float newX;
        switch (position)
        {
            case 1: // left
                {
                    float far = Mathf.Max(platformXPosition - maxOffset, minX);
                    float near = platformXPosition - minOffset;
                    newX = Random.Range(far, near);
                    findRange(far, near);
                    break;
                }
            case 3: // right
                {
                    float near = platformXPosition + minOffset;
                    float far = Mathf.Min(platformXPosition + maxOffset, maxX);
                    newX = Random.Range(near, far);
                    findRange(near, far);
                    break;
                }
            default: // center
                {
                    float left = Mathf.Max(platformXPosition - half, minX);
                    float right = Mathf.Min(platformXPosition + half, maxX);
                    newX = Random.Range(left, right);
                    findRange(left, right);
                    break;
                }
        }

        spawnPoint.position = new Vector3(newX, spawnPoint.position.y, spawnPoint.position.z);
    }

    // ------------------------------------------------------------------
    // Extra leaves
    // ------------------------------------------------------------------
    private void SpawnExtraLeaves(int count)
    {
        if (count <= 0) return;

        float half = platformSize / 2f;
        float pathX = spawnPoint.position.x;

        // Free regions on each side of the path leaf, as [start, start + width].
        // Clearance is already removed next to the path leaf.
        float leftStart = camLeftEdge;
        float leftEnd = pathX - half - platformClearance;
        float rightStart = pathX + half + platformClearance;
        float rightEnd = camRightEdge;

        float leftWidth = Mathf.Max(0f, leftEnd - leftStart);
        float rightWidth = Mathf.Max(0f, rightEnd - rightStart);

        int leftCapacity = GetCapacity(leftWidth);
        int rightCapacity = GetCapacity(rightWidth);

        int leftCount, rightCount;
        DistributeLeaves(count, leftWidth, rightWidth, leftCapacity, rightCapacity,
                         out leftCount, out rightCount);

        PlaceLeavesInRegion(leftCount, leftStart, leftWidth);
        PlaceLeavesInRegion(rightCount, rightStart, rightWidth);
    }

    // How many platforms fit in a region of this width (with clearance between them)
    private int GetCapacity(float regionWidth)
    {
        float step = platformSize + platformClearance;
        if (step <= 0f || regionWidth < platformSize) return 0;
        // n * size + (n - 1) * clearance <= width   ->   n <= (width + clearance) / step
        return Mathf.FloorToInt((regionWidth + platformClearance) / step + 0.0001f);
    }

    // Decides how many of the extra leaves go left and how many go right
    private void DistributeLeaves(int count, float leftWidth, float rightWidth,
                                  int leftCapacity, int rightCapacity,
                                  out int leftCount, out int rightCount)
    {
        leftCount = 0;
        rightCount = 0;

        if (leftCapacity <= 0 && rightCapacity <= 0) return; // nowhere to put anything

        bool leftIsSmaller = leftWidth <= rightWidth;
        float smallWidth = leftIsSmaller ? leftWidth : rightWidth;
        float bigWidth = leftIsSmaller ? rightWidth : leftWidth;
        int smallCapacity = leftIsSmaller ? leftCapacity : rightCapacity;
        int bigCapacity = leftIsSmaller ? rightCapacity : leftCapacity;

        // Smaller side only gets leaves if it can fit at least one
        int smallCount = 0;
        if (smallCapacity > 0)
        {
            // Share proportional to the space; exactly .5 rounds down, so
            // 2 leaves with a 1:3 space split both go to the bigger side.
            float share = count * smallWidth / (smallWidth + bigWidth);
            smallCount = Mathf.CeilToInt(share - 0.5f - 0.001f);
            smallCount = Mathf.Clamp(smallCount, 0, smallCapacity);
        }

        int bigCount = count - smallCount;

        // If the bigger side is full, push the overflow to the smaller side
        if (bigCount > bigCapacity)
        {
            int overflow = bigCount - bigCapacity;
            bigCount = bigCapacity;
            smallCount = Mathf.Min(smallCount + overflow, smallCapacity);
        }

        leftCount = leftIsSmaller ? smallCount : bigCount;
        rightCount = leftIsSmaller ? bigCount : smallCount;
    }

    // Places n leaves at random spots in a region without overlapping and
    // keeping at least platformClearance between them.
    private void PlaceLeavesInRegion(int n, float regionStart, float regionWidth)
    {
        if (n <= 0) return;

        float half = platformSize / 2f;
        float step = platformSize + platformClearance;

        // Space that is left over once n leaves are packed tightly together
        float required = n * platformSize + (n - 1) * platformClearance;
        float slack = Mathf.Max(0f, regionWidth - required);

        // Pick n random shifts inside the slack and sort them. Leaf i is pushed
        // right by its shift, on top of its tightly-packed position. Sorted shifts
        // can only grow, so leaves never overlap and the gap never drops below clearance.
        float[] shifts = new float[n];
        for (int i = 0; i < n; i++)
        {
            shifts[i] = Random.Range(0f, slack);
        }
        System.Array.Sort(shifts);

        for (int i = 0; i < n; i++)
        {
            float leftEdge = regionStart + shifts[i] + i * step;
            CreateLeafAt(leftEdge + half);
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    public float GetRandomNumberFloat(float min, float max)
    {
        return Random.Range(min, max); // float version is already inclusive
    }

    public int GetRandomNumberInt(int min, int max)
    {
        return Random.Range(min, max + 1); // +1 because the int version excludes max
    }

    public void UpdatePreviousSpawnPoint()
    {
        platformXPosition = spawnPoint.position.x;
    }

    public int LeftRightCenter()
    {
        return GetRandomNumberInt(1, 3);
    }

    public void findRange(float left, float right)
    {
        if (leftSide != null)
            leftSide.position = new Vector3(left, leftSide.position.y, leftSide.position.z);
        if (rightSide != null)
            rightSide.position = new Vector3(right, rightSide.position.y, rightSide.position.z);
    }
}