//-----------------------------------------------------------------------
// <copyright file="ActorHandler.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Annotations;
using Akka.Projections.Internal;
using Akka.Util;

namespace Akka.Projections.Dsl
{
    /// <summary>
    /// This <see cref="Handler{TEnvelope}"/> gives support for spawning an actor using the given Props 
    /// to delegate processing of the envelopes to the actor.
    /// <para>
    /// The lifecycle of the actor is managed by the <see cref="IProjection{TEnvelope}"/>. The behavior 
    /// is spawned when the <see cref="IProjection{TEnvelope}"/> is started and the <see cref="IActorRef"/> 
    /// is passed in as a parameter to the process method. The Actor is stopped when the 
    /// <see cref="IProjection{TEnvelope}"/> is stopped.
    /// </para>
    /// </summary>
    [ApiMayChange]
    public abstract class ActorHandler<TEnvelope> : Handler<TEnvelope>, IActorHandlerInit
    {
        private Option<IActorRef> _actor = Option<IActorRef>.None;

        public Props Props { get; }

        protected ActorHandler(Props props) => Props = props;

        /// <summary>
        /// <para>
        /// The process method is invoked for each <typeparamref name="TEnvelope"/>. One envelope is 
        /// processed at a time. The returned <see cref="Task"/> is to be completed when the processing 
        /// of the envelope has finished. It will not be invoked with the next envelope until after the 
        /// returned <see cref="Task"/> has been completed.
        /// </para>
        /// <para>
        /// The actor is created when the <see cref="IProjection{TEnvelope}"/> is started and the 
        /// <see cref="IActorRef"/> is passed in as a parameter here.
        /// </para>
        /// <para>
        /// You will typically use the `Ask` pattern to delegate the processing of the envelope to the 
        /// actor and the returned <see cref="Task"/> corresponds to the reply of the ask.
        /// </para>
        /// </summary>
        public abstract Task<Done> Process(IActorRef actorRef, TEnvelope envelope);        

        public Option<IActorRef> SetActor(IActorRef actorRef) => _actor = new Option<IActorRef>(actorRef);

        public IActorRef GetActor() => _actor.HasValue
            ? _actor.Value
            : throw new InvalidOperationException("Actor not started, please report issue at " +
                "https://github.com/ismaelhamed/Akka.Projections/issues");
    }
}
