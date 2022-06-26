//-----------------------------------------------------------------------
// <copyright file="ProjectionContextImpl.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Actor;
using Akka.Annotations;

namespace Akka.Projections.Internal
{
    [InternalApi]
    internal class ProjectionContextImpl<TOffset, TEnvelope> : IProjectionContext
    {
        public TOffset Offset { get; }
        public TEnvelope Envelope { get; }
        public IActorRef ExternalContext { get; }
        public int GroupSize { get; }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="offset">TBD</param>
        /// <param name="envelope">TBD</param>
        /// <param name="externalContext">TBD</param>
        /// <param name="groupSize">GroupSize is used only in GroupHandlerStrategies so a single context instance can report that multiple envelopes were processed.</param>
        private ProjectionContextImpl(TOffset offset, TEnvelope envelope, IActorRef externalContext, int groupSize)
        {
            Offset = offset;
            Envelope = envelope;
            ExternalContext = externalContext;
            GroupSize = groupSize;
        }

        [InternalApi]
        public static ProjectionContextImpl<TOffset, TEnvelope> Create(TOffset offset, TEnvelope envelope, IActorRef externalContext) =>
            new ProjectionContextImpl<TOffset, TEnvelope>(offset, envelope, externalContext, groupSize: 1);

        public ProjectionContextImpl<TOffset, TEnvelope> Copy(TOffset offset = default, TEnvelope envelope = default, IActorRef externalContext = default, int? groupSize = default) =>
            new ProjectionContextImpl<TOffset, TEnvelope>(offset ?? Offset, envelope ?? Envelope, externalContext ?? ExternalContext, groupSize ?? GroupSize);

        public void Deconstruct(out TOffset offset, out TEnvelope envelope, out IActorRef externalContext, out int groupSize)
        {
            offset = Offset;
            envelope = Envelope;
            externalContext = ExternalContext; 
            groupSize = GroupSize;
        }
    }
}