using UnityEngine;

// Stores an item stack represented by a world object.
public class DroppedItem : MonoBehaviour
{
    public int itemId = 1;
    [Min(1)] public int amount = 1;
    float readyAt;
    bool collected;

    public bool CanPickUp => !collected && Time.time >= readyAt;

    // Retains compatibility with scripts that use the original signature.
    public void Initialize(int id, float delay)
    {
        Initialize(id, 1, delay);
    }

    public void Initialize(int id, int count, float delay)
    {
        itemId = id;
        amount = Mathf.Max(1, count);
        collected = false;
        readyAt = Time.time + Mathf.Max(0f, delay);
    }

    public void MarkCollected() { collected = true; }
}
