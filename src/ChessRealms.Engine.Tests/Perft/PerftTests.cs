using ChessRealms.Engine.Parsing;
using Perft;

namespace ChessRealms.Engine.Tests.Perft;

internal class PerftTests
{
    // FEN and leaf-node counts verified against https://www.chessprogramming.org/Perft_Results
    // Sections Initial Position and Position 2–6 (including the mirrored Position 4).
    // Kiwipete's omitted clocks are normalized to 0 1; clocks do not affect perft.
    private static readonly (string Name, string Fen, ulong[] Nodes)[] Positions =
    [
        ("Initial", FenStrings.StartPosition, [20, 400, 8902, 197281, 4865609, 119060324]),
        ("Kiwipete", "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", [48, 2039, 97862, 4085603, 193690690]),
        ("Position3_EnPassant", "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", [14, 191, 2812, 43238, 674624, 11030083]),
        ("Position4_Promotions", "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", [6, 264, 9467, 422333, 15833292]),
        ("Position4_Mirrored", "r2q1rk1/pP1p2pp/Q4n2/bbp1p3/Np6/1B3NBn/pPPP1PPP/R3K2R b KQ - 0 1", [6, 264, 9467, 422333, 15833292]),
        ("Position5_PromotionCapture", "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", [44, 1486, 62379, 2103487, 89941194]),
        ("Position6", "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10", [46, 2079, 89890, 3894594, 164075551])
    ];

    private static IEnumerable<TestCaseData> FastCases() => Cases(deep: false);
    private static IEnumerable<TestCaseData> DeepCases() => Cases(deep: true);

    private static IEnumerable<TestCaseData> Cases(bool deep)
    {
        foreach (var (name, fen, nodes) in Positions)
        {
            for (int depth = 1; depth <= nodes.Length; depth++)
            {
                if ((depth > 3) == deep)
                    yield return new TestCaseData(fen, depth, nodes[depth - 1])
                        .SetName($"{(deep ? "Deep" : "Fast")}_{name}_Depth{depth}");
            }
        }
    }

    [TestCaseSource(nameof(FastCases)), Category("Perft")]
    public void Fast(string fen, int depth, ulong expected) => AssertNodes(fen, depth, expected);

    [TestCaseSource(nameof(DeepCases)), Category("Perft"), Category("Deep")]
    [Explicit("Expensive perft: select with --filter TestCategory=Deep")]
    public void Deep(string fen, int depth, ulong expected) => AssertNodes(fen, depth, expected);

    private static void AssertNodes(string fen, int depth, ulong expected)
    {
        Assert.That(FenStrings.TryParse(fen, out var position), Is.True, fen);
        Assert.That(PerftDriver.Test(position, depth, upper: false).Nodes, Is.EqualTo(expected),
            $"FEN: {fen}; depth: {depth}");
    }

    // Leaf-only special-move statistics from the same source, not just total nodes.
    [TestCase(1, 3, 17102, 45, 3162, 0)]
    [TestCase(2, 3, 209, 2, 0, 0)]
    [TestCase(3, 2, 87, 0, 6, 48)]
    [TestCase(4, 2, 87, 0, 6, 48)]
    [Category("Perft")]
    public void SpecialMoveCounts(int index, int depth, int captures, int ep, int castles, int promotions)
    {
        Assert.That(FenStrings.TryParse(Positions[index].Fen, out var position), Is.True);
        var result = PerftDriver.Test(position, depth, upper: false);
        Assert.Multiple(() =>
        {
            Assert.That(result.Captures, Is.EqualTo(captures));
            Assert.That(result.Ep, Is.EqualTo(ep));
            Assert.That(result.Castles, Is.EqualTo(castles));
            Assert.That(result.Promotions, Is.EqualTo(promotions));
        });
    }
}
