using UnityEngine;

[DisallowMultipleComponent]
public class OreNode : MonoBehaviour
{
    [Header("Těžba a inventář")]
    [Range(1,2)] public int minimumPickaxeTier = 1;
    [Min(1)] public int hitsRequired = 3;
    [Min(1)] public int dropItemId = 32, dropAmount = 1;
    [Tooltip("Volitelný prázdný potomek na spodku modelu. Jeho zelená osa +Y míří ven ze skály.")]
    public Transform mountPoint;
    [System.NonSerialized] public OreSpawner owner;
    [System.NonSerialized] public SmoothTerrain terrainOwner;
    [System.NonSerialized] public string persistentKey;
    int hits;
    bool collected;

    public void Hit(int pickaxeTier,InventorySystem inventory)
    {
        if(collected || inventory==null || pickaxeTier<minimumPickaxeTier || ++hits<Mathf.Max(1,hitsRequired)) return;
        collected=true;
        if(terrainOwner!=null) terrainOwner.MarkIntegratedOreMined(persistentKey);
        var outward=mountPoint!=null?mountPoint.up:transform.up;
        inventory.SpawnDroppedItem(dropItemId,Mathf.Max(1,dropAmount),transform.position+outward*.55f,outward*1.2f+Vector3.up*.65f);
        gameObject.SetActive(false); Destroy(gameObject);
    }
}
