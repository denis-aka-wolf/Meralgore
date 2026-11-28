using UnityEngine;

public class MeralgoreItemPickup : MonoBehaviour
{
    [SerializeField] private MeralgoreItemType itemType;

    private Vector3 _basePosition;

    public MeralgoreItemType ItemType => itemType;

    public void Configure(MeralgoreItemType type)
    {
        itemType = type;
    }

    private void Start()
    {
        _basePosition = transform.position;
    }

    private void Update()
    {
        transform.Rotate(0f, 35f * Time.deltaTime, 0f, Space.World);
        transform.position = _basePosition + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.15f;
    }

    private void OnTriggerEnter(Collider other)
    {
        MeralgorePlayerState player = other.GetComponent<MeralgorePlayerState>();
        if (player == null)
            return;

        FloorRunController runController = UnityEngine.Object.FindAnyObjectByType<FloorRunController>();
        if (!player.AddItem(itemType))
        {
            runController?.Log("Этот предмет уже есть в инвентаре.");
            Destroy(gameObject);
            return;
        }

        if (runController != null)
            runController.OnItemPicked(itemType);

        Destroy(gameObject);
    }
}
