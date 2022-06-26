//-----------------------------------------------------------------------
// <copyright file="OffsetVerification.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Annotations;

namespace Akka.Projections
{
    public interface IOffsetVerification { }

    [ApiMayChange]
    public class OffsetVerification
    {
        public class VerificationSuccess : IOffsetVerification
        {
            public VerificationSuccess Instance { get; } = new VerificationSuccess();
            private VerificationSuccess() { }
        }

        public class VerificationFailure : IOffsetVerification
        {
            public string Reason { get; }
            public VerificationFailure(string reason) => Reason = reason;
        }

        /// <summary>
        /// Used when verifying offsets as part of transaction.
        /// </summary>
        [InternalApi]
        public class VerificationFailureException : Exception
        {
            public VerificationFailureException() : base("Offset verification failed")
            { }
        }
    }
}
