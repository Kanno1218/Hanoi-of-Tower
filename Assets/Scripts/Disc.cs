using System;
using UnityEngine;

public class Disc : MonoBehaviour
{
    [Tooltip("小さいほど上に置ける（1が最小）。大きいほど下の層向け")]
    public int sizeIndex = 1;
    public Tower lastValidTower;

    [NonSerialized] public Tower currentTower;
    [NonSerialized] public Rigidbody rb;

    [NonSerialized] public float fixedHeight = 0f;

    Vector3 lastValidPosition;
    Quaternion lastValidRotation;

    // ===== 追加：掴み制御用 =====
    private Behaviour ovrGrabbable;          // OVRGrabbable をBehaviourとして保持（参照できなくてもコンパイルOK）
    private Collider[] allColliders;         // 子含む全コライダー
    private Collider[] grabColliders;        // 掴み判定に使うコライダー（Triggerのみ）
    // ==========================

    public float Height
    {
        get
        {
            if (fixedHeight > 0f) return fixedHeight;

            var col = GetComponentInChildren<Collider>();
            if (col) return col.bounds.size.y;

            var r = GetComponentInChildren<Renderer>();
            return r ? r.bounds.size.y : transform.lossyScale.y;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // ===== 追加：OVRGrabbable と GrabVolume を自動取得 =====
        // OVRGrabbable が付いていれば拾う（型参照しないので安全）
        // 「OVRGrabbable」というクラス名のBehaviourを探す
        foreach (var b in GetComponentsInChildren<Behaviour>(true))
        {
            if (b != null && b.GetType().Name == "OVRGrabbable")
            {
                ovrGrabbable = b;
                break;
            }
        }

        allColliders = GetComponentsInChildren<Collider>(true);

        // “掴み用コライダー”は基本 Trigger なので、TriggerだけをGrab判定として扱う
        // （物理用Colliderは非Triggerのはずなので残る）
        grabColliders = Array.FindAll(allColliders, c => c != null && c.isTrigger);
        // ================================================
    }

    /// <summary>
    /// 一番上だけ掴めるようにするための切り替え
    /// </summary>
    public void SetGrabbable(bool canGrab)
    {
        // OVRGrabbable 自体を無効化（付いていれば）
        if (ovrGrabbable != null) ovrGrabbable.enabled = canGrab;

        // GrabVolume(Trigger)だけON/OFF（物理Colliderは触らない）
        if (grabColliders != null)
        {
            foreach (var c in grabColliders)
            {
                if (c != null) c.enabled = canGrab;
            }
        }
    }

    public void CacheAsValid()
    {
        lastValidPosition = transform.position;
        lastValidRotation = transform.rotation;
    }

    public void RevertToLastValid()
    {
        if (lastValidTower == null) return;
        HanoiManager.Instance?.PlaceDiscOnTower(this, lastValidTower);
    }

    public void NotifyReleased()
    {
        HanoiManager.Instance?.TryPlaceDisc(this);
    }
}

