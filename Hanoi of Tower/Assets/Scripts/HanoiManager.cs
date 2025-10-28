using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // ← 必要

public class HanoiManager : MonoBehaviour
{
    // ← UI参照はクラスの“中”に置く
    public TextMeshProUGUI movesText;
    public TextMeshProUGUI goalText;

    [Header("Refs")]
    public Tower towerA;
    public Tower towerB;
    public Tower towerC;
    public GameObject discPrefab;

    [Header("Settings")]
    [Range(3, 10)] public int discCount = 5;
    public float baseRadius = 0.6f;
    public float radiusStep = 0.08f;

    [Header("State")]
    public int moveCount = 0;
    public bool isAutoSolving = false;

    // 互換性のため明示的に new List<Tower>()
    List<Tower> towers = new List<Tower>();

    void Awake()
    {
        towers.Add(towerA);
        towers.Add(towerB);
        towers.Add(towerC);
    }

    void Start()
    {
        ResetGame();
        UpdateUI();
    }

    public void ResetGame()
    {
        foreach (var t in towers)
        {
            foreach (var d in t.stack)
                if (d) Destroy(d.gameObject);
            t.stack.Clear();
        }
        moveCount = 0;
        isAutoSolving = false;

        for (int i = discCount; i >= 1; i--)
        {
            var go = Instantiate(discPrefab);
            go.name = $"Disc_{i}";
            float r = baseRadius - (discCount - i) * radiusStep;
            go.transform.localScale = new Vector3(r, 0.075f, r);

            var disc = go.GetComponent<Disc>();
            disc.size = i;

            towerA.Push(disc);
        }
        UpdateUI();
    }

    public bool TryMoveDisc(Tower from, Tower to, Disc disc)
    {
        if (to.CanPlace(disc))
        {
            if (from.stack.Count == 0 || from.stack[^1] != disc) return false;
            from.Pop();
            to.Push(disc);
            moveCount++;
            UpdateUI();

            if (towerC.stack.Count == discCount)
            {
                if (goalText) goalText.text = "CLEAR!";
            }
            return true;
        }
        return false;
    }

    void UpdateUI()
    {
        if (movesText) movesText.text = $"Moves: {moveCount}";
        if (goalText) goalText.text = $"Goal: {(int)(Mathf.Pow(2, discCount) - 1)}";
    }

    public void SolveAuto()
    {
        if (!isAutoSolving) StartCoroutine(SolveRoutine());
    }

    IEnumerator SolveRoutine()
    {
        isAutoSolving = true;
        yield return HanoiRecursive(discCount, towerA, towerC, towerB);
        isAutoSolving = false;
    }

    IEnumerator HanoiRecursive(int n, Tower from, Tower to, Tower aux)
    {
        if (n <= 0) yield break;
        yield return HanoiRecursive(n - 1, from, aux, to);

        Disc d = from.stack[^1];
        yield return MoveAnimated(from, to, d, 0.5f);

        yield return HanoiRecursive(n - 1, aux, to, from);
    }

    IEnumerator MoveAnimated(Tower from, Tower to, Disc d, float dur)
    {
        Vector3 start = d.transform.position;
        Vector3 up = new Vector3(start.x, 1.4f, start.z);
        Vector3 mid = new Vector3(to.transform.position.x, 1.4f, to.transform.position.z);

        from.Pop();
        to.stack.Add(d);
        Vector3 end = to.GetPlacePosition();

        yield return LerpPos(d.transform, start, up, dur * 0.3f);
        yield return LerpPos(d.transform, up, mid, dur * 0.4f);
        yield return LerpPos(d.transform, mid, end, dur * 0.3f);

        moveCount++;
        UpdateUI(); // ← 自動解法でも手数更新
    }

    IEnumerator LerpPos(Transform t, Vector3 a, Vector3 b, float time)
    {
        float e = 0;
        while (e < time)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / time);
            t.position = Vector3.Lerp(a, b, k);
            yield return null;
        }
        t.position = b;
    }
}
