using System;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// A count that grows by multiplying and adding, saturates at a cap instead of overflowing, and
    /// never goes negative. Integer math only, so doubling a bank 30 times gives the same number on
    /// every machine.
    /// </summary>
    public sealed class CountBank
    {
        public long Value { get; private set; }

        public long Cap { get; }

        public CountBank(long start, long cap)
        {
            Cap = Math.Max(0L, cap);
            Value = Clamp(start);
        }

        /// <summary>Multiplies the value. A factor of 0 (or less) empties the bank.</summary>
        public void Multiply(int factor)
        {
            if (factor <= 0 || Value == 0)
            {
                Value = 0;
                return;
            }

            // Compare before multiplying so it can never overflow.
            Value = Value > Cap / factor ? Cap : Value * factor;
        }

        public void Add(long amount)
        {
            if (amount >= 0)
            {
                Value = Value > Cap - amount ? Cap : Value + amount;
            }
            else
            {
                Value = amount < -Value ? 0 : Value + amount;
            }
        }

        /// <summary>Returns the current value and sets the bank to <paramref name="resetTo"/>.</summary>
        public long Drain(long resetTo)
        {
            long drained = Value;
            Value = Clamp(resetTo);
            return drained;
        }

        public void Reset(long value)
        {
            Value = Clamp(value);
        }

        long Clamp(long value)
        {
            return value < 0 ? 0 : (value > Cap ? Cap : value);
        }
    }
}
