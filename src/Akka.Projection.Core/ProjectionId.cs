//-----------------------------------------------------------------------
// <copyright file="ProjectionId.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Annotations;

namespace Akka.Projections
{
    [ApiMayChange]
    public struct ProjectionId
    {
        /// <summary>
        /// The projection name is shared across multiple instances of <see cref="IProjection{TEnvelope}"/> with different keys.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The key must be unique for a projection name.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// The unique id formed by the concatenation of `name` and `key`. A dash (-) is used as separator.
        /// </summary>
        public string Id => $"{Name}-{Key}";

        /// <summary>
        /// Constructs a <see cref="ProjectionId"/>.
        /// <para>
        /// A <see cref="ProjectionId"/> is composed by a name and a key.
        /// </para>
        /// <para>
        /// The projection name is shared across multiple instances of <see cref="IProjection{TEnvelope}"/> with different keys.
        /// For example, a "user-view" could be the name of a projection.
        /// </para>
        /// <para>
        /// The key must be unique for a projection name.
        /// For example, a "user-view" could have multiple projections with different keys representing different partitions,
        /// shards, etc.
        /// </para>
        /// </summary>
        /// <param name="name">The projection name.</param>
        /// <param name="key">The unique key. The key must be unique for a projection name.</param>
        public static ProjectionId Create(string name, string key) =>
            new ProjectionId(name, key);

        /// <summary>
        /// Constructs a Set of <see cref="ProjectionId"/>.
        /// <para>
        /// A <see cref="ProjectionId"/> is composed by a name and a key.
        /// </para>
        /// <para>
        /// The projection name is shared across multiple instances of <see cref="IProjection{TEnvelope}"/> with different keys.
        /// For example, a "user-view" could be the name of a projection.
        /// </para>
        /// <para>
        /// The key must be unique for a projection name.
        /// For example, a "user-view" could have multiple projections with different keys representing different partitions,
        /// shards, etc.
        /// </para>
        /// </summary>
        /// <param name="name">The projection name.</param>
        /// <param name="keys">The set of keys to associated with the passed name.</param>
        public static ImmutableList<ProjectionId> Create(string name, IEnumerable<string> keys) =>
            keys.Select(key => new ProjectionId(name, key)).ToImmutableList();

        private ProjectionId(string name, string key)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name), "name must not be null or empty");
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key), "key must not be null or empty");

            Name = name;
            Key = key;
        }

        public static bool operator ==(ProjectionId proj1, ProjectionId proj2) => proj1.Equals(proj2);

        public static bool operator !=(ProjectionId proj1, ProjectionId proj2) => !proj1.Equals(proj2);

        public override string ToString() => $"ProjectionId({Name}, {Key})";

        public override bool Equals(object obj) => obj is ProjectionId id && Id == id.Id;

        public override int GetHashCode() => 2108858624 + EqualityComparer<string>.Default.GetHashCode(Id);
    }
}
