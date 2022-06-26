//-----------------------------------------------------------------------
// <copyright file="ProjectionSettings.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Annotations;
using Akka.Configuration;
using Akka.Streams;

namespace Akka.Projections.Internal
{
    [InternalApi]
    public class ProjectionSettings
    {
        public RestartSettings RestartBackoff { get; }
        public int SaveOffsetAfterEnvelopes { get; }
        public TimeSpan SaveOffsetAfterDuration { get; }
        public int GroupAfterEnvelopes { get; }
        public TimeSpan GroupAfterDuration { get; }
        public IHandlerRecoveryStrategy RecoveryStrategy { get; }

        public ProjectionSettings(RestartSettings restartBackoff, int saveOffsetAfterEnvelopes, TimeSpan saveOffsetAfterDuration, int groupAfterEnvelopes, TimeSpan groupAfterDuration, IHandlerRecoveryStrategy recoveryStrategy)
        {
            RestartBackoff = restartBackoff;
            SaveOffsetAfterEnvelopes = saveOffsetAfterEnvelopes;
            SaveOffsetAfterDuration = saveOffsetAfterDuration;
            GroupAfterEnvelopes = groupAfterEnvelopes;
            GroupAfterDuration = groupAfterDuration;
            RecoveryStrategy = recoveryStrategy;
        }

        public static ProjectionSettings Create(ActorSystem system) =>
            FromConfig(system.Settings.Config.GetConfig("akka.projection"));

        public static ProjectionSettings FromConfig(Config config)
        {
            var restartBackoffConfig = config.GetConfig("restart-backoff");
            var atLeastOnceConfig = config.GetConfig("at-least-once");
            var groupedConfig = config.GetConfig("grouped");
            var recoveryStrategyConfig = config.GetConfig("recovery-strategy");

            var minBackoff = restartBackoffConfig.GetTimeSpan("min-backoff");
            var maxBackoff = restartBackoffConfig.GetTimeSpan("max-backoff");
            var randomFactor = restartBackoffConfig.GetDouble("random-factor");
            var maxRestarts = restartBackoffConfig.GetInt("max-restarts");

            var restartSettings = (maxRestarts >= 0)
                ? RestartSettings.Create(minBackoff, maxBackoff, randomFactor)
                : RestartSettings.Create(minBackoff, maxBackoff, randomFactor).WithMaxRestarts(maxRestarts, minBackoff);

            return new ProjectionSettings(
                restartSettings,
                atLeastOnceConfig.GetInt("save-offset-after-envelopes"),
                atLeastOnceConfig.GetTimeSpan("save-offset-after-duration"),
                groupedConfig.GetInt("group-after-envelopes"),
                groupedConfig.GetTimeSpan("group-after-duration"),
                RecoveryStrategyConfig.FromConfig(recoveryStrategyConfig));
        }
    }

    [InternalApi]
    internal class RecoveryStrategyConfig
    {
        public static IHandlerRecoveryStrategy FromConfig(Config config)
        {
            var strategy = config.GetString("strategy");
            var retries = config.GetInt("retries");
            var retryDelay = config.GetTimeSpan("retry-delay");

            return strategy switch
            {
                "fail" => HandlerRecoveryStrategy.Fail,
                "skip" => HandlerRecoveryStrategy.Skip,
                "retry-and-fail" => HandlerRecoveryStrategy.RetryAndFail(retries, retryDelay),
                "retry-and-skip" => HandlerRecoveryStrategy.RetryAndSkip(retries, retryDelay),
                _ => throw new ArgumentException(nameof(strategy),
                        $"Strategy type [{strategy}] is not supported. Supported options are [fail, skip, retry-and-fail, retry-and-skip]")
            };
        }
    }
}
