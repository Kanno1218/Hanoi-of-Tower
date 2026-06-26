using UnityEngine;

public class CanvasFollowCamera : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;

    [Header("Placement")]
    public float distance = 1.5f;      // カメラ前方の距離(m)
    public float heightOffset = 0.0f;  // 少し上げたいなら 0.1 など

    [Header("Smoothing")]
    public bool smooth = true;
    public float positionLerp = 12f;   // 大きいほど追従が速い
    public float rotationLerp = 12f;

    [Header("Rotation")]
    public bool keepUpright = true;    // 首の傾き(roll)を無視して水平維持

    void Reset()
    {
        // だいたい Main Camera を自動で拾う
        if (Camera.main != null) cameraTransform = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;

        // 目標位置：カメラ前方 distance + 高さオフセット
        Vector3 targetPos =
            cameraTransform.position +
            cameraTransform.forward * distance +
            Vector3.up * heightOffset;

        // 目標回転：Canvasがカメラの方を向く
        Vector3 toCamera = (cameraTransform.position - targetPos);
        if (keepUpright) toCamera.y = 0f; // 水平維持（好みでON/OFF）
        if (toCamera.sqrMagnitude < 1e-6f) return;

        Quaternion targetRot = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);

        if (smooth)
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, 1f - Mathf.Exp(-positionLerp * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-rotationLerp * Time.deltaTime));
        }
        else
        {
            transform.position = targetPos;
            transform.rotation = targetRot;
        }
    }
}
