using UnityEngine;

public class RayHitDebug : MonoBehaviour
{
    public float length = 0.1f;
    public LayerMask mask = ~0;

    void Update()
    {
        var ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out var hit, length, mask, QueryTriggerInteraction.Ignore))
        {
            Debug.Log($"[RayHit] {hit.collider.name}  layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}  dist={hit.distance:F2}");
        }
    }
}
