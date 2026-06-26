using UnityEngine;

public class OVRTriggerDebug : MonoBehaviour
{
    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
            Debug.Log("PrimaryIndexTrigger DOWN");

        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
            Debug.Log("SecondaryIndexTrigger DOWN");
    }
}
