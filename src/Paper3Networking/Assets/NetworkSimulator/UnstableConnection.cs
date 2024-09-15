using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using UnityEngine;
using Random = System.Random;

namespace Unity.Multiplayer.Tools.Samples.NetworkScenario
{
    [UsedImplicitly, Serializable]
    public class UnstableConnection : NetworkScenarioTask
    {
#region Scenario Event Configuration classes
        [Serializable]
        abstract class EventConfiguration
        {
            [SerializeField]
            [Tooltip("Toggle to activate this type of event.")]
            bool mActive;

            internal bool IsActive => mActive;

            internal abstract void Activate(INetworkEventsApi networkEventsApi, UnstableConnection scenario);
        }

        [Serializable]
        class LagSpikeConfiguration : EventConfiguration
        {
            [SerializeField, MinMaxRange(0, 5000, true)]
            [Tooltip("Time range (in milliseconds) the lag spike will last.")]
            Vector2 mTimeRangeInMs = new(0, 5000);

            internal override void Activate(INetworkEventsApi networkEventsApi, UnstableConnection scenario)
            {
                var timeSpan = TimeSpan.FromMilliseconds(scenario.Randomizer.Next((int)mTimeRangeInMs.x, (int)mTimeRangeInMs.y));
                networkEventsApi.TriggerLagSpike(timeSpan);
            }
        }

        [Serializable]
        class PacketDelayConfiguration : EventConfiguration
        {
            [SerializeField, MinMaxRange(0, 5000, true)]
            [Tooltip("Delay range (in milliseconds) that will be added to the simulator configuration.")]
            Vector2 mDelayRangeInMs = new(50, 150);

            internal override void Activate(INetworkEventsApi _, UnstableConnection scenario)
            {
                if (scenario.ShouldResetBetweenEvents)
                {
                    scenario.ResetScenarioConfiguration();
                }

                scenario.ScenarioConfiguration.PacketDelayMs = scenario.Randomizer.Next((int)mDelayRangeInMs.x, (int)mDelayRangeInMs.y);
            }
        }

        [Serializable]
        class PacketJitterConfiguration : EventConfiguration
        {
            [SerializeField, MinMaxRange(0, 5000, true)]
            [Tooltip("Jitter range (in milliseconds) that will be added to the simulator configuration.")]
            Vector2 mJitterRangeInMs = new(50, 100);

            internal override void Activate(INetworkEventsApi _, UnstableConnection scenario)
            {
                if (scenario.ShouldResetBetweenEvents)
                {
                    scenario.ResetScenarioConfiguration();
                }

                scenario.ScenarioConfiguration.PacketJitterMs = scenario.Randomizer.Next((int)mJitterRangeInMs.x, (int)mJitterRangeInMs.y);
            }
        }

        [Serializable]
        class PacketLossConfiguration : EventConfiguration
        {
            [SerializeField, MinMaxRange(0, 100, true)]
            [Tooltip("Packet loss percentage range that will be set in the simulator configuration. Minimum is 0. Maximum is 100.")]
            Vector2 mPacketLossRangeInPercent = new(0, 10);

            internal override void Activate(INetworkEventsApi _, UnstableConnection scenario)
            {
                if (scenario.ShouldResetBetweenEvents)
                {
                    scenario.ResetScenarioConfiguration();
                }

                scenario.ScenarioConfiguration.PacketLossPercent = scenario.Randomizer.Next((int)mPacketLossRangeInPercent.x, (int)mPacketLossRangeInPercent.y);
            }
        }

        [Serializable]
        class PacketLossIntervalConfiguration : EventConfiguration
        {
            [SerializeField, MinMaxRange(0, 9999, true)]
            [Tooltip("Packet loss range interval that will be set in the simulator configuration.")]
            Vector2 mMinimumInterval = new(0, 0);

            internal override void Activate(INetworkEventsApi _, UnstableConnection scenario)
            {
                if (scenario.ShouldResetBetweenEvents)
                {
                    scenario.ResetScenarioConfiguration();
                }

                scenario.ScenarioConfiguration.PacketLossInterval = scenario.Randomizer.Next((int)mMinimumInterval.x, (int)mMinimumInterval.y);
            }
        }
#endregion

        [SerializeField, Min(0)]
        [Tooltip("Minimum time (in milliseconds) before an event happen. No event will happen until this amount of time has passed.")]
        int mMinimumWaitTimeMs = 3000;

        [SerializeField, Min(0)]
        [Tooltip("Maximum time (in milliseconds) before an event happen. If it goes beyond that, an event will happen automatically.")]
        int mMaximumWaitTimeMs = 5000;

        [SerializeField, Range(0, 100)]
        [Tooltip("How often should an event occur.")]
        int mEventFrequency = 50;

        [SerializeField]
        [Tooltip("Seed to use for the randomizer. If set to -1, the default seed by System.Random is used.")]
        int mRandomizerSeed = -1;

        [SerializeField, Range(1, 5)]
        [Tooltip("How many events should occur at a time. Events will not repeat each other on the same trigger.")]
        int mEventCount = 1;

        [SerializeField]
        [Tooltip("Should the network configuration reset between events. If toggled, every new trigger will start from a clean slate." +
            "If not toggled, every new trigger will change the current configuration.")]
        bool mResetConfigurationBetweenEvents;

        [SerializeField]
        LagSpikeConfiguration mLagSpikeConfiguration = new();

        [SerializeField]
        PacketDelayConfiguration mPacketDelayConfiguration = new();

        [SerializeField]
        PacketJitterConfiguration mPacketJitterConfiguration = new();

        [SerializeField]
        PacketLossConfiguration mPacketLossConfiguration = new();

        [SerializeField]
        PacketLossIntervalConfiguration mPacketLossIntervalConfiguration = new();

        INetworkSimulatorPreset _mSimulatorCache;
        bool _mIsFirstEventOfTrigger;

        internal Random Randomizer { get; private set; }
        internal INetworkSimulatorPreset ScenarioConfiguration { get; private set; }
        internal bool ShouldResetBetweenEvents => mResetConfigurationBetweenEvents && _mIsFirstEventOfTrigger;

        EventConfiguration[] _mEventConfigurations;

        protected override async Task Run(INetworkEventsApi networkEventsApi, CancellationToken cancellationToken)
        {
            _mEventConfigurations = new EventConfiguration[]
            {
                mLagSpikeConfiguration,
                mPacketDelayConfiguration,
                mPacketJitterConfiguration,
                mPacketLossConfiguration,
                mPacketLossIntervalConfiguration
            };

            Randomizer = mRandomizerSeed == -1
                ? new Random()
                : new Random(mRandomizerSeed);

            _mSimulatorCache = networkEventsApi.CurrentPreset;

            ResetScenarioConfiguration();
            var lastTriggerTime = 0f;

            try
            {
                while (cancellationToken.IsCancellationRequested == false)
                {
                    if (IsPaused)
                    {
                        await Task.Yield();
                        continue;
                    }

                    var shouldDoSomething = lastTriggerTime > mMinimumWaitTimeMs &&
                        (Randomizer.Next(0, 100) < mEventFrequency
                            || lastTriggerTime > mMaximumWaitTimeMs);

                    if (shouldDoSomething)
                    {
                        await TriggerNetworkEvents(networkEventsApi, cancellationToken);
                        lastTriggerTime = mMinimumWaitTimeMs;
                        await Task.Delay(mMinimumWaitTimeMs, cancellationToken);
                    }
                    else
                    {
                        lastTriggerTime += Time.deltaTime * 1000;
                        await Task.Yield();
                    }
                }
            }
            finally
            {
                //Setting back the original preset at the end of the scenario
                networkEventsApi.ChangeConnectionPreset(_mSimulatorCache);
            }
        }

        internal void ResetScenarioConfiguration()
        {
            ScenarioConfiguration = NetworkSimulatorPreset.Create("Unstable Connection Configuration",
                packetDelayMs: _mSimulatorCache.PacketDelayMs,
                packetJitterMs: _mSimulatorCache.PacketJitterMs,
                packetLossPercent: _mSimulatorCache.PacketLossPercent,
                packetLossInterval: _mSimulatorCache.PacketLossInterval);
        }

        async Task TriggerNetworkEvents(INetworkEventsApi networkEventsApi, CancellationToken cancellationToken)
        {
            var activeConfigurations = _mEventConfigurations.Where(x => x?.IsActive ?? false).ToList();

            if (activeConfigurations.Count == 0)
            {
                Debug.Log("No Active Configurations");
                await Task.Yield();
                return;
            }

            var eventCount = mEventCount > activeConfigurations.Count ? activeConfigurations.Count : mEventCount;

            _mIsFirstEventOfTrigger = true;
            while (eventCount > 0)
            {
                var configIndex = Randomizer.Next(0, activeConfigurations.Count);
                var configuration = activeConfigurations.ToList()[configIndex];
                configuration.Activate(networkEventsApi, this);
                activeConfigurations.Remove(configuration);
                --eventCount;
                _mIsFirstEventOfTrigger = false;
            }

            networkEventsApi.ChangeConnectionPreset(ScenarioConfiguration);
        }
    }
}
