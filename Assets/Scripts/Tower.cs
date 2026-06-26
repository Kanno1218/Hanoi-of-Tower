using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Tower : MonoBehaviour
{
    [Header("基準点（地面の中心）。未設定なら transform.position")]
    public Transform snapOrigin;

    [Header("任意の微調整")]
    public float snapYOffset = 0f;
    public float snapRadius = 0.6f; // この半径以内に離したらスナップ候補

    [Header("Runtime (下→上)")]
    public List<Disc> stack = new List<Disc>();

    Vector3 BasePos => snapOrigin ? snapOrigin.position : transform.position;
    float SumHeights => stack.Sum(d => d.Height);

    /// <summary>
    /// 次に置くディスクを指定して積む位置を返す（高さをそのディスクの高さで計算）
    /// </summary>
    public Vector3 GetPlacePosition(Disc nextDisc)
    {
        var basePos = BasePos;
        float nextH = nextDisc ? nextDisc.Height : 0f;

        Debug.Log(
            $"[Tower:{name}] " +
            $"stackCount={stack.Count}, " +
            $"SumHeights={SumHeights}, " +
            $"nextH={nextH}, " +
            $"baseY={basePos.y}"
        );

        float y = basePos.y + SumHeights + (nextH * 0.5f) + snapYOffset + 0.001f * stack.Count;

        return new Vector3(basePos.x, y, basePos.z);
    }


    /// <summary>
    /// 互換用：引数なし版（DragDropController 互換）
    /// → すでに積まれている最上段の「上」を返す
    /// </summary>
    public Vector3 GetPlacePosition()
    {
        // 最上段の上面（次のディスク高さ0想定の中心Y）を返す
        var basePos = BasePos;
        float y = basePos.y + SumHeights + snapYOffset;
        return new Vector3(basePos.x, y, basePos.z);
    }

    public Disc Peek() => stack.Count > 0 ? stack[^1] : null;

    public bool IsTop(Disc disc) => Peek() == disc;

    public bool TryPopTop(Disc disc)
    {
        if (!IsTop(disc)) return false;
        stack.RemoveAt(stack.Count - 1);
        UpdateTopOnlyGrabbable();
        return true;
    }


    public bool CanPlace(Disc disc)
    {
        if (stack.Count == 0) return true;
        var top = stack[^1];
        return disc.sizeIndex < top.sizeIndex; // ルール：小さいもののみ上に置ける
    }

    public void Push(Disc disc)
    {
        stack.Add(disc);
        disc.currentTower = this;
        disc.CacheAsValid();

        UpdateTopOnlyGrabbable();
    }

    public Disc Pop()
    {
        if (stack.Count == 0) return null;
        var d = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        UpdateTopOnlyGrabbable();
        return d;
    }

    public float DistanceXZ(Vector3 worldPos)
    {
        var p = BasePos;
        var a = new Vector2(p.x, p.z);
        var b = new Vector2(worldPos.x, worldPos.z);
        return Vector2.Distance(a, b);
    }

    public void UpdateTopOnlyGrabbable()
    {
        for (int i = 0; i < stack.Count; i++)
        {
            bool isTop = (i == stack.Count - 1);
            stack[i].SetGrabbable(isTop);
        }
    }


    public bool IsWithinSnap(Vector3 worldPos) => DistanceXZ(worldPos) <= snapRadius;
}
