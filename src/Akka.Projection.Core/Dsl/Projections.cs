//-----------------------------------------------------------------------
// <copyright file="Projections.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Annotations;

namespace Akka.Projections.Dsl
{
    [DoNotInherit]
    public interface IExactlyOnceProjection<TOffset, TEnvelope> : IProjection<TEnvelope>
    {
        new IExactlyOnceProjection<TOffset, TEnvelope> WithRestartBackoff(TimeSpan minBackoff, TimeSpan maxBackoff, double randomFactor);
        new IExactlyOnceProjection<TOffset, TEnvelope> WithRestartBackoff(TimeSpan minBackoff, TimeSpan maxBackoff, double randomFactor, int maxRestarts);
        new IExactlyOnceProjection<TOffset, TEnvelope> WithStatusObserver(StatusObserver<TEnvelope> observer);

        IExactlyOnceProjection<TOffset, TEnvelope> WithRecoveryStrategy(HandlerRecoveryStrategy recoveryStrategy);
    }

    [DoNotInherit]
    public interface IAtLeastOnceFlowProjection<TOffset, TEnvelope> : IProjection<TEnvelope>
    {
        new IAtLeastOnceFlowProjection<TOffset, TEnvelope> WithRestartBackoff(TimeSpan minBackoff, TimeSpan maxBackoff, double randomFactor);
        new IAtLeastOnceFlowProjection<TOffset, TEnvelope> WithRestartBackoff(TimeSpan minBackoff, TimeSpan maxBackoff, double randomFactor, int maxRestarts);
        new IAtLeastOnceFlowProjection<TOffset, TEnvelope> WithStatusObserver(StatusObserver<TEnvelope> observer);

        IAtLeastOnceFlowProjection<TOffset, TEnvelope> WithSaveOffset(int afterEnvelopes, TimeSpan afterDuration);
    }

    [DoNotInherit]
    public interface IAtLeastOnceProjection<TOffset, TEnvelope> : IProjection<TEnvelope>
    {
        new IAtLeastOnceProjection<TOffset, TEnvelope> WithRestartBackoff(TimeSpan minBackoff, TimeSpan maxBackoff, double randomFactor);
        new IAtLeastOnceProjection<TOffset, TEnvelope> WithRestartBackoff(TimeSpan minBackoff, TimeSpan maxBackoff, double randomFactor, int maxRestarts);
        new IAtLeastOnceProjection<TOffset, TEnvelope> WithStatusObserver(StatusObserver<TEnvelope> observer);

        IAtLeastOnceProjection<TOffset, TEnvelope> WithSaveOffset(int afterEnvelopes, TimeSpan afterDuration);
        IAtLeastOnceProjection<TOffset, TEnvelope> WithRecoveryStrategy(HandlerRecoveryStrategy recoveryStrategy);
    }

    [DoNotInherit]
    public interface IAtMostOnceProjection<TOffset, TEnvelope> : IProjection<TEnvelope>
    {
        new IAtMostOnceProjection<TOffset, TEnvelope> WithRestartBackoff(TimeSpan minBackoff, TimeSpan maxBackoff, double randomFactor);
        new IAtMostOnceProjection<TOffset, TEnvelope> WithRestartBackoff(TimeSpan minBackoff, TimeSpan maxBackoff, double randomFactor, int maxRestarts);
        new IAtMostOnceProjection<TOffset, TEnvelope> WithStatusObserver(StatusObserver<TEnvelope> observer);

        IAtMostOnceProjection<TOffset, TEnvelope> WithRecoveryStrategy(HandlerRecoveryStrategy recoveryStrategy);
    }

    [DoNotInherit]
    public interface IGroupedProjection<TOffset, TEnvelope> : IProjection<TEnvelope>
    {
        new IGroupedProjection<TOffset, TEnvelope> WithRestartBackoff(TimeSpan minBackoff, TimeSpan maxBackoff, double randomFactor);
        new IGroupedProjection<TOffset, TEnvelope> WithRestartBackoff(TimeSpan minBackoff, TimeSpan maxBackoff, double randomFactor, int maxRestarts);
        new IGroupedProjection<TOffset, TEnvelope> WithStatusObserver(StatusObserver<TEnvelope> observer);

        IGroupedProjection<TOffset, TEnvelope> WithGroup(int groupAfterEnvelopes, TimeSpan groupAfterDuration);
        IGroupedProjection<TOffset, TEnvelope> WithRecoveryStrategy(HandlerRecoveryStrategy recoveryStrategy);
    }
}
