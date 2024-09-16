using Unity.Netcode;
using UnityEngine;

public class Coin : NetworkBehaviour
{
    private void OnCollisionEnter (Collision collision)
    {
        if (collision.gameObject.TryGetComponent<PlayerScore>(out PlayerScore playerScore))
        {
            if (IsServer)
            {
                playerScore.AddScoreServerRpc();
                Destroy(gameObject);
                Physics.IgnoreCollision(collision.collider, GetComponent<Collider>());
            }
        }
    }
}
