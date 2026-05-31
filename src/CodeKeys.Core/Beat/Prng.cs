namespace CodeKeys.Core.Beat;

/// <summary>FNV-1a 32-bit string hash (UTF-16 code units), matching the TS <c>hashSeed</c>.</summary>
public static class Fnv
{
    public static uint Hash(string s)
    {
        uint h = 2166136261u;
        foreach (char c in s)
        {
            h ^= c;
            unchecked { h *= 16777619u; }
        }
        return h;
    }
}

/// <summary>
/// mulberry32 PRNG, bit-for-bit with the TS original. Deterministic from its seed, so the
/// whole beat system is reproducible and unit-testable. <see cref="Next"/> returns [0, 1).
/// </summary>
public sealed class Prng
{
    private uint _a;
    public Prng(uint seed) => _a = seed;

    public double Next()
    {
        unchecked
        {
            _a += 0x6d2b79f5u;
            uint t = (_a ^ (_a >> 15)) * (1u | _a);
            t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }
}
