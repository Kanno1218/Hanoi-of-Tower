using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(Disc))]
[RequireComponent(typeof(Rigidbody))]
public class DiscVRBridge : MonoBehaviour
{
    Disc disc;
    Rigidbody rb;
    Tower grabbedFromTower;
    Component ovrGrabbable;
    PropertyInfo propIsGrabbed;

    // 追加：掴んでる手の推定用
    Component cachedGrabber;                 // OVRGrabber
    FieldInfo grabberFieldController;        // m_controller or controller
    bool? isLeftHandCached = null;           // true=left, false=right, null=unknown

    bool prevGrabbed = false;

    void Awake()
    {
        disc = GetComponent<Disc>();
        rb = GetComponent<Rigidbody>();
        if (disc.rb == null) disc.rb = rb;

        // OVRGrabbable を文字列取得（OVRなしでもコンパイルを通す）
        ovrGrabbable = GetComponent("OVRGrabbable") as Component;
        if (ovrGrabbable != null)
        {
            propIsGrabbed = ovrGrabbable.GetType().GetProperty(
                "isGrabbed",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
        }
    }

    void Start()
    {
        // grabPoints が空なら自動設定（OVRGrabbableがある場合のみ）
        if (ovrGrabbable == null) return;

        var grabPointsField = ovrGrabbable.GetType().GetField(
            "grabPoints",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (grabPointsField == null) return;

        var current = grabPointsField.GetValue(ovrGrabbable) as Collider[];
        if (current != null && current.Length > 0) return;

        var cols = GetComponentsInChildren<Collider>(true);
        if (cols != null && cols.Length > 0)
            grabPointsField.SetValue(ovrGrabbable, cols);
    }

    void Update()
    {
        if (ovrGrabbable == null || propIsGrabbed == null) return;

        bool grabbed = false;
        try { grabbed = (bool)propIsGrabbed.GetValue(ovrGrabbable); }
        catch { /* ignore */ }

        disc.lastValidTower = disc.currentTower;

        // 掴み始め
        if (!prevGrabbed && grabbed)
        {
            EnsureRB();

            // Top以外は掴めない
            var t = disc.currentTower;
            if (t != null && !t.IsTop(disc))
            {
                disc.RevertToLastValid();

                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.FreezeAll;

                // 掴み状態を切る（OVRGrabbableを一瞬無効化）
                if (ovrGrabbable is Behaviour b)
                {
                    b.enabled = false;
                    b.enabled = true;
                }

                prevGrabbed = false;
                return;
            }

            grabbedFromTower = disc.currentTower;  // 掴む直前の塔を保持
            // 正しく掴める場合は stack から外す
            if (t != null) t.TryPopTop(disc);

            // 掴み中はOVRがTransform追従するので物理は止める
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.None;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // ===== 追加：どっちの手で掴んだか推定してセッションに通知 =====
            isLeftHandCached = TryResolveIsLeftHand();
            NotifyGrabStateToSession(isGrabbed: true, isLeftHandCached);

            // （任意）イベントログも残す
            if (HanoiSessionManager.Instance != null)
                HanoiSessionManager.Instance.LogEvent("GRAB_START", disc, disc.lastValidTower, null, $"hand={HandLabel(isLeftHandCached)}");
        }

        // 離した
        if (prevGrabbed && !grabbed)
        {
            EnsureRB();

            // 傾き防止：離した瞬間に水平化（X/Z=0、Y保持）
            ResetRotationHorizontal();

            // 一旦物理へ戻す（NotifyReleased内で置けたらFreezeAllに戻る想定）
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.None;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // ===== 追加：離したことをセッションに通知（掴みディスク解除） =====
            NotifyGrabStateToSession(isGrabbed: false, isLeftHandCached);

            // （任意）イベントログ
            if (HanoiSessionManager.Instance != null)
                HanoiSessionManager.Instance.LogEvent("GRAB_END", disc, disc.lastValidTower, null, $"hand={HandLabel(isLeftHandCached)}");

            // 次の掴みに備えてリセット
            isLeftHandCached = null;
            cachedGrabber = null;
            grabberFieldController = null;

            disc.NotifyReleased();
           

            var sm = HanoiSessionManager.Instance;
            if (sm != null)
            {
                Tower toTower = disc.currentTower; // NotifyReleased後に確定している想定

                if (grabbedFromTower != null && toTower != null && grabbedFromTower != toTower)
                {
                    sm.LogEvent("MOVE", disc, grabbedFromTower, toTower, "ok");
                }
                else if (grabbedFromTower != null && toTower == grabbedFromTower)
                {
                    sm.LogEvent("MOVE_BACK", disc, grabbedFromTower, toTower, "reverted");
                }
                else
                {
                    sm.LogEvent("MOVE_FAIL", disc, grabbedFromTower, toTower, "no_tower");
                }
            }
            grabbedFromTower = null;

        }

        prevGrabbed = grabbed;
    }

    void OnDisable()
    {
        // 念のため：掴み中にDisableされたら離した扱い
        if (prevGrabbed)
        {
            EnsureRB();
            ResetRotationHorizontal();

            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.None;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // 掴み解除通知
            NotifyGrabStateToSession(isGrabbed: false, isLeftHandCached);

            isLeftHandCached = null;
            cachedGrabber = null;
            grabberFieldController = null;

            disc.NotifyReleased();

            var sm = HanoiSessionManager.Instance;
            if (sm != null)
            {
                Tower toTower = disc.currentTower; // NotifyReleased後に確定している想定

                if (grabbedFromTower != null && toTower != null && grabbedFromTower != toTower)
                {
                    sm.LogEvent("MOVE", disc, grabbedFromTower, toTower, "ok");
                }
                else if (grabbedFromTower != null && toTower == grabbedFromTower)
                {
                    sm.LogEvent("MOVE_BACK", disc, grabbedFromTower, toTower, "reverted");
                }
                else
                {
                    sm.LogEvent("MOVE_FAIL", disc, grabbedFromTower, toTower, "no_tower");
                }
            }
            grabbedFromTower = null;

            prevGrabbed = false;
        }
    }

    void EnsureRB()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (disc.rb == null) disc.rb = rb;
    }

    void ResetRotationHorizontal()
    {
        var e = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, e.y, 0f);
    }

    // =========================
    // 追加：セッション連携
    // =========================
    void NotifyGrabStateToSession(bool isGrabbed, bool? isLeft)
    {
        var sm = HanoiSessionManager.Instance;
        if (sm == null) return;

        // 左右が判定できる場合のみ片方を更新
        if (isLeft.HasValue)
        {
            sm.SetGrabbedDisc(isLeft.Value, isGrabbed ? disc : null);
        }
        else
        {
            // 判定できないとき：安全策として両方更新（解析上は曖昧になるが欠損は減る）
            // 片方だけ更新したいなら、このelseブロックを消してOK
            sm.SetGrabbedDisc(true, isGrabbed ? disc : null);
            sm.SetGrabbedDisc(false, isGrabbed ? disc : null);
        }
    }

    string HandLabel(bool? isLeft)
    {
        if (!isLeft.HasValue) return "U"; // unknown
        return isLeft.Value ? "L" : "R";
    }

    // =========================
    // 追加：OVRGrabber から左右判定
    // =========================
    bool? TryResolveIsLeftHand()
    {
        // OVRGrabbable の "grabbedBy" を反射で取得できる場合がある
        // そこから OVRGrabber を辿って m_controller(LTouch/RTouch) を読む
        if (ovrGrabbable == null) return null;

        // 1) grabbedBy を探す（public/protected/private どれでも）
        var grabbedByField = ovrGrabbable.GetType().GetField(
            "grabbedBy",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        object grabbedBy = null;
        if (grabbedByField != null)
        {
            try { grabbedBy = grabbedByField.GetValue(ovrGrabbable); }
            catch { grabbedBy = null; }
        }

        // grabbedBy が取れない場合：子/親から OVRGrabber を探す（保険）
        if (grabbedBy == null)
        {
            cachedGrabber = FindGrabberInParentsOrScene();
        }
        else
        {
            cachedGrabber = grabbedBy as Component;
        }

        if (cachedGrabber == null) return null;

        // 2) grabber の controller フィールド名は実装差があるので候補を探す
        // よくある: "m_controller" / "controller"
        var t = cachedGrabber.GetType();
        grabberFieldController = t.GetField("m_controller", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                             ?? t.GetField("controller", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (grabberFieldController == null) return null;

        object controllerVal = null;
        try { controllerVal = grabberFieldController.GetValue(cachedGrabber); }
        catch { controllerVal = null; }

        if (controllerVal == null) return null;

        // controllerVal は enum のはず（OVRInput.Controller）
        string s = controllerVal.ToString(); // "LTouch" / "RTouch" などになる
        if (s.Contains("L")) return true;
        if (s.Contains("R")) return false;

        return null;
    }

    Component FindGrabberInParentsOrScene()
    {
        // 親に OVRGrabber がいることが多いので上方向に探す
        Transform p = transform.parent;
        while (p != null)
        {
            var c = p.GetComponent("OVRGrabber") as Component;
            if (c != null) return c;
            p = p.parent;
        }

        // 最終手段：シーン内から探す（2つ見つかる可能性があるので不確実）
        // ここでは最初に見つかったものを返す
        var go = GameObject.FindObjectOfType(typeof(Component), true); // ダミー
        // ↑ これは使えないので、OVRがある時だけ探すなら下の代替手段を使う
        // ただし「OVRなしでもコンパイル」は保ちたいので今回は null を返しておく
        return null;
    }
}
