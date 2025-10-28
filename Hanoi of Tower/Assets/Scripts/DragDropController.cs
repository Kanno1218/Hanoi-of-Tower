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

        // �����q�b�g��S�����ׂ�i���Collider�D��őI�ԁj
        Ray ray = cam.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, discMask);

        if (hits.Length == 0) return;

        // ��Ԏ�O�i�J�����ɍł��߂��j�q�b�g��I��
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            var disc = hit.collider.GetComponentInParent<Disc>();
            if (!disc) continue;

            Tower t = NearestTower(disc.transform.position);

            // ���̓��̍ŏ�i�����͂߂�
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

        // �����͈��iholdHeight�j�̐����ʂɓ��e
        Plane plane = new Plane(Vector3.up, new Vector3(0, holdHeight, 0));
        if (plane.Raycast(r, out float d))
        {
            Vector3 p = r.GetPoint(d);

            // ���������|�C���g�Fz �𓃂� z �ɌŒ�i���Ԃ� 0�j
            float towersZ = manager.towerA.transform.position.z;
            p.z = towersZ;

            // �i�C�Ӂjx �𓃂̍��E�͈̔͂ɃN�����v���đ��삵�₷��
            float minX = Mathf.Min(manager.towerA.transform.position.x,
                                   manager.towerB.transform.position.x,
                                   manager.towerC.transform.position.x) - 0.4f;
            float maxX = Mathf.Max(manager.towerA.transform.position.x,
                                   manager.towerB.transform.position.x,
                                   manager.towerC.transform.position.x) + 0.4f;
            p.x = Mathf.Clamp(p.x, minX, maxX);

            // ���f�iy �͏�� holdHeight�j
            p.y = holdHeight;
            heldDisc.transform.position = p;
        }
    }


    void TryDrop()
    {
        Tower target = NearestTower(heldDisc.transform.position);

        // �������l�F���̐^���ɗ����Ƃ������u����i�z���t�����j
        float dist = (heldDisc.transform.position - target.transform.position).magnitude;
        if (dist > 0.8f) // ��������Ƃ��͖߂�
        {
            heldDisc.transform.position = fromTower.GetPlacePosition();
            heldDisc = null; fromTower = null;
            return;
        }

        // ���[������ �� �X�i�b�v
        bool ok = manager.TryMoveDisc(fromTower, target, heldDisc);
        if (!ok)
        {
            // �ᔽ�Ȃ猳�̓��̈�ԏ��
            heldDisc.transform.position = fromTower.GetPlacePosition();
        }
        // OK �̏ꍇ�� TryMoveDisc ���Ő����������ɐς܂��

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
