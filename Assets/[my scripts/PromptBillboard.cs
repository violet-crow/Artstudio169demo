using UnityEngine;
public class PromptBillboard : MonoBehaviour
{
Transform cam;
void LateUpdate()
{
if (!cam && Camera.main) cam = Camera.main.transform;
if (cam) transform.LookAt(transform.position + cam.forward, Vector3.up);
}
}