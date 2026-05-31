using CodeKeys.Core.Beat;
using Xunit;

namespace CodeKeys.Tests;

public class MotifTests
{
    private const int Diatonic = 7;   // major / dorian / minor
    private const int Pentatonic = 5;

    private static Motif Gen(uint seed = 12345u, int degrees = Diatonic) =>
        MotifFactory.Generate(seed, degrees);

    [Fact]
    public void Generate_Is_Deterministic()
    {
        Assert.Equal(Gen().Notes, Gen().Notes); // same seed → identical note sequence
    }

    [Fact]
    public void Different_Seeds_Usually_Differ()
    {
        // Not a hard guarantee, but across a handful of seeds we should see more than one tune.
        var distinct = new HashSet<string>();
        for (uint s = 1; s <= 8; s++)
            distinct.Add(string.Join(",", Gen(s).Notes.Select(n => $"{n.Degree}@{n.Step}")));
        Assert.True(distinct.Count > 1);
    }

    [Theory]
    [InlineData(Diatonic)]
    [InlineData(Pentatonic)]
    public void Degrees_Stay_In_Register(int degrees)
    {
        int top = System.Math.Max(4, degrees);
        Assert.All(Gen(777u, degrees).Notes, n => Assert.InRange(n.Degree, 0, top));
    }

    [Fact]
    public void Steps_Are_Sorted_Distinct_And_Inside_One_Bar()
    {
        var steps = Gen().Notes.Select(n => n.Step).ToList();
        Assert.Equal(steps.OrderBy(x => x).ToList(), steps);     // sorted
        Assert.Equal(steps.Distinct().Count(), steps.Count);     // distinct
        Assert.All(steps, s => Assert.InRange(s, 0, Motif.Steps - 1));
    }

    [Fact]
    public void Articulates_The_Downbeat()
    {
        Assert.Contains(Gen().Notes, n => n.Step == 0);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(42u)]
    [InlineData(98765u)]
    public void Starts_And_Ends_On_A_Stable_Tone(uint seed)
    {
        var notes = Gen(seed).Notes;
        Assert.Contains(notes[0].Degree, new[] { 0, 2, 4 });
        Assert.Contains(notes[^1].Degree, new[] { 0, 2, 4 });
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(7u)]
    [InlineData(31337u)]
    public void Is_Not_A_Straight_Ascending_Scale(uint seed)
    {
        // Regression guard for the old `degree = (s/2) % span` run: a real motif must not be a
        // strictly +1 ascending sequence of degrees.
        var degs = Gen(seed).Notes.Select(n => n.Degree).ToList();
        bool strictlyAscendingByOne = degs.Zip(degs.Skip(1), (a, b) => b - a).All(d => d == 1);
        Assert.False(strictlyAscendingByOne);
    }

    [Fact]
    public void WithResolvedEnding_Lands_On_The_Tonic()
    {
        var resolved = MotifFactory.WithResolvedEnding(Gen());
        Assert.Equal(0, resolved.Notes[^1].Degree);
    }

    [Fact]
    public void Transpose_Shifts_Every_Degree()
    {
        var m = Gen();
        var up = MotifFactory.Transpose(m, 2);
        Assert.Equal(
            m.Notes.Select(n => n.Degree + 2).ToList(),
            up.Notes.Select(n => n.Degree).ToList());
    }

    [Fact]
    public void Invert_Mirrors_Around_The_First_Note()
    {
        var m = Gen();
        int axis = m.Notes[0].Degree;
        var inv = MotifFactory.Invert(m);
        Assert.Equal(
            m.Notes.Select(n => 2 * axis - n.Degree).ToList(),
            inv.Notes.Select(n => n.Degree).ToList());
        Assert.Equal(axis, inv.Notes[0].Degree); // the axis note maps to itself
    }
}
