//-----------------------------------------------------------------------
// <copyright file="ProjectionSerializer.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Runtime.Serialization;
using Akka.Actor;
using Akka.Annotations;
using Akka.Serialization;
using Akka.Util;
using Google.Protobuf;

namespace Akka.Projections.Internal
{
    [InternalApi]
    public class ProjectionSerializer : SerializerWithStringManifest
    {
        private const string GetOffsetManifest = "a";
        private const string CurrentOffsetManifest = "b";
        private const string SetOffsetManifest = "c";
        private const string IsPausedManifest = "d";
        private const string SetPausedManifest = "e";

        private readonly Lazy<OffsetSerialization> _offsetSerialization;

        public ProjectionSerializer(ExtendedActorSystem system)
            : base(system)
        {
            _offsetSerialization = new Lazy<OffsetSerialization>(() => new OffsetSerialization(system));
        }

        public override string Manifest(object o)
        {
            return o switch
            {
                GetOffset _ => GetOffsetManifest,
                CurrentOffset _ => CurrentOffsetManifest,
                SetOffset _ => SetOffsetManifest,
                IsPaused _ => IsPausedManifest,
                SetPaused _ => SetPausedManifest,
                _ => throw new ArgumentException($"Can't serialize object of type ${o.GetType().Name} in [${nameof(ProjectionSerializer)}]")
            };
        }

        public override object FromBinary(byte[] bytes, string manifest)
        {
            return manifest switch
            {
                GetOffsetManifest => GetOffsetFromBinary(bytes),
                CurrentOffsetManifest => CurrentOffsetFromBinary(bytes),
                SetOffsetManifest => SetOffsetFromBinary(bytes),
                IsPausedManifest => IsPausedFromBinary(bytes),
                SetPausedManifest => SetPausedFromBinary(bytes),
                _ => throw new SerializationException($"Unimplemented deserialization of message with manifest [{manifest}] in [{nameof(ProjectionSerializer)}]")
            };
        }

        public override byte[] ToBinary(object obj)
        {
            return obj switch
            {
                GetOffset getOffset => GetOffsetToBinary(getOffset),
                CurrentOffset currentOffset => CurrentOffsetToBinary(currentOffset),
                SetOffset setOffset => SetOffsetToBinary(setOffset),
                IsPaused isPaused => IsPausedToBinary(isPaused),
                SetPaused setPaused => SetPausedToBinary(setPaused),
                _ => throw new ArgumentException($"Cannot serialize object of type [${obj.GetType().Name}]")
            };
        }

        private byte[] GetOffsetToBinary(GetOffset m)
        {
            return new ProjectionMessages.GetOffset
            {
                ProjectionId = ProjectionIdToProto(m.ProjectionId),
                ReplyTo = SerializeActorRef(m.ReplyTo)
            }.ToByteArray();
        }

        private object GetOffsetFromBinary(byte[] bytes)
        {
            var currentOffset = ProjectionMessages.GetOffset.Parser.ParseFrom(bytes);
            return new GetOffset(ProjectionIdFromProto(currentOffset.ProjectionId), DeserializeActorRef(currentOffset.ReplyTo));
        }

        private byte[] CurrentOffsetToBinary(CurrentOffset m)
        {
            return new ProjectionMessages.CurrentOffset
            {
                ProjectionId = ProjectionIdToProto(m.ProjectionId),
                Offset = OffsetToProto(m.ProjectionId, m.Offset.Value)
            }.ToByteArray();
        }

        private object CurrentOffsetFromBinary(byte[] bytes)
        {
            var currentOffset = ProjectionMessages.CurrentOffset.Parser.ParseFrom(bytes);
            var safeOffset = currentOffset.Offset.HasValue ? new Option<object>(OffsetFromProto(currentOffset.Offset)) : Option<object>.None;
            return new CurrentOffset(ProjectionIdFromProto(currentOffset.ProjectionId), safeOffset);
        }

        private byte[] SetOffsetToBinary(SetOffset m)
        {
            return new ProjectionMessages.SetOffset
            {
                ProjectionId = ProjectionIdToProto(m.ProjectionId),
                ReplyTo = SerializeActorRef(m.ReplyTo),
                Offset = m.Offset.HasValue ? OffsetToProto(m.ProjectionId, m.Offset) : default
            }.ToByteArray();
        }

        private object SetOffsetFromBinary(byte[] bytes)
        {
            var setOffset = ProjectionMessages.SetOffset.Parser.ParseFrom(bytes);
            var offset = setOffset.Offset.HasValue ? new Option<object>(OffsetFromProto(setOffset.Offset)) : Option<object>.None;
            return new SetOffset(ProjectionIdFromProto(setOffset.ProjectionId), offset, DeserializeActorRef(setOffset.ReplyTo));
        }

        private byte[] IsPausedToBinary(IsPaused m)
        {
            return new ProjectionMessages.IsPaused
            {
                ProjectionId = ProjectionIdToProto(m.ProjectionId),
                ReplyTo = SerializeActorRef(m.ReplyTo)
            }.ToByteArray();
        }

        private object IsPausedFromBinary(byte[] bytes)
        {
            var isPaused = ProjectionMessages.IsPaused.Parser.ParseFrom(bytes);
            return new IsPaused(ProjectionIdFromProto(isPaused.ProjectionId), DeserializeActorRef(isPaused.ReplyTo));
        }

        private byte[] SetPausedToBinary(SetPaused m)
        {
            return new ProjectionMessages.SetPaused
            {
                ProjectionId = ProjectionIdToProto(m.ProjectionId),
                ReplyTo = SerializeActorRef(m.ReplyTo),
                Paused = m.Paused
            }.ToByteArray();
        }

        private object SetPausedFromBinary(byte[] bytes)
        {
            var setPaused = ProjectionMessages.SetPaused.Parser.ParseFrom(bytes);
            return new SetPaused(ProjectionIdFromProto(setPaused.ProjectionId), setPaused.Paused, DeserializeActorRef(setPaused.ReplyTo));
        }

        private ProjectionMessages.ProjectionId ProjectionIdToProto(ProjectionId projectionId) =>
            new ProjectionMessages.ProjectionId { Name = projectionId.Name, Key = projectionId.Key };

        private ProjectionId ProjectionIdFromProto(ProjectionMessages.ProjectionId p) => ProjectionId.Create(p.Name, p.Key);

        private ProjectionMessages.Offset OffsetToProto(ProjectionId projectionId, object offset)
        {
            var storageRepresentation = _offsetSerialization.Value.ToStorageRepresentation(projectionId, offset) switch
            {
                OffsetSerialization.SingleOffset s => s,
                _ => throw new ArgumentException("MultipleOffsets not supported yet.")
            };

            return new ProjectionMessages.Offset
            {
                Manifest = storageRepresentation.Manifest,
                Value = storageRepresentation.Offset
            };
        }

        private object OffsetFromProto(ProjectionMessages.Offset o) =>
            _offsetSerialization.Value.FromStorageRepresentation<object>(o.Value, o.Manifest);

        private string SerializeActorRef(IActorRef actorRef) =>
            actorRef.Path.ToSerializationFormatWithAddress(system.Provider.DefaultAddress);

        private IActorRef DeserializeActorRef(string replyTo) =>
            system.Provider.ResolveActorRef(replyTo);
    }
}
