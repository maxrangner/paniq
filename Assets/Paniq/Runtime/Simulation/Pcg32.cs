using System;

namespace Paniq.Simulation
{
    /// <summary>PCG32 XSH-RR using the reference seeding procedure.</summary>
    public struct Pcg32
    {
        public const ulong ReferenceSequence = 54UL;
        private ulong state;
        private ulong increment;

        public Pcg32(ulong initState, ulong initSequence = ReferenceSequence)
        {
            state = 0UL;
            increment = (initSequence << 1) | 1UL;
            NextUInt();
            state = unchecked(state + initState);
            NextUInt();
        }

        public ulong State => state;
        public ulong Increment => increment;

        public uint NextUInt()
        {
            ulong oldState = state;
            state = unchecked(oldState * 6364136223846793005UL + increment);
            uint xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            uint rotation = (uint)(oldState >> 59);
            uint rotateRight = xorshifted >> (int)rotation;
            uint rotateLeft = xorshifted << (int)((32U - rotation) & 31U);
            return rotateRight | rotateLeft;
        }

        public int NextIntInclusive(int minimum, int maximum)
        {
            if (minimum > maximum)
            {
                throw new ArgumentOutOfRangeException(nameof(minimum));
            }

            uint range = checked((uint)(maximum - minimum + 1));
            return minimum + (int)(NextUInt() % range);
        }

        /// <summary>True with the given whole-number percentage chance.</summary>
        public bool NextPercent(int percent)
        {
            return NextUInt() % 100U < (uint)Math.Max(0, percent);
        }
    }
}
