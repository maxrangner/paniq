using System;
using System.Collections;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// A read-only look at the first <see cref="Count"/> entries of a list
    /// that only ever grows, such as the event log or the burning cells.
    /// Entries are immutable once added, so a snapshot can hold this view
    /// instead of copying the whole history every tick; later additions stay
    /// invisible to it.
    /// </summary>
    public sealed class AppendOnlyView<T> : IReadOnlyList<T>
    {
        private readonly List<T> source;

        internal AppendOnlyView(List<T> source)
        {
            this.source = source;
            Count = source.Count;
        }

        public int Count { get; }

        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return source[index];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return source[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
