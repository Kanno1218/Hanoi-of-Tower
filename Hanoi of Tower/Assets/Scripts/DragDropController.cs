using UnityEngine;

public class DragDropController : MonoBehaviour
{
    public Camera cam;
    public LayerMask discMask = ~0;
    public HanoiManager manager;

    Disc heldDisc = null;
    Tower fromTower = null;
    float holdHeight = 1.3f;

    void Start()
    {
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        if (manager && manager.isAutoSolving) return;

        if (Input.GetMouseButtonDown(0)) TryPick();
        else if (Input.GetMouseButton(0) && heldDisc) Drag();
        else if (Input.GetMouseButtonUp(0) && heldDisc) TryDrop();
    }

    void TryPick()
    {
        if (heldDisc) return;

        // 複数ヒットを全部調べる（上のCollider優先で選ぶ）
        Ray ray = cam.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, discMask);

        if (hits.Length == 0) return;

        // 一番手前（カメラに最も近い）ヒットを選ぶ
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            var disc = hit.collider.GetComponentInParent<Disc>();
            if (!disc) continue;

            Tower t = NearestTower(disc.transform.position);

            // その塔の最上段だけ掴める
            if (t.stack.Count > 0 && t.stack[^1] == disc)
            {
                heldDisc = disc;
                fromTower = t;
                return;
            }
        }
    }


    void Drag()
    {
        Vector2 screenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        Ray r = cam.ScreenPointToRay(screenPos);

        // 高さは一定（holdHeight）の水平面に投影
        Plane plane = new Plane(Vector3.up, new Vector3(0, holdHeight, 0));
        if (plane.Raycast(r, out float d))
        {
            Vector3 p = r.GetPoint(d);

            // ★ここがポイント：z を塔の z に固定（たぶん 0）
            float towersZ = manager.towerA.transform.position.z;
            p.z = towersZ;

            // （任意）x を塔の左右の範囲にクランプして操作しやすく
            float minX = Mathf.Min(manager.towerA.transform.position.x,
                                   manager.towerB.transform.position.x,
                                   manager.towerC.transform.position.x) - 0.4f;
            float maxX = Mathf.Max(manager.towerA.transform.position.x,
                                   manager.towerB.transform.position.x,
                                   manager.towerC.transform.position.x) + 0.4f;
            p.x = Mathf.Clamp(p.x, minX, maxX);

            // 反映（y は常に holdHeight）
            p.y = holdHeight;
            heldDisc.transform.position = p;
        }
    }


    void TryDrop()
    {
        Tower target = NearestTower(heldDisc.transform.position);

        // しきい値：塔の真横に来たときだけ置ける（吸い付く感）
        float dist = (heldDisc.transform.position - target.transform.position).magnitude;
        if (dist > 0.8f) // 遠すぎるときは戻す
        {
            heldDisc.transform.position = fromTower.GetPlacePosition();
            heldDisc = null; fromTower = null;
            return;
        }

        // ルール判定 ＆ スナップ
        bool ok = manager.TryMoveDisc(fromTower, target, heldDisc);
        if (!ok)
        {
            // 違反なら元の塔の一番上へ
            heldDisc.transform.position = fromTower.GetPlacePosition();
        }
        // OK の場合は TryMoveDisc 内で正しい高さに積まれる

        heldDisc = null;
        fromTower = null;
    }


    bool Raycast(out RaycastHit hit)
    {
        Ray r = cam.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(r, out hit, 100f, discMask);
    }

    Tower NearestTower(Vector3 p)
    {
        Tower[] arr = new[] { manager.towerA, manager.towerB, manager.towerC };
        Tower best = arr[0];
        float bestD = (p - best.transform.position).sqrMagnitude;
        for (int i = 1; i < arr.Length; i++)
        {
            float d = (p - arr[i].transform.position).sqrMagnitude;
            if (d < bestD) { best = arr[i]; bestD = d; }
        }
        return best;
    }
}
