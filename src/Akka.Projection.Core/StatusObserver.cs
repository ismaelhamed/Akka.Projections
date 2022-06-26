//-----------------------------------------------------------------------
// <copyright file="StatusObserver.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Annotations;

namespace Akka.Projections
{
    /// <summary>
    /// Track status of a projection by implementing a <see cref="StatusObserver{TEnvelope}"/> and 
    /// install it using <see cref="IProjection{TEnvelope}.WithStatusObserver"/>.
    /// </summary>
    [ApiMayChange]
    public abstract class StatusObserver<TEnvelope>
    {
        /// <summary>
        /// Called when a projection is started. Also called after the projection has been restarted.
        /// </summary>
        public abstract void Started(ProjectionId projectionId);

        /// <summary>
        /// Called when a projection failed.
        /// <para>
        /// The projection will be restarted unless the projection restart backoff settings are configured 
        /// with max-restarts limit.
        /// </para>
        /// </summary>
        public abstract void Failed(ProjectionId projectionId, Exception exception);

        /// <summary>
        /// Called when a projection is stopped. Also called before the projection is restarted.
        /// </summary>
        public abstract void Stopped(ProjectionId projectionId);

        /// <summary>
        /// Called as soon as an envelop is ready to be processed. The envelope processing may not start immediately 
        /// if grouping or batching are enabled.
        /// </summary>
        public abstract void BeforeProcess(ProjectionId projectionId, TEnvelope envelope);

        /// <summary>
        /// Invoked as soon as the projected information is readable by a separate thread (e.g committed to database). 
        /// It will not be invoked if the envelope is skipped or handling fails.
        /// </summary>
        public abstract void AfterProcess(ProjectionId projectionId, TEnvelope envelope);

        /// <summary>
        /// Called when the corresponding offset has been stored. It might not be called for each envelope.
        /// </summary>
        public abstract void OffsetProgress(ProjectionId projectionId, TEnvelope envelope);

        /// <summary>
        /// Called when processing of an envelope failed. The invocation of this method is not guaranteed when 
        /// the handler failure causes a stream failure (e.g. using a Flow-based handler or a recovery strategy 
        /// that immediately fails).
        /// <para>
        /// From the `recoveryStrategy` and keeping track how many times error is called it's possible to derive 
        /// what next step will be; fail, skip, retry.
        /// </para>
        /// </summary>
        public abstract void Error(ProjectionId projectionId, TEnvelope envelope, Exception exception, IHandlerRecoveryStrategy recoveryStrategy);
    }
}
