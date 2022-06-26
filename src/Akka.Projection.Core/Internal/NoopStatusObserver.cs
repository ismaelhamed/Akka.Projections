//-----------------------------------------------------------------------
// <copyright file="NoopStatusObserver.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Annotations;

namespace Akka.Projections.Internal
{
    [InternalApi]
    internal class NoopStatusObserver : StatusObserver<object>
    {
        public override void AfterProcess(ProjectionId projectionId, object envelope)
        {
        }

        public override void BeforeProcess(ProjectionId projectionId, object envelope)
        {
        }

        public override void Error(ProjectionId projectionId, object envelope, Exception exception, IHandlerRecoveryStrategy recoveryStrategy)
        {
        }

        public override void Failed(ProjectionId projectionId, Exception exception)
        {
        }

        public override void OffsetProgress(ProjectionId projectionId, object envelope)
        {
        }

        public override void Started(ProjectionId projectionId)
        {
        }

        public override void Stopped(ProjectionId projectionId)
        {
        }
    }
}
