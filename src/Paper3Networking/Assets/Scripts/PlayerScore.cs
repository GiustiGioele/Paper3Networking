using TMPro;
using Unity.Netcode;
using UnityEngine;

public class PlayerScore : NetworkBehaviour
{
    private NetworkVariable<int> _playerScore = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField]
    private TextMeshProUGUI scoreText;


    public void AddScore()
    {
        _playerScore.Value += 1;
    }

    private void Start()
    {
        if (IsClient && IsOwner )
        {
            _playerScore.OnValueChanged += UpdateScoreUI;

            UpdateScoreUI(0, _playerScore.Value);
        }
    }

    private void UpdateScoreUI(int oldValue, int newValue)
    {
        if (IsClient && IsOwner)
        {
            scoreText.text = $"Score: {newValue}";
        }
    }

    private void OnDestroy()
    {
        if (IsClient && IsOwner)
        {
            _playerScore.OnValueChanged -= UpdateScoreUI;
        }
    }
}
