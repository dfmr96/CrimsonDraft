using UnityEngine;


public class PickupPoints : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        ScoreManager.Instance.CollectKey();

        Destroy(gameObject);
    }
}