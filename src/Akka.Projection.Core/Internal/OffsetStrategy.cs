//-----------------------------------------------------------------------
// <copyright file="ActorHandlerInit.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Annotations;
using Akka.Projections.Dsl;
using Akka.Streams.Dsl;
using Akka.Util;
using System;
using System.Collections.Immutable;

namespace Akka.Projections.Internal
{
    [InternalApi]
    public interface IOffsetStrategy
    {
    }

    [InternalApi]
    public sealed class AtMostOnce : IOffsetStrategy
    {
        public Option<IStrictRecoveryStrategy> RecoveryStrategy { get; }
        public AtMostOnce(Option<IStrictRecoveryStrategy> recoveryStrategy) => RecoveryStrategy = recoveryStrategy;
    }

    [InternalApi]
    public sealed class ExactlyOnce : IOffsetStrategy
    {
        public Option<IHandlerRecoveryStrategy> RecoveryStrategy { get; }
        public ExactlyOnce(Option<IHandlerRecoveryStrategy> recoveryStrategy) => RecoveryStrategy = recoveryStrategy;
    }

    [InternalApi]
    public sealed class AtLeastOnce : IOffsetStrategy
    {
        public Option<int> AfterEnvelopes { get; }
        public Option<TimeSpan> OrAfterDuration { get; }
        public Option<IHandlerRecoveryStrategy> RecoveryStrategy { get; }

        public AtLeastOnce(Option<int> afterEnvelopes, Option<TimeSpan> orAfterDuration, Option<IHandlerRecoveryStrategy> recoveryStrategy)
        {
            AfterEnvelopes = afterEnvelopes;
            OrAfterDuration = orAfterDuration;
            RecoveryStrategy = recoveryStrategy;
        }
    }

    [InternalApi]
    public interface IHandlerStrategy
    {
        void RecreateHandlerOnNextAccess();
        HandlerLifecycle Lifecycle();
        Option<IActorHandlerInit> ActorHandlerInit();
    }

    [InternalApi]
    public abstract class FunctionHandlerStrategy<TEnvelope> : IHandlerStrategy
    {
        private readonly Func<Handler<TEnvelope>> _handlerFactory;
        private Option<Handler<TEnvelope>> _handler = Option<Handler<TEnvelope>>.None;
        private bool _recreateHandlerOnNextAccess = true;

        public FunctionHandlerStrategy(Func<Handler<TEnvelope>> handlerFactory) => _handlerFactory = handlerFactory;

        /// <summary>
        /// Current handler instance, or lazy creation of it.
        /// </summary>
        public Handler<TEnvelope> Handler()
        {
            if (_handler.HasValue && !_recreateHandlerOnNextAccess)
            {
                return _handler.Value;
            }
            else
            {
                CreateHandler();
                _recreateHandlerOnNextAccess = false;
                return _handler.Value;
            }
        }

        private void CreateHandler()
        {
            var newHandler = _handlerFactory();
            if (_handler.HasValue && _handler.Value is IActorHandlerInit h1 && newHandler != null && newHandler is IActorHandlerInit h2)
            {
                // use same actor in new handler
                h2.SetActor(h1.GetActor());
            }
            _handler = newHandler;
        }

        public Option<IActorHandlerInit> ActorHandlerInit() => Handler() switch
        {
            IActorHandlerInit init => new Option<IActorHandlerInit>(init),
            _ => Option<IActorHandlerInit>.None
        };

        public HandlerLifecycle Lifecycle() => Handler();

        public void RecreateHandlerOnNextAccess() => _recreateHandlerOnNextAccess = true;
    }

    [InternalApi]
    public sealed class SingleHandlerStrategy<TEnvelope> : FunctionHandlerStrategy<TEnvelope>
    {
        public SingleHandlerStrategy(Func<Handler<TEnvelope>> handlerFactory)
            : base(handlerFactory)
        {
        }
    }

    [InternalApi]
    public sealed class GroupedHandlerStrategy<TEnvelope> : FunctionHandlerStrategy<ImmutableList<TEnvelope>>
    {
        public Option<int> AfterEnvelopes { get; }
        public Option<TimeSpan> OrAfterDuration { get; }

        public GroupedHandlerStrategy(Func<Handler<ImmutableList<TEnvelope>>> handlerFactory)
            : this(handlerFactory, Option<int>.None, Option<TimeSpan>.None)
        {
        }

        public GroupedHandlerStrategy(Func<Handler<ImmutableList<TEnvelope>>> handlerFactory, Option<int> afterEnvelopes)
            : this(handlerFactory, afterEnvelopes, Option<TimeSpan>.None)
        {
        }

        public GroupedHandlerStrategy(Func<Handler<ImmutableList<TEnvelope>>> handlerFactory, Option<TimeSpan> orAfterDuration)
            : this(handlerFactory, Option<int>.None, orAfterDuration)
        {
        }

        public GroupedHandlerStrategy(Func<Handler<ImmutableList<TEnvelope>>> handlerFactory, Option<int> afterEnvelopes, Option<TimeSpan> orAfterDuration)
            : base(handlerFactory)
        {
            AfterEnvelopes = afterEnvelopes;
            OrAfterDuration = orAfterDuration;
        }
    }

    [InternalApi]
    public sealed class FlowHandlerStrategy<TEnvelope> : IHandlerStrategy
    {
        public FlowWithContext<TEnvelope, IProjectionContext, Done, IProjectionContext, object> FlowCtx { get; }

        public FlowHandlerStrategy(FlowWithContext<TEnvelope, IProjectionContext, Done, IProjectionContext, object> flowCtx) => FlowCtx = flowCtx;

        public Option<IActorHandlerInit> ActorHandlerInit() => Option<IActorHandlerInit>.None;

        public HandlerLifecycle Lifecycle() => new HandlerLifecycle();

        public void RecreateHandlerOnNextAccess() { }
    }
}
