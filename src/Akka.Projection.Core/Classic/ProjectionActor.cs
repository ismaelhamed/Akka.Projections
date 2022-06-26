//-----------------------------------------------------------------------
// <copyright file="ProjectionActor.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Actor;
using Akka.Projections.Dsl;
using Akka.Util;
using static Akka.Projections.ProjectionActor;

namespace Akka.Projections
{
    public static class ProjectionActor
    {
        public interface ICommand { }

        /// <summary>
        /// The ProjectionActor and its Projection can be stopped with this message.
        /// </summary>
        public class Stop : ICommand
        {
            public static Stop Instance { get; } = new Stop();
            private Stop() { }
        }

        /// <summary>
        /// Creates a <see cref="ProjectionActor"/> for the passed projections.
        /// </summary>
        public static Props Create<TOffset, TEnvelope>(IProjection<TEnvelope> projection) =>
            Props.Create<ProjectionActor<TOffset, TEnvelope>>(projection);
    }

    internal class ProjectionActor<TOffset, TEnvelope> : UntypedActor, IWithUnboundedStash
    {
        private readonly IProjection<TEnvelope> _projection;
        private ProjectionId ProjectionId => _projection.ProjectionId;

        public IStash Stash { get; set; }

        /// <summary>
        /// Creates the Props of a <see cref="ProjectionActor{TOffset, TEnvelope}"/> for the passed projections.
        /// </summary>
        public static Props Create(IProjection<TEnvelope> projection) =>
            Props.Create(() => new ProjectionActor<TOffset, TEnvelope>(projection));

        private ProjectionActor(IProjection<TEnvelope> projection) => _projection = projection;

        protected override void PreStart()
        {
            base.PreStart();

            Context.System.Log.Info("Starting projection [{0}]", ProjectionId);
            if (_projection.ActorHandlerInit() is { HasValue: true } handlerInit)
            {
                var init = handlerInit.Value;
                var actorRef = Context.ActorOf(init.Props);
                init.SetActor(actorRef);

                Context.System.Log.Debug("Started actor handler [{0}] for projection [{1}]", actorRef, ProjectionId);
            }

            var running = _projection.Run(Context.System);
            if (running.GetType().IsAssignableFrom(typeof(IRunningProjectionManagement<>)))
            {
                ProjectionManagement.Get(Context.System).Register(_projection.ProjectionId, Context.Self);
            }

            Context.Become(Started(running));
        }

        protected override void OnReceive(object message)
        {
            // ignore
        }

        private Receive Started(IRunningProjection running)
        {
            return message =>
            {
                switch (message)
                {
                    case Stop _:
                        {
                            Context.System.Log.Debug("Projection [{0}] is being stopped", ProjectionId);
                            var stoppedTask = running.Stop();
                            // we send a Stopped for whatever completes the Task Success or Failure,
                            // doesn't matter, since the internal stream is by then stopped
                            stoppedTask.ContinueWith(_ => Stopped.Instance).PipeTo(Self);
                            Context.Become(Stopping);
                            return true;
                        }
                    case GetOffset getOffset:
                        {
                            if (getOffset is IRunningProjectionManagement<TOffset> mgmt)
                            {
                                if (getOffset.ProjectionId == ProjectionId)
                                {
                                    mgmt.GetOffset().PipeTo(Self,
                                        success: offset => new GetOffsetResult(offset, getOffset.ReplyTo),
                                        failure: ex => new ManagementOperationException(getOffset, ex));
                                }
                            }
                            else base.Unhandled(message);

                            return true;
                        }
                    case GetOffsetResult result:
                        ReceiveGetOffsetResult(result);
                        return true;
                    case SetOffset setOffset:
                        {
                            if (setOffset is IRunningProjectionManagement<TOffset> mgmt)
                            {
                                if (setOffset.ProjectionId == ProjectionId)
                                {
                                    Context.System.Log.Info("Offset will be changed to [{0}] for projection [{1}]. The Projection will be restarted.",
                                        setOffset.Offset, ProjectionId);

                                    running.Stop().ContinueWith(t => Stopped.Instance).PipeTo(Self);
                                    Context.Become(SettingOffset(setOffset, mgmt));
                                }
                            }
                            else base.Unhandled(message);

                            return true;
                        }
                    case ManagementOperationException op:
                        Context.System.Log.Warning(op.Cause, "Operation [{0}] failed.", op.Command);
                        return true;
                    case IsPaused isPaused:
                        {
                            if (isPaused is IRunningProjectionManagement<TOffset> mgmt)
                            {
                                if (isPaused.ProjectionId == ProjectionId)
                                {
                                    mgmt.GetManagementState().PipeTo(Self,
                                        success: state => new GetManagementStateResult(state, isPaused.ReplyTo),
                                        failure: ex => new ManagementOperationException(isPaused, ex));
                                }
                            }
                            else base.Unhandled(message);

                            return true;
                        }
                    case GetManagementStateResult result:
                        result.ReplyTo.Tell(result.State.HasValue && result.State.Value.Paused);
                        return true;
                    case SetPaused setPaused:
                        {
                            if (setPaused is IRunningProjectionManagement<TOffset> mgmt)
                            {
                                if (setPaused.ProjectionId == ProjectionId)
                                {
                                    Context.System.Log.Info("Running state will be changed to [{0}] for projection [{1}].",
                                        setPaused.Paused ? "paused" : "resumed",
                                        ProjectionId);

                                    running.Stop().ContinueWith(t => Stopped.Instance).PipeTo(Self);
                                    Context.Become(SettingPaused(setPaused, mgmt));
                                }
                                else
                                {
                                    // not for this projectionId
                                }
                            }
                            else base.Unhandled(message);

                            return true;
                        }
                }
                return false;
            };
        }

        private Receive SettingOffset(SetOffset setOffset, IRunningProjectionManagement<TOffset> mgmt)
        {
            return message =>
            {
                switch (message)
                {
                    case Stopped _:
                        {
                            Context.System.Log.Debug("Projection [{0}] stopped", ProjectionId);

                            // FIXME: not quiet sure this is alright
                            var offset = setOffset.Offset.HasValue ? new Option<TOffset>((TOffset)setOffset.Offset.Value) : Option<TOffset>.None;

                            mgmt.SetOffset(offset).PipeTo(Self,
                                success: _ => new SetOffsetResult(setOffset.ReplyTo),
                                failure: ex => new ManagementOperationException(setOffset, ex));

                            return true;
                        }
                    case SetOffsetResult result:
                        {
                            Context.System.Log.Info("Starting projection [{0}] after setting offset to [{1}]",
                                ProjectionId, setOffset.Offset);

                            var running = _projection.Run(Context.System);
                            result.ReplyTo.Tell(Done.Instance);
                            Stash.UnstashAll();
                            Context.Become(Started(running));

                            return true;
                        }
                    case ManagementOperationException op:
                        {
                            Context.System.Log.Warning(op.Cause, "Operation [{0}] failed.", op.Command);
                            var running = _projection.Run(Context.System);
                            Stash.UnstashAll();
                            Context.Become(Started(running));
                            return true;
                        }
                    default:
                        Stash.Stash();
                        return true;
                }
            };
        }

        private void Stopping(object message)
        {
            if (message is Stopped)
            {
                Context.System.Log.Debug("Projection [{0}] stopped", ProjectionId);
                Context.Stop(Self);
                return;
            }

            Context.System.Log.Debug("Projection [{0}] is being stopped. Discarding [{1}].", ProjectionId, message);
            base.Unhandled(message);
        }

        private void ReceiveGetOffsetResult(GetOffsetResult result) =>
            result.ReplyTo.Tell(new CurrentOffset(ProjectionId, result.Offset));

        private Receive SettingPaused(SetPaused setPaused, IRunningProjectionManagement<TOffset> mgmt)
        {
            return message =>
            {
                switch (message)
                {
                    case Stopped _:
                        {
                            Context.System.Log.Debug("Projection [{0}] stopped", ProjectionId);

                            mgmt.SetPaused(setPaused.Paused).PipeTo(Self,
                                success: _ => new SetPausedResult(setPaused.ReplyTo),
                                failure: ex => new ManagementOperationException(setPaused, ex));

                            return true;
                        }
                    case SetPausedResult result:
                        {
                            Context.System.Log.Info("Starting projection [{0}] in {1} mode.",
                                ProjectionId,
                                setPaused.Paused ? "paused" : "resumed");

                            var running = _projection.Run(Context.System);
                            result.ReplyTo.Tell(Done.Instance);
                            Stash.UnstashAll();
                            Context.Become(Started(running));
                            return true;
                        }
                    case ManagementOperationException op:
                        {
                            Context.System.Log.Warning(op.Cause, "Operation [{0}] failed.", op.Command);
                            // start anyway, but no reply
                            var running = _projection.Run(Context.System);
                            Stash.UnstashAll();
                            Context.Become(Started(running));
                            return true;
                        }
                    default:
                        Stash.Stash();
                        return true;
                }
            };
        }

        private class Stopped : ICommand
        {
            public static Stopped Instance { get; } = new Stopped();
            private Stopped() { }
        }
    }
}
