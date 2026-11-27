using System.Collections.Generic;
using UnityEngine;

public class FloorMapRoom : MonoBehaviour
{
    private readonly List<FloorMapLink> _links = new List<FloorMapLink>();
    private FloorRunController _runController;

    public FloorMapNode Node { get; private set; }
    public bool IsCleared { get; private set; }

    public void Initialize(FloorMapNode node, Chapter1Floor1Generator generator)
    {
        Node = node;
        var trigger = gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 8f, 0f);
        trigger.size = new Vector3(64f, 18f, 48f);
    }

    public void SetRunController(FloorRunController runController)
    {
        _runController = runController;
    }

    public void AddLink(FloorMapLink link)
    {
        _links.Add(link);
    }

    public void SetLocked(bool locked)
    {
        foreach (FloorMapLink link in _links)
            link.SetLocked(locked);
    }

    public void MarkCleared()
    {
        IsCleared = true;
        SetLocked(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerMovement>() != null && _runController != null)
            _runController.EnterRoom(this);
    }
}
