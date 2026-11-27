using UnityEngine;
using Unity.Cinemachine;

public class Chapter1CameraRig : MonoBehaviour
{
    [SerializeField] private CinemachineCamera virtualCamera;

    public void SetVirtualCamera(CinemachineCamera camera)
    {
        virtualCamera = camera;
    }

    private void LateUpdate()
    {
        if (virtualCamera == null)
            return;

        transform.SetPositionAndRotation(
            virtualCamera.transform.position,
            virtualCamera.transform.rotation);
    }
}
