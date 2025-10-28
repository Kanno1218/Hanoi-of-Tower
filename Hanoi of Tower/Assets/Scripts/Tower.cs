using System.Collections.Generic;
using UnityEngine;

public class Tower : MonoBehaviour
{
    public Transform topPoint;      // éqÇ…çÏÇ¡ÇΩTopPoint
    public float discHeight = 0.15f;
    public List<Disc> stack = new List<Disc>(); // â∫Å®è„

    public Vector3 GetPlacePosition()
    {
        int n = stack.Count;
        Vector3 basePos = new Vector3(transform.position.x, 0f, transform.position.z);
        return new Vector3(basePos.x, n * discHeight + discHeight * 0.5f, basePos.z);
    }

    public bool CanPlace(Disc d)
    {
        if (stack.Count == 0) return true;
        return d.size < stack[^1].size;
    }

    public void Push(Disc d)
    {
        stack.Add(d);
        d.transform.position = GetPlacePosition();
    }

    public Disc Pop()
    {
        if (stack.Count == 0) return null;
        Disc top = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return top;
    }
}
