//-----------------------------------------------------------------------
// <copyright file="HandlerRecoveryImpl.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Annotations;
using Akka.Event;

namespace Akka.Projections.Internal
{
    [InternalApi]
    public class HandlerRecoveryImpl
    {
        public static HandlerRecoveryImpl<TOffset, TEnvelope> Create<TOffset, TEnvelope>(
            ProjectionId projectionId,
            IHandlerRecoveryStrategy recoveryStrategy,
            ILoggingAdapter logger,
            StatusObserver<TEnvelope> statusObserver) =>
            new HandlerRecoveryImpl<TOffset, TEnvelope>(projectionId, recoveryStrategy, logger, statusObserver);
    }


    [InternalApi]
    public class HandlerRecoveryImpl<TOffset, TEnvelope>
    {
        public HandlerRecoveryImpl(
            ProjectionId projectionId,
            IHandlerRecoveryStrategy recoveryStrategy,
            ILoggingAdapter logger,
            StatusObserver<TEnvelope> statusObserver)
        {
        }

        public Task<Done> ApplyRecovery(TEnvelope env, TOffset firstOffset, TOffset lastOffset, Task<Done> abort, Func<Task<Done>> futureCallback) => 
            Task.FromResult(Done.Instance);
    }
}
