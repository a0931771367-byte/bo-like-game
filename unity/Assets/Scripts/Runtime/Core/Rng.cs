namespace BoLike.Core
{
    /// <summary>
    /// Seeded xorshift64* random source. Everything that varies inside a run (room draw, waves, damage rolls)
    /// goes through the run's Rng so a seed reproduces the run exactly for bug reports.
    /// </summary>
    public class Rng
    {
        ulong s;

        public Rng(ulong seed) { s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }

        public ulong NextU64()
        {
            s ^= s >> 12; s ^= s << 25; s ^= s >> 27;
            return s * 2685821657736338717UL;
        }

        /// <summary>[0, 1)</summary>
        public double NextDouble() => (NextU64() >> 11) * (1.0 / 9007199254740992.0);
        public float Value => (float)NextDouble();
        public bool Chance(double p) => NextDouble() < p;
        /// <summary>[min, max)</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(NextDouble() * (max - min));
        /// <summary>[min, max] inclusive, like Phaser.Math.Between.</summary>
        public int Between(int min, int max) => Range(min, max + 1);
        public float Range(float min, float max) => min + (float)NextDouble() * (max - min);

        /// <summary>Fresh seed from the clock for a new run.</summary>
        /// <summary>Seeds are 40-bit so the 8-char code round-trips exactly (8 × 5 bits).</summary>
        public const ulong SeedMask = (1UL << 40) - 1;
        public static ulong NewSeed()
        {
            ulong v = ((ulong)System.DateTime.UtcNow.Ticks ^ ((ulong)System.Environment.TickCount << 32)) & SeedMask;
            return v == 0 ? 1 : v;
        }

        /// <summary>Eight-character code shown to players (Crockford base32, no vowels to mistype).</summary>
        public static string SeedToCode(ulong seed)
        {
            const string A = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            var c = new char[8];
            ulong v = seed & SeedMask;
            for (int i = 7; i >= 0; i--) { c[i] = A[(int)(v & 31)]; v >>= 5; }
            return new string(c);
        }

        public static ulong CodeToSeed(string code)
        {
            const string A = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            ulong v = 0;
            foreach (char ch in code.Trim().ToUpperInvariant())
            {
                int i = A.IndexOf(ch);
                if (i < 0) continue;
                v = (v << 5) | (ulong)i;
            }
            v &= SeedMask;
            return v == 0 ? 1 : v;
        }
    }
}
