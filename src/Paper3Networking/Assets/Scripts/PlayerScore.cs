using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerScore : NetworkBehaviour
{
    private NetworkVariable<int> _playerScore = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [SerializeField] private TextMeshProUGUI scoreText;

    [ServerRpc]
    public void AddScoreServerRpc()
    {
        _playerScore.Value += 1;
    }

    private void Start()
    {
        if (IsOwner)
        {

            _playerScore.OnValueChanged += UpdateScoreUI;
            UpdateScoreUI(0, _playerScore.Value);
        }
    }
    private void UpdateScoreUI(int oldValue, int newValue)
    {
        if (IsOwner)
        {
            scoreText.text = $"Score: {newValue}";
        }
    }

    private void OnDestroy()
    {
        if (IsOwner)
        {
            _playerScore.OnValueChanged -= UpdateScoreUI;
        }
    }
}
