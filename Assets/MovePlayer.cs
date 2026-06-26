using UnityEngine;

public class MovePlayer : MonoBehaviour
{
    [Header("Refs")]
    // HMDの中心（OVRCameraRig なら CenterEyeAnchor、XR Origin なら Camera Offset/Camera）
    public Transform head;

    [Header("Move Params")]
    public float moveSpeed = 2.0f;    // m/s
    public float turnSpeed = 120f;    // deg/s（右スティック横で回転させたいとき）
    public float verticalSpeed = 1.5f;// 上下移動用（必要な場合のみ）

    [Header("Input")]
    public OVRInput.Controller left = OVRInput.Controller.LTouch;
    public OVRInput.Controller right = OVRInput.Controller.RTouch;

    void Reset()
    {
        // 自動で head を推定
        if (!head)
        {
            var cam = Camera.main;
            if (cam) head = cam.transform;
        }
    }

    void Update()
    {
        // === 左スティックで水平移動 ===
        Vector2 moveAxis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, left); // 左スティック
        Vector3 fwd = Vector3.ProjectOnPlane(head ? head.forward : transform.forward, Vector3.up).normalized;
        Vector3 rightDir = Vector3.ProjectOnPlane(head ? head.right : transform.right, Vector3.up).normalized;

        Vector3 planarMove = (fwd * moveAxis.y + rightDir * moveAxis.x) * moveSpeed * Time.deltaTime;
        transform.position += planarMove; // ← リグの“ルート”を動かす

        // === 右スティック横でスムーズ回転（任意） ===
        float turn = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick, right).x;
        if (Mathf.Abs(turn) > 0.1f)
        {
            transform.Rotate(0f, turn * turnSpeed * Time.deltaTime, 0f, Space.Self);
        }

        // === トリガ/ボタンで上下（必要なら） ===
        float upDown = 0f;
        upDown += OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger, right);  // 右グリップで上昇
        upDown -= OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, left);     // 左グリップで下降

        // 追加でA/Bボタンでも
        if (OVRInput.Get(OVRInput.Button.Two, right)) upDown += 1f; // B で上昇
        if (OVRInput.Get(OVRInput.Button.One, right)) upDown -= 1f; // A で下降

        if (Mathf.Abs(upDown) > 0.01f)
        {
            transform.position += Vector3.up * (upDown * verticalSpeed * Time.deltaTime);
        }

        // X/Yでリセット（任意）
        if (OVRInput.GetDown(OVRInput.Button.Three, left) || OVRInput.GetDown(OVRInput.Button.Four, left))
        {
            transform.position = new Vector3(0f, 1.6f, 0f); // 好きな初期位置に
            transform.rotation = Quaternion.identity;
        }
    }
}
