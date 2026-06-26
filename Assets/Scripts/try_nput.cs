using UnityEngine;

public class OVRButtonTest : MonoBehaviour
{
    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One)) Debug.Log("Button One");
        if (OVRInput.GetDown(OVRInput.Button.Two)) Debug.Log("Button Two");
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger)) Debug.Log("Primary Index Trigger");
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger)) Debug.Log("Secondary Index Trigger");
    }
}
