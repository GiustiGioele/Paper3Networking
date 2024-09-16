using Unity.Netcode;
using UnityEngine;

public class Coin : NetworkBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.TryGetComponent<PlayerScore>(out PlayerScore playerScore))
        {
            if (IsClient)
            {
                CollectCoinServerRpc(playerScore.OwnerClientId);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CollectCoinServerRpc(ulong clientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            var playerScore = client.PlayerObject.GetComponent<PlayerScore>();
            if (playerScore != null)
            {
                playerScore.AddScore();
                Destroy(gameObject);
            }
        }
    }
}
