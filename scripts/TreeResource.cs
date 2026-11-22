using UnityEngine;

// A successful timed axe action removes the tree and drops wood.
public class TreeResource : MonoBehaviour
{
    public int woodDropAmount = 10;
    public GameObject woodDropPrefab;
    [Tooltip("Volitelný vizuální potomek, který se kýve ve větru. Umísti jeho pivot ke kořenům.")]
    public Transform windPivot;
    private bool chopped;
    private SmoothTerrain generatedTerrain;
    private string generatedKey;
    private GameObject generatedRoot;

    public void BindGenerated(SmoothTerrain terrain, string key, GameObject root = null)
    {
        generatedTerrain = terrain;
        generatedKey = key;
        generatedRoot = root;
    }

    public void Chop(InventorySystem inventory)
    {
        if (chopped || inventory == null || !inventory.HasAxeEquipped) return;
        chopped = true;
        if (generatedTerrain != null) generatedTerrain.RememberTree(generatedKey);
        Vector3 origin = transform.position + Vector3.up * 0.8f;
        for (int i = 0; i < Mathf.Max(0, woodDropAmount); i++)
        {
            Vector3 offset = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0.1f, 0.6f), Random.Range(-0.8f, 0.8f));
            if (woodDropPrefab == null)
            {
                inventory.SpawnDroppedItem(3, 1, origin + offset, offset + Vector3.up);
            }
            else
            {
                GameObject obj = Instantiate(woodDropPrefab, origin + offset, Quaternion.identity);
                if (obj.GetComponentInChildren<Collider>() == null) obj.AddComponent<BoxCollider>();
                DroppedItem item = obj.GetComponent<DroppedItem>();
                if (item == null) item = obj.AddComponent<DroppedItem>();
                item.Initialize(3, 1, 0.8f);
                Rigidbody body = obj.GetComponent<Rigidbody>();
                if (body == null) body = obj.AddComponent<Rigidbody>();
                body.isKinematic = false;
                body.useGravity = true;
                body.AddForce(offset + Vector3.up, ForceMode.VelocityChange);
            }
        }
        Destroy(generatedRoot != null ? generatedRoot : gameObject);
    }
}
