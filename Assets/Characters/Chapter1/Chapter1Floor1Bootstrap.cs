using UnityEngine;

public class Chapter1Floor1Bootstrap : MonoBehaviour
{
    [SerializeField] private Transform playerSpawn;
    [SerializeField] private float fallLimitY = -20f;
    [SerializeField] private float sideLimit = 310f;
    [SerializeField] private float backLimitZ = -110f;
    [SerializeField] private float forwardLimitZ = 1450f;

    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;
    private GameObject _player;

    private void Start()
    {
        if (playerSpawn == null)
        {
            playerSpawn = FindInChildren(transform, "PlayerSpawn");
        }

        _player = GameObject.Find("Player");
        if (_player != null && playerSpawn != null)
        {
            _spawnPosition = playerSpawn.position + Vector3.up * 1.5f;
            _spawnRotation = Quaternion.LookRotation(Vector3.forward);
            TeleportPlayerToStart();
        }
    }

    private static Transform FindInChildren(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
                return child;
        }
        return null;
    }

    private void Update()
    {
        if (_player == null)
            return;

        // Bounds are defined in the generator's local space, not at world origin.
        // This keeps the safety check valid if the floor root is moved or rotated in the scene.
        Vector3 position = transform.InverseTransformPoint(_player.transform.position);
        bool outsideMap =
            position.y < fallLimitY ||
            Mathf.Abs(position.x) > sideLimit ||
            position.z < backLimitZ ||
            position.z > forwardLimitZ;

        if (outsideMap)
            TeleportPlayerToStart();
    }

    private void TeleportPlayerToStart()
    {
        CharacterController controller = _player.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        _player.transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);

        Camera mainCamera = Camera.main;
        Transform cameraRoot = _player.transform.Find("CameraRoot");
        if (mainCamera != null && cameraRoot != null)
        {
            mainCamera.transform.SetPositionAndRotation(
                cameraRoot.position + Vector3.up * 1.2f,
                cameraRoot.rotation);
        }

        if (controller != null)
            controller.enabled = true;
    }
}
