//-----------------------------------------------------------------------
// <copyright file="Projection.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Annotations;
using Akka.Projections.Internal;
using Akka.Streams.Dsl;
using Akka.Util;

namespace Akka.Projections
{
    public interface IProjection
    { }

    /// <summary>
    /// The core abstraction in Akka Projections.
    /// <para>
    /// A projection instance may share the same name and <typeparamref name="TEnvelope"/>, but must have a unique key. 
    /// The key is used to achieve processing parallelism for a projection.
    /// </para>
    /// <para>
    /// For example, many projections may share the same name `user-events-projection`, but can process events for
    /// different sharded entities within Akka Cluster, where key could be the Akka Cluster shardId.
    /// </para>
    /// </summary>
    [ApiMayChange]
    public interface IProjection<TEnvelope> : IProjection
    {
        ProjectionId ProjectionId { get; }

        IProjection<TEnvelope> WithRestartBackoff(TimeSpan minBackoff, TimeSpan maxBackoff, double randomFactor);
        IProjection<TEnvelope> WithRestartBackoff(TimeSpan minBackoff, TimeSpan maxBackoff, double randomFactor, int maxRestarts);

        StatusObserver<TEnvelope> StatusObserver();
        IProjection<TEnvelope> WithStatusObserver(StatusObserver<TEnvelope> observer);

        /// <summary>
        /// This method returns the projection <see cref="Source"/> mapped with user `handler` function, but before any sink attached.
        /// This is mainly intended to be used by the TestKit allowing it to attach a TestSink to it.
        /// </summary>
        [InternalApi]
        Source<Done, Task<Done>> MappedSource(ActorSystem system);

        [InternalApi]
        Option<IActorHandlerInit> ActorHandlerInit();

        /// <summary>
        /// Return a RunningProjection
        /// </summary>
        [InternalApi]
        IRunningProjection Run(ActorSystem system);
    }

    /// <summary>
    /// Helper to wrap the projection source with a <see cref="RestartSource"/> using the provided settings.
    /// </summary>
    [InternalApi]
    public static class RunningProjection
    {
        public static Source<Done, NotUsed> WithBackOff(Func<Source<Done, NotUsed>> source, ProjectionSettings settings) =>
            RestartSource.OnFailuresWithBackoff(() =>
            {
                return source().RecoverWithRetries((exception) => exception switch
                {
                    AbortProjectionException _ => Source.Empty<Done>(), // don't restart
                    _ => throw exception
                }, 1);
            }, settings.RestartBackoff);

        /// <summary>
        /// When stopping an projection the retry mechanism is aborted via this exception.
        /// </summary>
        public class AbortProjectionException : Exception
        {
            public AbortProjectionException() 
                : base("Projection aborted.")
            { }
        }
    }

    [InternalApi]
    public interface IRunningProjection
    {
        /// <summary>
        /// Stop the projection if it's running.
        /// </summary>
        /// <returns>The returned Task should return the stream materialized value.</returns>
        Task<Done> Stop();
    }

    [InternalApi]
    public interface IRunningProjectionManagement<TOffset>
    {
        Task<Option<TOffset>> GetOffset();
        Task<Done> SetOffset(Option<TOffset> offset);
        Task<Option<ManagementState>> GetManagementState();
        Task<Done> SetPaused(bool paused);
    }
}
