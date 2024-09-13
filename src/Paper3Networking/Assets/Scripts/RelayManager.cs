using DilmerGames.Core.Singletons;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : Singleton<RelayManager>
{
    [SerializeField]
    private string environment = "production";

    [SerializeField]
    private int maxNumberOfConnections = 10;

    public bool IsRelayEnabled => Transport != null && Transport.Protocol == UnityTransport.ProtocolType.RelayUnityTransport;

    public UnityTransport Transport => NetworkManager.Singleton.gameObject.GetComponent<UnityTransport>();

    public async Task<RelayHostData> SetupRelay()
    {
        Logger.Instance.LogInfo($"Relay Server Starting With Max Connections: {maxNumberOfConnections}");

        InitializationOptions options = new InitializationOptions()
            .SetEnvironmentName(environment);

        await UnityServices.InitializeAsync(options);

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        Allocation allocation = await Relay.Instance.CreateAllocationAsync(maxNumberOfConnections);

        RelayHostData relayHostData = new RelayHostData
        {
            _key = allocation.Key,
            _port = (ushort) allocation.RelayServer.Port,
            _allocationID = allocation.AllocationId,
            _allocationIDBytes = allocation.AllocationIdBytes,
            _pv4Address = allocation.RelayServer.IpV4,
            _connectionData = allocation.ConnectionData
        };

        relayHostData._joinCode = await Relay.Instance.GetJoinCodeAsync(relayHostData._allocationID);

        Transport.SetRelayServerData(relayHostData._pv4Address, relayHostData._port, relayHostData._allocationIDBytes,
                relayHostData._key, relayHostData._connectionData);

        Logger.Instance.LogInfo($"Relay Server Generated Join Code: {relayHostData._joinCode}");

        return relayHostData;
    }

    public async Task<RelayJoinData> JoinRelay(string joinCode)
    {
        Logger.Instance.LogInfo($"Client Joining Game With Join Code: {joinCode}");

        InitializationOptions options = new InitializationOptions()
            .SetEnvironmentName(environment);

        await UnityServices.InitializeAsync(options);

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        JoinAllocation allocation = await Relay.Instance.JoinAllocationAsync(joinCode);

        RelayJoinData relayJoinData = new RelayJoinData
        {
            _key = allocation.Key,
            _port = (ushort)allocation.RelayServer.Port,
            _allocationID = allocation.AllocationId,
            _allocationIDBytes = allocation.AllocationIdBytes,
            _connectionData = allocation.ConnectionData,
            _hostConnectionData = allocation.HostConnectionData,
            _pv4Address = allocation.RelayServer.IpV4,
            _joinCode = joinCode
        };

        Transport.SetRelayServerData(relayJoinData._pv4Address, relayJoinData._port, relayJoinData._allocationIDBytes,
            relayJoinData._key, relayJoinData._connectionData, relayJoinData._hostConnectionData);

        Logger.Instance.LogInfo($"Client Joined Game With Join Code: {joinCode}");

        return relayJoinData;
    }
}
