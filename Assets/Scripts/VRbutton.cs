using UnityEngine;

public class VRButtonLogger : MonoBehaviour
{
    [Header("GameRoot配下の HanoiManager を入れる")]
    public HanoiManager hanoiManager;

    private bool isHover = false;
    private bool wasLeftPressed = false;
    private bool wasRightPressed = false;

    void Update()
    {
        if (!isHover) return;

        bool leftPressed = OVRInput.Get(
            OVRInput.Button.PrimaryIndexTrigger,
            OVRInput.Controller.LTouch
        );

        bool rightPressed = OVRInput.Get(
            OVRInput.Button.PrimaryIndexTrigger,
            OVRInput.Controller.RTouch
        );

        // 押した瞬間だけ反応
        if ((leftPressed && !wasLeftPressed) ||
            (rightPressed && !wasRightPressed))
        {
            Debug.Log("Startボタンが押されました");

            if (hanoiManager != null)
            {
                hanoiManager.ResetGame(); // ← ここが重要
            }
            else
            {
                Debug.LogError("HanoiManager が設定されていません");
            }
        }

        wasLeftPressed = leftPressed;
        wasRightPressed = rightPressed;
    }

    private void OnTriggerEnter(Collider other)
    {
        isHover = true;
    }

    private void OnTriggerExit(Collider other)
    {
        isHover = false;
        wasLeftPressed = false;
        wasRightPressed = false;
    }
}
