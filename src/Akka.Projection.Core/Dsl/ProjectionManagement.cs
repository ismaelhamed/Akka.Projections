//-----------------------------------------------------------------------
// <copyright file="ProjectionManagement.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Util;

namespace Akka.Projections.Dsl
{
    public class ProjectionManagement : IExtension
    {
        private readonly ExtendedActorSystem _system;
        private readonly int _retryAttempts;
        private readonly ConcurrentDictionary<string, IActorRef> _topics = new ConcurrentDictionary<string, IActorRef>();

        public ProjectionManagement(ExtendedActorSystem system)
        {
            _system = system;
            var askTimeout = system.Settings.Config.GetTimeSpan("akka.projection.management.ask-timeout");
            var operationTimeout = system.Settings.Config.GetTimeSpan("akka.projection.management.operation-timeout");
            _retryAttempts = Math.Max(1, (int)(operationTimeout.Ticks / askTimeout.Ticks));
        }

        public static ProjectionManagement Get(ActorSystem system) =>
            system.WithExtension<ProjectionManagement, ProjectionManagementProvider>();

        private static string TopicName(string projectionName) => $"projection-{projectionName}";

        private IActorRef Topic(string projectionName) =>
            _topics.GetOrAdd(projectionName, _ =>
            {
                var name = TopicName(projectionName);
                // TODO: Requires Akka Typed Receptionist
                //return _system.ActorOf(Topic.Props(name), name); 

                return ActorRefs.Nobody;
            });

        /// <summary>
        /// Projection registers when started
        /// </summary>
        public void Register(ProjectionId projectionId, IActorRef projection)
        {
            // TODO: Subscribes projection to a topic named 'projectionId.Name'
            // Topic(projectionId.Name) ! Topic.Subscribe(projection)
        }

        /// <summary>
        /// Get the latest stored offset for the <paramref name="projectionId"/>.
        /// </summary>
        public Task<Option<TOffset>> GetOffset<TOffset>(ProjectionId projectionId)
        {
            async Task<Option<TOffset>> AskGetOffset()
            {
                var replyTo = Topic(projectionId.Name);
                var currentOffset = await replyTo.Ask<CurrentOffset>(new GetOffset(projectionId, replyTo));
                // FIXME: not quiet sure this is alright
                return currentOffset.Offset.HasValue ? (TOffset)currentOffset.Offset.Value : Option<TOffset>.None;
            }

            return Retry(AskGetOffset);
        }

        /// <summary>
        /// Update the stored offset for the <paramref name="projectionId"/> and restart the Projection. This can be useful 
        /// if the projection was stuck with errors on a specific offset and should skip that offset and continue with next. 
        /// Note that when the projection is restarted it will continue from the next offset that is greater than the stored offset.
        /// </summary>
        public Task<Done> UpdateOffset<TOffset>(ProjectionId projectionId, Option<TOffset> offset) =>
            SetOffset(projectionId, offset);

        /// <summary>
        /// Clear the stored offset for the <paramref name="projectionId"/> and restart the Projection. This can be useful if 
        /// the projection should be completely rebuilt, starting over again from the first offset.
        /// </summary>
        public Task<Done> ClearOffset<TOffset>(ProjectionId projectionId) =>
            SetOffset(projectionId, Option<TOffset>.None);

        private Task<Done> SetOffset<TOffset>(ProjectionId projectionId, Option<TOffset> offset)
        {
            async Task<Done> AskSetOffset()
            {
                var replyTo = Topic(projectionId.Name);
                return await replyTo.Ask<Done>(new SetOffset(projectionId, offset, replyTo));
            }

            return Retry(AskSetOffset);
        }

        private Task<T> Retry<T>(Func<Task<T>> operation)
        {
            static Task<T> Attempt(int remaining, Func<Task<T>> operation)
            {
                try
                {
                    return operation();
                }
                catch (TimeoutException ex)
                {
                    if (remaining > 0) Attempt(remaining - 1, operation);
                    return Task.FromException<T>(ex);
                }
            }

            return Attempt(_retryAttempts, operation);
        }

        /// <summary>
        /// Is the given Projection paused or not?
        /// </summary>
        public Task<bool> IsPaused(ProjectionId projectionId)
        {
            async Task<bool> AskIsPaused()
            {
                var replyTo = Topic(projectionId.Name);
                return await replyTo.Ask<bool>(new IsPaused(projectionId, replyTo));
            }

            return Retry(AskIsPaused);
        }

        /// <summary>
        /// Pause the given Projection. Processing will be stopped. While the Projection is paused other management 
        /// operations can be performed, such as <see cref="Resume"/>. The Projection can be resumed with <see cref="Resume"/>.
        /// <para>
        /// The paused/resumed state is stored and, and it is read when the Projections are started, for example in case 
        /// of rebalance or system restart.
        /// </para>
        /// </summary>
        public Task<Done> Pause(ProjectionId projectionId) =>
            SetPauseProjection(projectionId, true);

        /// <summary>
        /// Resume a paused Projection. Processing will be start from previously stored offset.
        /// <para>
        /// The paused/resumed state is stored and, and it is read when the Projections are started, for example in case 
        /// of rebalance or system restart.
        /// </para>
        /// </summary>
        public Task<Done> Resume(ProjectionId projectionId) =>
            SetPauseProjection(projectionId, false);

        private Task<Done> SetPauseProjection(ProjectionId projectionId, bool paused)
        {
            async Task<Done> AskSetPaused()
            {
                var replyTo = Topic(projectionId.Name);
                return await replyTo.Ask<Done>(new SetPaused(projectionId, paused, replyTo));
            }

            return Retry(AskSetPaused);
        }
    }

    public class ProjectionManagementProvider : ExtensionIdProvider<ProjectionManagement>
    {
        public override ProjectionManagement CreateExtension(ExtendedActorSystem system) =>
            new ProjectionManagement(system);
    }
}
