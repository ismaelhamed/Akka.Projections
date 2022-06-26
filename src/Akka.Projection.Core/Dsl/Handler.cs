//-----------------------------------------------------------------------
// <copyright file="Handler.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Annotations;

namespace Akka.Projections.Dsl
{
    /// <summary>
    /// <para>
    /// Implement this interface for the `TEnvelope` handler in the <see cref="IProjection{TEnvelope}"/>. 
    /// Some projections may have more specific handler types.
    /// </para>
    /// <para>
    /// It can be stateful, with variables and mutable data structures. It is invoked by the <see cref="IProjection{TEnvelope}"/> 
    /// machinery one envelope at a time and visibility guarantees between the invocations are handled automatically, 
    /// i.e. no volatile or other concurrency primitives are needed for managing the state.
    /// </para>
    /// Supported error handling strategies for when processing an `TEnvelope` fails can be defined in configuration or 
    /// using the `WithRecoveryStrategy` method of a <see cref="IProjection{TEnvelope}"/> implementation.
    /// </summary>
    public abstract class Handler<TEnvelope> : HandlerLifecycle
    {
        /// The process method is invoked for each `Envelope`. One envelope is processed at a time. The returned <see cref="Task"/> is to be completed 
        /// when the processing of the envelope has finished. It will not be invoked with the next envelope until after the returned <see cref="Task"/> 
        /// has been completed.
        public abstract Task<Done> Process(TEnvelope envelop);

        /// <summary>
        /// Handler that can be define from a simple function
        /// </summary>
        [InternalApi]
        private class HandlerFunction : Handler<TEnvelope>
        {
            private readonly Func<TEnvelope, Task<Done>> _handler;
            public HandlerFunction(Func<TEnvelope, Task<Done>> handler) => _handler = handler;

            public override Task<Done> Process(TEnvelope envelop) => _handler(envelop);
        }

        public static Handler<TEnvelope> FromFunction(Func<TEnvelope, Task<Done>> handler) => new HandlerFunction(handler);
    }

    [ApiMayChange]
    public class HandlerLifecycle
    {
        /// <summary>
        /// Invoked when the projection is starting, before first envelope is processed. Can be overridden to implement initialization. 
        /// It is also called when the <see cref="IProjection{TEnvelope}"/> is restarted after a failure.
        /// </summary>
        public virtual Task<Done> Start() => Task.FromResult(Done.Instance);

        /// <summary>
        /// Invoked when the projection has been stopped. Can be overridden to implement resource cleanup. 
        /// It is also called when the <see cref="IProjection{TEnvelope}"/> is restarted after a failure.
        /// </summary>
        public Task<Done> Stop() => Task.FromResult(Done.Instance);

        [InternalApi]
        public Task<Done> TryStart()
        {
            try
            {
                return Start();
            }
            catch (Exception ex)
            {
                return Task.FromException<Done>(ex); // in case the call throws
            }
        }

        [InternalApi]
        public Task<Done> TryStop()
        {
            try
            {
                return Stop();
            }
            catch (Exception ex)
            {
                return Task.FromException<Done>(ex); // in case the call throws
            }
        }
    }
}
