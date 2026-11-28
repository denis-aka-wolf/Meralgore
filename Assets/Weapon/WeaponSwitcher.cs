using UnityEngine;

public class WeaponSwitcher : MonoBehaviour
{
    public GameObject currentWeapon;
    public Weapon weapon;

    private readonly Weapon[] _slots = new Weapon[2];
    public int CurrentSlot { get; private set; }

    private void Start()
    {
        Weapon startingWeapon = null;
        foreach (Transform child in transform)
        {
            Weapon childWeapon = child.GetComponent<Weapon>();
            child.gameObject.SetActive(false);
            if (childWeapon is Revolver)
                startingWeapon = childWeapon;
        }

        if (startingWeapon == null && transform.childCount > 0)
            startingWeapon = transform.GetChild(0).GetComponent<Weapon>();

        _slots[0] = startingWeapon;
        ChangeWeapon(0);
    }

    public void ChangeWeapon(int weaponNum)
    {
        if (weaponNum < 0 || weaponNum >= _slots.Length || _slots[weaponNum] == null)
            return;

        foreach (Transform child in transform)
            child.gameObject.SetActive(false);

        weapon = _slots[weaponNum];
        currentWeapon = weapon.gameObject;
        currentWeapon.SetActive(true);
        CurrentSlot = weaponNum;
    }

    public Weapon GetWeaponForSlot(int slot) =>
        slot >= 0 && slot < _slots.Length ? _slots[slot] : null;
}
