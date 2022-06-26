//-----------------------------------------------------------------------
// <copyright file="MergeableOffset.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using Akka.Annotations;

namespace Akka.Projections
{
    [ApiMayChange]
    public class MergeableOffset<TOffset>
    {
        public MergeableOffset(Dictionary<string, TOffset> entries) => Entries = entries;
        public Dictionary<string, TOffset> Entries { get; }
    }
}
