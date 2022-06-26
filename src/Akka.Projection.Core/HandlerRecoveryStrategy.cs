//-----------------------------------------------------------------------
// <copyright file="HandlerRecoveryStrategy.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Annotations;

namespace Akka.Projections
{
    [ApiMayChange]
    public interface IHandlerRecoveryStrategy { }
    public interface IStrictRecoveryStrategy : IHandlerRecoveryStrategy { }
    public interface IRetryRecoveryStrategy : IHandlerRecoveryStrategy { }

    /// <summary>
    /// Error handling strategy when processing an Envelope fails. The default is defined in configuration .
    /// </summary>
    [ApiMayChange]
    public class HandlerRecoveryStrategy
    {
        /// <summary>
        /// If the first attempt to invoke the handler fails it will immediately give up and fail the stream.
        /// </summary>
        public static IStrictRecoveryStrategy Fail => new Internal.Fail();

        /// <summary>
        /// If the first attempt to invoke the handler fails it will immediately give up, discard the element and continue with next.
        /// </summary>
        public static IStrictRecoveryStrategy Skip => new Internal.Skip();

        /// <summary>
        /// If the first attempt to invoke the handler fails it will retry invoking the handler with the same 
        /// envelope this number of retries with the delay between each attempt. It will give up and fail the stream 
        /// if all attempts fail.
        /// </summary>
        public static IHandlerRecoveryStrategy RetryAndFail(int retries, TimeSpan delay) =>
            retries < 1 ? Fail : (IHandlerRecoveryStrategy)new Internal.RetryAndFail(retries, delay);

        /// <summary>
        /// If the first attempt to invoke the handler fails it will retry invoking the handler with the same 
        /// envelope this number of retries with the delay between each attempt. It will give up, discard the element 
        /// and continue with next if all attempts fail.
        /// </summary>
        public static IHandlerRecoveryStrategy RetryAndSkip(int retries, TimeSpan delay) =>
            retries < 1 ? Fail : (IHandlerRecoveryStrategy)new Internal.RetryAndSkip(retries, delay);

        [InternalApi]
        private class Internal
        {
            public class Fail : IStrictRecoveryStrategy, IRetryRecoveryStrategy { }
            public class Skip : IStrictRecoveryStrategy, IRetryRecoveryStrategy { }

            public sealed class RetryAndFail : IRetryRecoveryStrategy
            {
                public int Retries { get; }
                public TimeSpan Delay { get; }

                public RetryAndFail(int retries, TimeSpan delay)
                {
                    if (retries <= 0) throw new ArgumentException("retries must be > 0", nameof(retries));

                    Retries = retries;
                    Delay = delay;
                }
            }

            public sealed class RetryAndSkip : IRetryRecoveryStrategy
            {
                public int Retries { get; }
                public TimeSpan Delay { get; }

                public RetryAndSkip(int retries, TimeSpan delay)
                {
                    if (retries <= 0) throw new ArgumentException("retries must be > 0", nameof(retries));

                    Retries = retries;
                    Delay = delay;
                }
            }
        }
    }    
}
