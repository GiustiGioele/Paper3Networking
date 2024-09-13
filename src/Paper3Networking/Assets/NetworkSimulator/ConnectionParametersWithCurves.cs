using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DilmerGames.Core.Singletons;
using Unity.Netcode;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using UnityEngine.Serialization;

[Serializable]
public class ConnectionParametersWithCurves : NetworkScenarioBehaviour
{
    [SerializeField] private float loopDurationMs;
    [SerializeField] private AnimationCurve packetDelayMs = new (default, new (.5f, 100f), new (1f, 0));
    [SerializeField] private AnimationCurve packetJitterMs = new (default, new (.5f, 50f), new(1f, 0));
    [SerializeField] private AnimationCurve packetLossInterval = AnimationCurve.Constant(0f, 1f, 0f);
    [SerializeField] private AnimationCurve packetLossPercent = new(default, new(.5f, 3f), new(1f, 0f));

    private INetworkEventsApi _networkEventsApi;
    private INetworkSimulatorPreset _networkSimulatorPreset;
    private INetworkSimulatorPreset _networkSimulatorPreset2;
    private float _elapsedTime;

    public override void Start(INetworkEventsApi networkEventsApi)
    {
        // Keep a reference to the NetworkEventsApi so then we can change the preset in the update.
        _networkEventsApi = networkEventsApi;

        // Store the current preset so then we can revert once the scenario finishes.
        _networkSimulatorPreset2 = _networkEventsApi.CurrentPreset;

        // Create a custom preset so then we can change the parameters.
        _networkSimulatorPreset = NetworkSimulatorPreset.Create(nameof(ConnectionParametersWithCurves));

        UpdateParameters();
    }

    protected override void Update(float deltaTime)
    {
        _elapsedTime += deltaTime;
        if (_elapsedTime >= loopDurationMs) {
            _elapsedTime -= loopDurationMs;
        }
    }
    private void UpdateParameters()
    {
        var progress = _elapsedTime / loopDurationMs;

        _networkSimulatorPreset.PacketDelayMs = (int)packetDelayMs.Evaluate(progress);
        _networkSimulatorPreset.PacketJitterMs = (int)packetJitterMs.Evaluate(progress);
        _networkSimulatorPreset.PacketLossInterval = (int)packetLossInterval.Evaluate(progress);
        _networkSimulatorPreset.PacketLossPercent = (int)packetLossPercent.Evaluate(progress);

        _networkEventsApi.ChangeConnectionPreset(_networkSimulatorPreset);
    }

    public override void Dispose()
    {
        _networkEventsApi?.ChangeConnectionPreset(_networkSimulatorPreset2);
    }
}
