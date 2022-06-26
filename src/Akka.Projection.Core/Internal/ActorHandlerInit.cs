//-----------------------------------------------------------------------
// <copyright file="ActorHandlerInit.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Actor;
using Akka.Annotations;
using Akka.Util;

namespace Akka.Projections.Internal
{
    [InternalApi]
    public interface IActorHandlerInit
    {
        [InternalApi]
        Props Props { get; }

        [InternalApi]
        Option<IActorRef> SetActor(IActorRef actorRef);

        [InternalApi]
        IActorRef GetActor();
    }
}
