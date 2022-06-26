//-----------------------------------------------------------------------
// <copyright file="ProjectionActor.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Projections.Internal;
using Akka.Util;
using static Akka.Projections.ProjectionActor;

namespace Akka.Projections
{
    internal interface IProjectionManagementCommand : ICommand 
    { }

    internal sealed class GetOffset : IProjectionManagementCommand
    {
        public ProjectionId ProjectionId { get; }
        public IActorRef ReplyTo { get; }

        public GetOffset(ProjectionId projectionId, IActorRef replyTo)
        {
            ProjectionId = projectionId;
            ReplyTo = replyTo;
        }
    }

    internal sealed class CurrentOffset
    {
        public ProjectionId ProjectionId { get; }
        public Option<object> Offset { get; }

        public CurrentOffset(ProjectionId projectionId, Option<object> offset)
        {
            ProjectionId = projectionId;
            Offset = offset;
        }
    }

    internal sealed class GetOffsetResult : IProjectionManagementCommand
    {
        public Option<object> Offset { get; }
        public IActorRef ReplyTo { get; }

        public GetOffsetResult(Option<object> offset, IActorRef replyTo)
        {
            Offset = offset;
            ReplyTo = replyTo;
        }
    }

    internal sealed class ManagementOperationException : IProjectionManagementCommand
    {
        public ICommand Command { get; }
        public Exception Cause { get; }

        public ManagementOperationException(ICommand command, Exception cause)
        {
            Command = command;
            Cause = cause;
        }
    }

    internal sealed class SetOffset : IProjectionManagementCommand
    {
        public ProjectionId ProjectionId { get; }
        public Option<object> Offset { get; }
        public IActorRef ReplyTo { get; }

        public SetOffset(ProjectionId projectionId, Option<object> offset, IActorRef replyTo)
        {
            ProjectionId = projectionId;
            Offset = offset;
            ReplyTo = replyTo;
        }
    }

    internal sealed class SetOffsetResult : IProjectionManagementCommand
    {
        public IActorRef ReplyTo { get; }
        public SetOffsetResult(IActorRef replyTo) => ReplyTo = replyTo;
    }

    internal sealed class IsPaused : IProjectionManagementCommand
    {
        public ProjectionId ProjectionId { get; }
        public IActorRef ReplyTo { get; }

        public IsPaused(ProjectionId projectionId, IActorRef replyTo)
        {
            ProjectionId = projectionId;
            ReplyTo = replyTo;
        }
    }

    internal sealed class SetPaused : IProjectionManagementCommand
    {
        public ProjectionId ProjectionId { get; }
        public bool Paused { get; }
        public IActorRef ReplyTo { get; }

        public SetPaused(ProjectionId projectionId, bool paused, IActorRef replyTo)
        {
            ProjectionId = projectionId;
            Paused = paused;
            ReplyTo = replyTo;
        }
    }

    internal sealed class SetPausedResult : IProjectionManagementCommand
    {
        public IActorRef ReplyTo { get; }
        public SetPausedResult(IActorRef replyTo) => ReplyTo = replyTo;
    }

    internal sealed class GetManagementStateResult : IProjectionManagementCommand
    {
        public Option<ManagementState> State { get; }
        public IActorRef ReplyTo { get; }

        public GetManagementStateResult(Option<ManagementState> state, IActorRef replyTo)
        {
            State = state;
            ReplyTo = replyTo;
        }
    }
}
