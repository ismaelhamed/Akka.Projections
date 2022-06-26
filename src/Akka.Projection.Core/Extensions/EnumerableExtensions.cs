//-----------------------------------------------------------------------
// <copyright file="EnumerableExtensions.cs" company="Akka.NET Project">
//     Copyright (C) 2021 Ismael Hamed <https://github.com/ismaelhamed/Akka.Projection>
// </copyright>
//-----------------------------------------------------------------------

using System.Linq;

namespace System.Collections.Generic
{
    public static class EnumerableExtensions
    {
        public static void Deconstruct<T>(this IEnumerable<T> list, out T head, out List<T> tail)
        {
            var enumerable = list as T[] ?? list.ToArray();
            head = enumerable.FirstOrDefault();
            tail = new List<T>(enumerable.Skip(1));
        }
    }
}
