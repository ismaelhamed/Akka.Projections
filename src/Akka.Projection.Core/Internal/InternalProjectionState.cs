//-----------------------------------------------------------------------
// <copyright file="InternalProjectionState.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Annotations;
using Akka.Event;
using Akka.Projections.Dsl;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;
using Akka.Util.Internal;
using static Akka.Projections.OffsetVerification;

namespace Akka.Projections.Internal
{
    [InternalApi]
    internal abstract class InternalProjectionState<TOffset, TEnvelope>
    {
        private readonly ProjectionId _projectionId;
        private readonly SourceProvider<TOffset, TEnvelope> _sourceProvider;
        private readonly IOffsetStrategy _offsetStrategy;
        private readonly IHandlerStrategy _handlerStrategy;
        private readonly StatusObserver<TEnvelope> _statusObserver;
        private readonly ProjectionSettings _settings;
        private readonly SharedKillSwitch _killSwitch;
        private readonly TaskCompletionSource<Done> _abort = new TaskCompletionSource<Done>();

        protected InternalProjectionState(ProjectionId projectionId, SourceProvider<TOffset, TEnvelope> sourceProvider, IOffsetStrategy offsetStrategy, IHandlerStrategy handlerStrategy, StatusObserver<TEnvelope> statusObserver, ProjectionSettings settings)
        {
            _projectionId = projectionId;
            _sourceProvider = sourceProvider;
            _offsetStrategy = offsetStrategy;
            _handlerStrategy = handlerStrategy;
            _statusObserver = statusObserver;
            _settings = settings;

            _killSwitch = KillSwitches.Shared(projectionId.Id);
        }

        protected abstract ILoggingAdapter Logger { get; }

        protected abstract Task<bool> ReadPaused();
        protected abstract Task<Option<TOffset>> ReadOffsets();
        protected abstract Task<Done> SaveOffset(ProjectionId projectionId, TOffset offset);

        protected Task<Done> SaveOffsetAndReport(ProjectionId projectionId, ProjectionContextImpl<TOffset, TEnvelope> projectionContext, int batchSize) =>
            SaveOffset(projectionId, projectionContext.Offset)
                .Map(done =>
                {
                    try
                    {
                        _statusObserver.OffsetProgress(projectionId, projectionContext.Envelope);
                    }
                    catch (Exception)
                    {
                        // ignore
                    }

                    return Done.Instance;
                });

        protected Task<Done> SaveOffsetsAndReport(ProjectionId projectionId, ImmutableList<ProjectionContextImpl<TOffset, TEnvelope>> batch)
        {
            // The batch contains multiple projections contexts. Each of these contexts may represent
            // a single envelope or a group of envelopes. The size of the batch and the size of the
            // group may differ.
            var totalNumberOfEnvelopes = batch.Sum(context => context.GroupSize);
            var last = batch.Last();

            return SaveOffsetAndReport(projectionId, last, totalNumberOfEnvelopes);
        }

        /// <summary>
        /// A convenience method to serialize asynchronous operations to occur one after another is complete
        /// </summary>
        private Task<Done> Serialize(
            Dictionary<string, ImmutableList<ProjectionContextImpl<TOffset, TEnvelope>>> batches,
            Func<string, ImmutableList<ProjectionContextImpl<TOffset, TEnvelope>>, Task<Done>> op)
        {
            const int logProgressEvery = 5;
            var size = batches.Count;

            Task<Done> Loop(IEnumerable<(string, ImmutableList<ProjectionContextImpl<TOffset, TEnvelope>>)> remaining, int n)
            {
                if (remaining is null)
                    return Task.FromResult(Done.Instance);

                // Deconstruction FTW!
                var ((key, batch), tail) = remaining;
                return op(key, batch).FlatMap(_ =>
                {
                    if (n % logProgressEvery == 0)
                        Logger.Debug("Processed batches [{0}] of [{1}]", n, size);

                    return Loop(tail, n + 1);
                });
            }

            var result = Loop(batches.Select(pair => (pair.Key, pair.Value)), 1);
            result.ContinueWith(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                    Logger.Error(t.Exception, "Processing of batches failed");
                else
                    Logger.Debug("Processing completed of [{0}] batches", size);
            });

            return result;
        }

        private Source<Done, NotUsed> AtLeastOnceProcessing(
            Source<ProjectionContextImpl<TOffset, TEnvelope>, NotUsed> source,
            int afterEnvelopes,
            TimeSpan orAfterDuration,
            IHandlerRecoveryStrategy recoveryStrategy)
        {
            Flow<ProjectionContextImpl<TOffset, TEnvelope>, ProjectionContextImpl<TOffset, TEnvelope>, NotUsed> atLeastOnceHandlerFlow = null;

            switch (_handlerStrategy)
            {
                case SingleHandlerStrategy<TEnvelope> single:
                    {
                        var handler = single.Handler();
                        var handlerRecovery = HandlerRecoveryImpl.Create<TOffset, TEnvelope>(_projectionId, recoveryStrategy, Logger, _statusObserver);

                        atLeastOnceHandlerFlow = Flow.Create<ProjectionContextImpl<TOffset, TEnvelope>>()
                            .SelectAsync(1, context =>
                            {
                                Task<Done> Measured() => handler.Process(context.Envelope).Map(done =>
                                {
                                    _statusObserver.AfterProcess(_projectionId, context.Envelope);
                                    return done;
                                });

                                return handlerRecovery
                                    .ApplyRecovery(context.Envelope, context.Offset, context.Offset, _abort.Task, Measured)
                                    .ContinueWith(_ => context);
                            });
                    }
                    break;
                case GroupedHandlerStrategy<TEnvelope> grouped:
                    {
                        var groupAfterEnvelopes = grouped.AfterEnvelopes.GetOrElse(_settings.GroupAfterEnvelopes);
                        var groupAfterDuration = grouped.OrAfterDuration.GetOrElse(_settings.GroupAfterDuration);
                        var handler = grouped.Handler();
                        var handlerRecovery = HandlerRecoveryImpl.Create<TOffset, TEnvelope>(_projectionId, recoveryStrategy, Logger, _statusObserver);

                        atLeastOnceHandlerFlow = Flow.Create<ProjectionContextImpl<TOffset, TEnvelope>>()
                            .GroupedWithin(groupAfterEnvelopes, groupAfterDuration)
                            .Where(g => g.Any())
                            .SelectAsync(1, group =>
                            {
                                var first = group.First();
                                var last = group.Last();
                                var envelopes = group.Select(g => g.Envelope);

                                Task<Done> Measured() => handler.Process(envelopes.ToImmutableList()).Map(done =>
                                {
                                    group.ForEach(ctx => _statusObserver.AfterProcess(_projectionId, ctx.Envelope));
                                    return done;
                                });

                                return handlerRecovery
                                    .ApplyRecovery(first.Envelope, first.Offset, last.Offset, _abort.Task, Measured)
                                    .ContinueWith(_ => last.Copy(groupSize: envelopes.Count()));
                            });
                    }
                    break;
                case FlowHandlerStrategy<TEnvelope> f:
                    {
                        var flow = f.FlowCtx.AsFlow().WatchTermination((_, taskDone) =>
                            taskDone.ContinueWith(_ => taskDone).Unwrap());

                        atLeastOnceHandlerFlow = Flow.Create<ProjectionContextImpl<TOffset, TEnvelope>>()
                            .Select(context => (context.Envelope, (IProjectionContext)context))
                            .Via(flow)
                            .Select(pair =>
                            {
                                var ctx = (ProjectionContextImpl<TOffset, TEnvelope>)pair.Item2;
                                _statusObserver.AfterProcess(_projectionId, ctx.Envelope);
                                return ctx;
                            });
                    }
                    break;
            }

            if (afterEnvelopes == 1)
            {
                // optimization of general AtLeastOnce case
                return source.Via(atLeastOnceHandlerFlow)
                    .SelectAsync(1, context => SaveOffsetAndReport(_projectionId, context, context.GroupSize));
            }

            return source.Via(atLeastOnceHandlerFlow)
                .GroupedWithin(afterEnvelopes, orAfterDuration)
                .Where(c => c.Any())
                .SelectAsync(1, batch => SaveOffsetsAndReport(_projectionId, batch.ToImmutableList()));
        }

        private Source<Done, NotUsed> ExactlyOnceProcessing(
            Source<ProjectionContextImpl<TOffset, TEnvelope>, NotUsed> source,
            IHandlerRecoveryStrategy recoveryStrategy)
        {
            Task<Done> ProcessGrouped(
                Handler<ImmutableList<TEnvelope>> handler,
                HandlerRecoveryImpl<TOffset, TEnvelope> handlerRecovery,
                IEnumerable<ProjectionContextImpl<TOffset, TEnvelope>> envelopesAndOffsets)
            {
                Task<Done> ProcessEnvelopes(IEnumerable<ProjectionContextImpl<TOffset, TEnvelope>> partitioned)
                {
                    var first = partitioned.Head();
                    var firstOffset = first.Offset;
                    var lastOffset = partitioned.Last().Offset;
                    var envelopes = partitioned.Select(ctx => ctx.Envelope);

                    Task<Done> Measured() => handler.Process(envelopes.ToImmutableList()).Map(done =>
                    {
                        partitioned.ForEach(ctx => _statusObserver.AfterProcess(_projectionId, ctx.Envelope));
                        return done;
                    });

                    return handlerRecovery.ApplyRecovery(first.Envelope, firstOffset, lastOffset, _abort.Task, Measured);
                }

                switch (_sourceProvider)
                {
                    case MergeableOffsetSourceProvider<TOffset, TEnvelope> _:
                        {
                            var batches = envelopesAndOffsets
                                .SelectMany(context => context switch
                                {
                                    ProjectionContextImpl<TOffset, TEnvelope>(MergeableOffset<TOffset> offset, _, _, _) ctx =>
                                        offset.Entries.Select(pair => (pair.Key, ctx)),
                                    _ => // should never happen
                                        throw new InvalidOperationException("The offset should always be of type MergeableOffset"),
                                })
                                .GroupBy(pair => pair.Key, pair => pair.ctx)
                                .ToDictionary(g => g.Key, g => g.ToImmutableList());

                            // process batches in sequence, but not concurrently, in order to provide singled threaded guarantees
                            // to the user envelope handler
                            return Serialize(batches, (surrogateKey, partitionedEnvelopes) =>
                            {
                                Logger.Debug("Processing grouped envelopes for MergeableOffset with key [{0}]", surrogateKey);
                                return ProcessEnvelopes(partitionedEnvelopes);
                            });
                        }
                    default:
                        return ProcessEnvelopes(envelopesAndOffsets);
                }
            }

            var handlerRecovery = HandlerRecoveryImpl.Create<TOffset, TEnvelope>(_projectionId, recoveryStrategy, Logger, _statusObserver);

            switch (_handlerStrategy)
            {
                case SingleHandlerStrategy<TEnvelope> single:
                    {
                        var handler = single.Handler();
                        return source.SelectAsync(1, context =>
                        {
                            Task<Done> Measured() => handler.Process(context.Envelope).Map(done =>
                            {
                                _statusObserver.AfterProcess(_projectionId, context.Envelope);
                                try
                                {
                                    _statusObserver.OffsetProgress(_projectionId, context.Envelope);
                                }
                                catch (Exception)
                                {
                                    // ignore
                                }
                                return done;
                            });

                            return handlerRecovery.ApplyRecovery(context.Envelope, context.Offset, context.Offset, _abort.Task, Measured);
                        });
                    }
                case GroupedHandlerStrategy<TEnvelope> grouped:
                    {
                        var groupAfterEnvelopes = grouped.AfterEnvelopes.GetOrElse(_settings.GroupAfterEnvelopes);
                        var groupAfterDuration = grouped.OrAfterDuration.GetOrElse(_settings.GroupAfterDuration);
                        var handler = grouped.Handler();

                        return source
                            .GroupedWithin(groupAfterEnvelopes, groupAfterDuration)
                            .Where(g => g.Any())
                            .SelectAsync(1, group =>
                            {
                                var last = group.Last();
                                return ProcessGrouped(handler, handlerRecovery, group).Map(done =>
                                {
                                    try
                                    {
                                        _statusObserver.OffsetProgress(_projectionId, last.Envelope);
                                    }
                                    catch (Exception)
                                    {
                                        // ignore
                                    }
                                    return done;
                                });
                            });
                    }
                default:
                    // not possible, no API for this
                    throw new InvalidOperationException("Unsupported combination of ExactlyOnce and Flow");
            }
        }

        private Source<Done, NotUsed> AtMostOnceProcessing(
            Source<ProjectionContextImpl<TOffset, TEnvelope>, NotUsed> source,
            IHandlerRecoveryStrategy recoveryStrategy)
        {
            var handlerRecovery = HandlerRecoveryImpl.Create<TOffset, TEnvelope>(_projectionId, recoveryStrategy, Logger, _statusObserver);

            switch (_handlerStrategy)
            {
                case SingleHandlerStrategy<TEnvelope> single:
                    {
                        var handler = single.Handler();
                        return source.SelectAsync(1, context =>
                        {
                            Task<Done> Measured() => handler.Process(context.Envelope).Map(done =>
                            {
                                _statusObserver.AfterProcess(_projectionId, context.Envelope);
                                return done;
                            });

                            return handlerRecovery.ApplyRecovery(context.Envelope, context.Offset, context.Offset, _abort.Task, Measured);
                        })
                        .Select(_ => Done.Instance);
                    }
                default:
                    // not possible, no API for this
                    throw new InvalidOperationException("Unsupported combination of AtMostOnce and Grouped");
            }
        }

        public Source<Done, Task<Done>> MappedSource()
        {
            var handlerLifecycle = _handlerStrategy.Lifecycle();
            _statusObserver.Started(_projectionId);

            var source = Source.FromTaskSource(ReadPaused().ContinueWith(t =>
                {
                    if (t.Result)
                    {
                        Logger.Debug("Projection [{0}] started in resumed mode.", _projectionId);
                        return handlerLifecycle.TryStart().ContinueWith(_ => _sourceProvider.Source(() => ReadOffsets())).Unwrap();
                    }
                    else
                    {
                        Logger.Info("Projection [{0}] started in paused mode.", _projectionId);
                        // paused stream, no elements
                        return Task.FromResult(Source.Never<TEnvelope>());
                    }
                }).Unwrap())
                .Via(_killSwitch.Flow<TEnvelope>())
                .Select(env =>
                {
                    _statusObserver.BeforeProcess(_projectionId, env);
                    var externalContext = ActorRefs.Nobody; // TODO: Telemetry
                    return ProjectionContextImpl<TOffset, TEnvelope>.Create(_sourceProvider.ExtractOffset(env), env, externalContext);
                })
                .Where(context =>
                {
                    if (_sourceProvider is VerifiableSourceProvider<TOffset, TEnvelope> vsp)
                    {
                        switch (vsp.VerifyOffset(context.Offset))
                        {
                            case VerificationSuccess _:
                                return true;
                            case VerificationFailure failure:
                                Logger.Warning("Source provider instructed projection to skip offset [{0}] with reason: {1}", context.Offset, failure.Reason);
                                return false;
                            default:
                                return false;
                        }
                    }

                    return true;
                })
                .MapMaterializedValue(_ => NotUsed.Instance);

            var composedSource = _offsetStrategy switch
            {
                ExactlyOnce exactlyOnce => ExactlyOnceProcessing(source, exactlyOnce.RecoveryStrategy.GetOrElse(_settings.RecoveryStrategy)),
                AtLeastOnce atLeastOnce => AtLeastOnceProcessing(
                    source,
                    atLeastOnce.AfterEnvelopes.GetOrElse(_settings.SaveOffsetAfterEnvelopes),
                    atLeastOnce.OrAfterDuration.GetOrElse(_settings.SaveOffsetAfterDuration),
                    atLeastOnce.RecoveryStrategy.GetOrElse(_settings.RecoveryStrategy)),
                AtMostOnce atMostOnce => AtMostOnceProcessing(
                    source,
                    atMostOnce.RecoveryStrategy.HasValue ? atMostOnce.RecoveryStrategy.Value : _settings.RecoveryStrategy),
                _ => throw new ArgumentOutOfRangeException() // TODO: not part of the JVM API. Should we throw or just return null?
            };

            return StopHandlerOnTermination(composedSource, handlerLifecycle);
        }

        /// <summary>
        /// Adds a `WatchTermination` on the passed Source that will call the `StopHandler` on completion.
        /// The stopHandler function is called on success or failure. In case of failure, the original failure is preserved.
        /// </summary>
        private Source<Done, Task<Done>> StopHandlerOnTermination(Source<Done, NotUsed> src, HandlerLifecycle handlerLifecycle)
        {
            return src.WatchTermination((_, futDone) =>
            {
                _handlerStrategy.RecreateHandlerOnNextAccess();
                return futDone.ContinueWith(_ =>
                    handlerLifecycle.TryStop().ContinueWith(t =>
                    {
                        if (t.IsFaulted || t.IsCanceled)
                        {
                            var exception = t.Exception is AggregateException aggregateException
                                ? aggregateException.Flatten().InnerExceptions[0]
                                : t.Exception;

                            if (exception is RunningProjection.AbortProjectionException)
                            {
                                _statusObserver.Stopped(_projectionId); // no restart
                            }
                            else
                            {
                                _statusObserver.Failed(_projectionId, exception);
                                _statusObserver.Stopped(_projectionId);
                            }
                        }
                        else
                        {
                            _statusObserver.Stopped(_projectionId);
                        }
                        return t;
                    })).Unwrap().Unwrap();
            });
        }
    }

    [InternalApi]
    public sealed class ManagementState
    {
        public bool Paused { get; }
        public ManagementState(bool paused) => Paused = paused;
    }
}
