//-----------------------------------------------------------------------
// <copyright file="ProjectionActor.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Data;
using System.Threading.Tasks;
using Akka.Annotations;
using Akka.Streams.Dsl;
using Akka.Util;

namespace Akka.Projections.Dsl
{
    [ApiMayChange]
    public abstract class SourceProvider<TOffset, TEnvelope>
    {
        public abstract Task<Source<TEnvelope, NotUsed>> Source(Func<Task<Option<TOffset>>> offset);
        public abstract TOffset ExtractOffset(TEnvelope envelope);

        /// <summary>
        /// Timestamp (in millis-since-epoch) of the instant when the envelope was created. The meaning of "when the 
        /// envelope was created" is implementation specific and could be an instant on the producer machine, or the 
        /// instant when the database persisted the envelope, or other.
        /// </summary>
        public abstract long ExtractCreationTime(TEnvelope envelope);
    }

    [ApiMayChange]
    public abstract class VerifiableSourceProvider<TOffset, TEnvelope> : SourceProvider<TOffset, TEnvelope>
    {
        public abstract IOffsetVerification VerifyOffset(TOffset offset);
    }

    [ApiMayChange]
    public abstract class MergeableOffsetSourceProvider<TOffset, TEnvelope> : SourceProvider<TOffset, TEnvelope>
    {
        public MergeableOffsetSourceProvider()
        {
            // HACK: due to limitations of generic type constraint in csharp
            if (typeof(TOffset) != typeof(MergeableOffset<TOffset>))
                throw new InvalidConstraintException("Only `TOffset` of type `MergeableOffset<TOffset>` is supported");
        }
    }
}
