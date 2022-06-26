//-----------------------------------------------------------------------
// <copyright file="OffsetSerialization.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Annotations;
using Akka.Persistence.Query;

namespace Akka.Projections.Internal
{
    [InternalApi]
    internal class OffsetSerialization
    {
        public interface IStorageRepresentation
        { }

        public sealed class SingleOffset : IStorageRepresentation
        {
            public ProjectionId Id { get; }
            public string Manifest { get; }
            public string Offset { get; }
            public bool Mergeable { get; }

            public SingleOffset(ProjectionId id, string manifest, string offset, bool mergeable = false)
            {
                Id = id;
                Manifest = manifest;
                Offset = offset;
                Mergeable = mergeable;
            }

            public void Deconstruct(out ProjectionId id, out string manifest, out string offset, out bool mergeable)
            {
                id = Id;
                manifest = Manifest;
                offset = Offset;
                mergeable = Mergeable;
            }
        }

        public sealed class MultipleOffsets : IStorageRepresentation
        {
            public ImmutableList<SingleOffset> Reps { get; }

            public MultipleOffsets(IEnumerable<SingleOffset> reps) => Reps = ImmutableList.CreateRange(reps);

            public void Deconstruct(out IEnumerable<SingleOffset> reps) => reps = Reps;
        }

        private const string StringManifest = "STR";
        private const string LongManifest = "LNG";
        private const string IntManifest = "INT";
        private const string SequenceManifest = "SEQ";
        private const string TimeBasedUUIDManifest = "TBU";

        private readonly Serialization.Serialization _serialization;

        public OffsetSerialization(ActorSystem system) => _serialization = system.Serialization;

        /// <summary>
        /// Deserialize an offset from a storage representation of one or more offsets. The offset 
        /// is converted from its string representation to its real type.
        /// </summary>
        public TOffset FromStorageRepresentation<TOffset, TInner>(IStorageRepresentation rep)
        {
            TOffset GetMergeableOffset(IEnumerable<SingleOffset> reps)
            {
                var offsets = reps
                    .Select(s => new KeyValuePair<string, TInner>(s.Id.Key, FromStorageRepresentation<TInner>(s.Offset, s.Manifest)))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);

                return (TOffset)Convert.ChangeType(new MergeableOffset<TInner>(offsets), typeof(TOffset));
            }

            return rep switch
            {
                SingleOffset(_, var manifest, var offsetStr, _) => FromStorageRepresentation<TOffset>(offsetStr, manifest),
                MultipleOffsets(var reps) => GetMergeableOffset(reps),
                _ => throw new ArgumentException("Unsupported IStorageRepresentation [{0}]", rep.GetType().Name),
            };
        }

        /// <summary>
        /// Deserialize an offset from a stored string representation and manifest. The offset is 
        /// converted from its string representation to its real type.
        /// </summary>
        public TOffset FromStorageRepresentation<TOffset>(string offsetStr, string manifest)
        {
            object DeserializeFromManifest()
            {
                var parts = manifest.Split(':');
                var serializerId = Convert.ToInt32(parts[0]);
                var serializerManifest = parts[1];
                var bytes = Convert.FromBase64String(offsetStr);
                return _serialization.Deserialize(bytes, serializerId, serializerManifest);
            }

            object offset = manifest switch
            {
                StringManifest => offsetStr,
                LongManifest => Convert.ToInt64(offsetStr),
                IntManifest => Convert.ToInt32(offsetStr),
                SequenceManifest => Offset.Sequence(Convert.ToInt64(offsetStr)),
                TimeBasedUUIDManifest => Offset.TimeBasedUuid(Guid.Parse(offsetStr)),
                _ => DeserializeFromManifest()
            };

            return (TOffset)Convert.ChangeType(offset, typeof(TOffset));
        }

        /// <summary>
        /// Convert the offset to a tuple (string, string) where the first element is the string 
        /// representation of the offset and the second its manifest.
        /// </summary>
        public IStorageRepresentation ToStorageRepresentation<TOffset>(ProjectionId id, TOffset offset, bool mergeable = false)
        {
            switch (offset)
            {
                case string s:
                    return new SingleOffset(id, StringManifest, s, mergeable);
                case long l:
                    return new SingleOffset(id, LongManifest, l.ToString(), mergeable);
                case int i:
                    return new SingleOffset(id, IntManifest, i.ToString(), mergeable);
                case Sequence seq:
                    return new SingleOffset(id, SequenceManifest, seq.Value.ToString(), mergeable);
                case TimeBasedUuid tbu:
                    return new SingleOffset(id, TimeBasedUUIDManifest, tbu.Value.ToString(), mergeable);
                case MergeableOffset<TOffset> mrg:
                    {
                        var list = mrg.Entries.Select(e => (SingleOffset)ToStorageRepresentation(ProjectionId.Create(id.Name, e.Key), e.Value, mergeable: true));
                        return new MultipleOffsets(list);
                    }
                default:
                    {
                        var serializer = _serialization.FindSerializerFor(offset);
                        var serializerId = serializer.Identifier;
                        var serializerManifest = Serialization.Serialization.ManifestFor(serializer, offset);
                        var bytes = serializer.ToBinary(offset);
                        var offsetStr = Convert.ToBase64String(bytes);
                        return new SingleOffset(id, $"{serializerId}:{serializerManifest}", offsetStr);
                    }
            }
        }
    }
}
