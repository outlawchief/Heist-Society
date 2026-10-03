using UnityEngine;

public class HeistBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
    }
}
