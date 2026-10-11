using System.Collections;
using UnityEngine;

// ======================================================================
//  WitheringLeaf.cs   (B: make unsafe leaves impermanent)
//  Added to leaves at runtime by SpawnLeaf. You do NOT need to put it on
//  the prefab (but you can, SpawnLeaf will reuse it).
//
//  Behaviour: the first time the player lands on it, it starts to rot
//  (tint fades from idle to decay colour), then it lets go of its
//  colliders so the spider drops, fades out and destroys itself.
//  Once stepped on it is doomed, even if the spider jumps off.
//
//  Works with solid colliders (OnCollisionEnter2D) and trigger colliders
//  (OnTriggerEnter2D). The spider (or its Rigidbody2D object) must carry
//  the tag set in SpawnLeaf -> Player Tag.
// ======================================================================

public class WitheringLeaf : MonoBehaviour
{
    private float witherDelay = 0.8f;
    private float fadeTime = 0.35f;
    private Color idleTint = Color.white;
    private Color decayTint = Color.gray;
    private string playerTag = "Player";

    // How far below the leaf's top the player's feet may be and still count as "landed on it"
    private const float LandingTolerance = 0.15f;

    private SpriteRenderer[] renderers;
    private Color[] baseColors;
    private Collider2D[] colliders;
    private bool withering;

    private void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>();
        colliders = GetComponentsInChildren<Collider2D>();

        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
    }

    public void Configure(float delay, float fade, Color idle, Color decay, string tag)
    {
        witherDelay = Mathf.Max(0.0001f, delay);
        fadeTime = Mathf.Max(0.0001f, fade);
        idleTint = idle;
        decayTint = decay;
        playerTag = tag;

        ApplyTint(idleTint, 1f);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryStartWither(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartWither(other);
    }

    private void TryStartWither(Collider2D other)
    {
        if (withering) return;

        GameObject owner = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (!owner.CompareTag(playerTag)) return;

        // Only landing from above counts, not bumping into it from below or the side
        if (other.bounds.min.y < GetLeafTop() - LandingTolerance) return;

        withering = true;
        StartCoroutine(WitherRoutine());
    }

    private float GetLeafTop()
    {
        float top = transform.position.y;
        bool found = false;
        foreach (Collider2D c in colliders)
        {
            if (c == null || !c.enabled) continue;
            float y = c.bounds.max.y;
            if (!found || y > top) top = y;
            found = true;
        }
        return top;
    }

    private IEnumerator WitherRoutine()
    {
        // Rot: idle colour -> decay colour while the spider can still use the leaf
        float t = 0f;
        while (t < witherDelay)
        {
            t += Time.deltaTime;
            ApplyTint(Color.Lerp(idleTint, decayTint, t / witherDelay), 1f);
            yield return null;
        }

        // Let go: nothing to stand on any more
        foreach (Collider2D c in colliders)
        {
            if (c != null) c.enabled = false;
        }

        // Fade out, then clean up
        t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            ApplyTint(decayTint, 1f - Mathf.Clamp01(t / fadeTime));
            yield return null;
        }

        Destroy(gameObject);
    }

    private void ApplyTint(Color tint, float alpha)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = baseColors[i] * tint;
            c.a = baseColors[i].a * tint.a * alpha;
            renderers[i].color = c;
        }
    }
}