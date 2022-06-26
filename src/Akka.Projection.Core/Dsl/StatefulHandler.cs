//-----------------------------------------------------------------------
// <copyright file="ProjectionActor.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Annotations;

namespace Akka.Projections.Dsl
{
    [ApiMayChange]
    public abstract class StatefulHandler<TState, TEnvelope> : Handler<TEnvelope>
    {
        private Task<TState> _state;

        /// <summary>
        /// Invoked to load the initial state when the projection is started or if previous <see cref="Process(TState, TEnvelope)"/> failed.
        /// </summary>
        public abstract Task<TState> InitialState();

        /// <summary>
        /// The process method is invoked for each <typeparamref name="TEnvelope"/>. One envelope is processed at a time. The returned 
        /// `Task{TState}` is to be completed when the processing of the <paramref name="envelope"/> has finished. It will not be 
        /// invoked with the next envelope until after the returned `Task` has been completed.
        /// <para>
        /// The state is the completed value of the previously returned `Task{TState}` or the <see cref="InitialState"/>. If 
        /// the previously returned `Task{TState}` failed it will call initialState again and use that value.
        /// </para>
        /// </summary>
        public abstract Task<TState> Process(TState state, TEnvelope envelope);

        /// <summary>
        /// Calls <see cref="InitialState"/> when the projection is started.
        /// </summary>
        public override Task<Done> Start()
        {
            _state = InitialState();
            return _state.ContinueWith(_ => Done.Instance);
        }

        /// <summary>
        /// Calls <see cref="Process(TState, TEnvelope)"/> with the completed value of the previously returned 
        /// `Task{TState}` or the <see cref="InitialState"/>. If the previously returned `Task{TState}` failed 
        /// it will call <see cref="InitialState"/> again and use that value.
        /// </summary>
        public override Task<Done> Process(TEnvelope envelope)
        {
            Task<TState> newState;

            if (_state.IsFaulted || _state.IsCanceled)
                newState = InitialState();                
            else if (_state.IsCompleted) // must be IsCompletedSuccessfully
                newState = _state;
            else
                throw new InvalidOperationException(
                    "Process called before previous Task completed. " +
                    "Did you share the same handler instance between several Projection instances? " + 
                    "Otherwise, please report issue at " + 
                    "https://github.com/ismaelhamed/Akka.Projections/issues");

            _state = newState.FlatMap(s => Process(s, envelope));
            return _state.ContinueWith(_ => Done.Instance);
        }
    }
}
