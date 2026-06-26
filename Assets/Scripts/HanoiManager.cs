using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HanoiManager : MonoBehaviour
{
    public static HanoiManager Instance { get; private set; }

    [Header("参照")]
    public Tower towerA;
    public Tower towerB;
    public Tower towerC;
    public GameObject discPrefab;

    [Header("設定")]
    [Range(1, 10)] public int discCount = 5;
    public float discHeight = 0.05f;
    public float discRadiusMax = 0.45f;
    public float discRadiusMin = 0.18f;

    [Header("進捗用")]
    public int goalTowerIndex = 2;

    public int TotalDiscs => discCount;

    List<Tower> towers;

    void Awake()
    {
        Instance = this;
        towers = new List<Tower> { towerA, towerB, towerC };
    }

    void Start()
    {
        // ResetGame();
    }

    public void ResetGame()
    {
        Debug.Log("Game Reset");

        foreach (var d in FindObjectsOfType<Disc>())
            Destroy(d.gameObject);

        foreach (var t in towers)
            t.stack.Clear();

        for (int i = discCount; i >= 1; i--)
        {
            var go = Instantiate(discPrefab);
            go.name = $"Disc_{i}";
            ApplyDiscColor(go, i, discCount);

            var disc = go.GetComponent<Disc>();
            if (!disc) disc = go.AddComponent<Disc>();

            disc.sizeIndex = i;
            disc.fixedHeight = discHeight;

            float t01 = (discCount <= 1) ? 0f : (i - 1) / (float)(discCount - 1);
            float radius = Mathf.Lerp(discRadiusMin, discRadiusMax, t01);
            go.transform.localScale = new Vector3(radius * 2f, discHeight * 0.5f, radius * 2f);

            var rb = go.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }

            PlaceOnTowerImmediate(disc, towerA);

            var grabbable = go.GetComponent<OVRGrabbable>();
            StartCoroutine(EnableAfterInit(rb, grabbable));
        }
    }

    IEnumerator EnableAfterInit(Rigidbody rb, OVRGrabbable grabbable)
    {
        yield return null;
        if (grabbable) grabbable.enabled = true;
        if (rb) rb.detectCollisions = true;
    }

    void ApplyDiscColor(GameObject discObj, int index, int total)
    {
        float t = (total <= 1) ? 0f : (float)index / (total - 1);
        Color c = Color.HSVToRGB(t * 0.85f, 0.85f, 0.95f);

        var rens = discObj.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rens)
        {
            if (r && r.material) r.material.color = c;
        }
    }

    void PlaceOnTowerImmediate(Disc disc, Tower tower)
    {
        Debug.Log($"[Before] {tower.name} count={tower.stack.Count}");
        var rb = disc.rb ? disc.rb : disc.GetComponent<Rigidbody>();

        rb.detectCollisions = false;

        int level = tower.stack.Count;
        float h = discHeight;

        var origin = tower.snapOrigin ? tower.snapOrigin : tower.transform;
        Vector3 pos = origin.position;
        pos.y += h * (level + 0.5f);

        Quaternion rot = Quaternion.Euler(0f, origin.eulerAngles.y, 0f);
        disc.transform.SetPositionAndRotation(pos, rot);

        rb.isKinematic = true;
        rb.position = pos;
        rb.rotation = rot;
        rb.Sleep();

        disc.currentTower = tower;
        tower.Push(disc);

        Debug.Log($"[After ] {tower.name} count={tower.stack.Count}");
    }

    public float snapRadius = 0.25f;

    public void TryPlaceDisc(Disc disc, Tower overrideTarget = null)
    {
        if (!disc) return;

        Tower original = disc.lastValidTower != null ? disc.lastValidTower : disc.currentTower;
        Tower target = overrideTarget;

        if (target == null)
        {
            target = towers
                .Where(t => t.DistanceXZ(disc.transform.position) <= snapRadius)
                .OrderBy(t => t.DistanceXZ(disc.transform.position))
                .FirstOrDefault();
        }

        if (target == null)
        {
            RevertToTowerTop(disc, original);

            HanoiSessionManager.Instance?.LogEvent(
                "MOVE_FAIL",
                disc,
                original,
                null,
                "reason=no_snap_target"
            );
            return;
        }

        if (target.CanPlace(disc))
        {
            PlaceDiscOnTower(disc, target);

            HanoiSessionManager.Instance?.LogEvent(
                "MOVE_SUCCESS",
                disc,
                original,
                target,
                ""
            );
        }
        else
        {
            RevertToTowerTop(disc, original);

            HanoiSessionManager.Instance?.LogEvent(
                "MOVE_FAIL",
                disc,
                original,
                target,
                "reason=size_rule"
            );
        }
    }

    void SnapToTower(Disc disc, Tower tower)
    {
        PlaceDiscOnTower(disc, tower);
    }

    public void PlaceDiscOnTower(Disc disc, Tower tower)
    {
        if (disc.currentTower != null && disc.currentTower != tower)
        {
            var prev = disc.currentTower;

            if (prev.IsTop(disc)) prev.Pop();
            else prev.stack.Remove(disc);

            prev.UpdateTopOnlyGrabbable();
        }

        var origin = tower.snapOrigin != null ? tower.snapOrigin : tower.transform;

        int level = tower.stack.Count;
        float h = discHeight;

        Vector3 pos = origin.position;
        pos.y += h * (level + 0.5f);

        Quaternion rot = Quaternion.Euler(0f, origin.eulerAngles.y, 0f);

        disc.transform.SetPositionAndRotation(pos, rot);

        var rb = disc.rb;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        disc.currentTower = tower;
        tower.Push(disc);
        disc.CacheAsValid();
        disc.lastValidTower = tower;

        tower.UpdateTopOnlyGrabbable();
    }

    void RevertToTowerTop(Disc disc, Tower tower)
    {
        if (tower == null) return;

        var origin = (tower.snapOrigin != null) ? tower.snapOrigin : tower.transform;

        int level = tower.stack.Count;
        float h = discHeight;

        Vector3 pos = origin.position;
        pos.y += h * (level + 0.5f);

        Quaternion rot = Quaternion.Euler(0f, origin.eulerAngles.y, 0f);
        disc.transform.SetPositionAndRotation(pos, rot);

        disc.currentTower = tower;

        var rb = disc.rb != null ? disc.rb : disc.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        tower.Push(disc);

        disc.lastValidTower = tower;
        disc.CacheAsValid();
    }

    // ===== 進捗ログ用に追加 =====
    public int[] GetCurrentRodState()
    {
        int[] rodState = new int[discCount];

        for (int rodIndex = 0; rodIndex < towers.Count; rodIndex++)
        {
            Tower tower = towers[rodIndex];

            foreach (Disc disc in tower.stack)
            {
                if (disc == null) continue;

                int idx = disc.sizeIndex - 1; // sizeIndex は 1始まり
                if (idx >= 0 && idx < rodState.Length)
                {
                    rodState[idx] = rodIndex;
                }
            }
        }

        return rodState;
    }
}