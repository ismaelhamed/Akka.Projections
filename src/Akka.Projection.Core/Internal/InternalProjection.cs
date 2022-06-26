//-----------------------------------------------------------------------
// <copyright file="InternalProjectionState.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Annotations;

namespace Akka.Projections.Internal
{
    [InternalApi]
    public interface IInternalProjection
    {
        [InternalApi]
        IOffsetStrategy OffsetStrategy();
    }
}
