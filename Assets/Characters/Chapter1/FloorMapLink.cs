using UnityEngine;

public class FloorMapLink : MonoBehaviour
{
    public void SetLocked(bool locked)
    {
        gameObject.SetActive(locked);
    }
}
