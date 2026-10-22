using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Parsing;
using System.Numerics;

namespace ChessRealms.Engine.Tests;

internal class RepetitionKeyTests
{
    [Test]
    public void Identity_DistinguishesEveryPieceColorAndSquare()
    {
        // Geometric fixtures isolate identity; they are not playable games.
        var keys = new HashSet<RepetitionKey> { RepetitionKey.Create(new Position(), []) };
        for (int color = 0; color < 2; ++color)
        {
            for (int piece = 0; piece < 6; ++piece)
            {
                for (int square = 0; square < 64; ++square)
                {
                    var position = new Position();
                    position.SetPieceAt(square, piece, color);
                    Assert.That(keys.Add(RepetitionKey.Create(position, [])), Is.True,
                        $"color={color}, piece={piece}, square={square}");
                }
            }
        }

        Assert.That(keys, Has.Count.EqualTo(769));
    }

    [Test]
    public void Identity_DistinguishesSideAndEveryCastlingCombination()
    {
        var keys = new HashSet<RepetitionKey>();
        var position = Position.CreateDefault();
        for (int color = 0; color < 2; ++color)
        {
            for (int rights = 0; rights < 16; ++rights)
            {
                position.color = color;
                position.castlings = rights;
                Assert.That(keys.Add(RepetitionKey.Create(position, [])), Is.True);
            }
        }

        Assert.That(keys, Has.Count.EqualTo(32));
    }

    [Test]
    public void Identity_IgnoresBothCounters_EvenBeyondInt64()
    {
        var position = Position.CreateDefault();
        var before = RepetitionKey.Create(position, []);
        position.halfMoveClock = BigInteger.Pow(10, 80);
        position.fullMoveCount = BigInteger.Pow(10, 90);
        var after = RepetitionKey.Create(position, []);
        Assert.That(after, Is.EqualTo(before));
        Assert.That(after.GetHashCode(), Is.EqualTo(before.GetHashCode()));
    }

    [TestCase("7k/8/8/3p4/8/8/8/7K w - d6 0 1", false)]
    [TestCase("7k/8/8/3pP3/8/8/8/7K w - d6 0 1", true)]
    [TestCase("k3r3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", false)]
    [TestCase("7k/8/8/8/3Pp3/8/8/7K b - d3 0 1", true)]
    [TestCase("4k3/8/8/8/3Pp3/8/8/K3R3 b - d3 0 1", false)]
    public void Identity_RetainsOnlyLegallyAvailableEnPassant(string fen, bool distinct)
    {
        Assert.That(FenParser.TryParse(fen, out var position), Is.True);
        Span<int> moves = stackalloc int[MoveGen.MaxMoves];
        int count = LegalMoves.Write(position, moves);
        var withTarget = RepetitionKey.Create(position, moves[..count]);
        position.enpassant = Squares.Empty;
        count = LegalMoves.Write(position, moves);
        var withoutTarget = RepetitionKey.Create(position, moves[..count]);
        Assert.That(withTarget != withoutTarget, Is.EqualTo(distinct));
    }

    [Test]
    public void Identity_DistinguishesDifferentLegalEnPassantTargetsOnTheSameBoard()
    {
        Assert.That(FenParser.TryParse("7k/8/8/1pPpP3/8/8/8/7K w - b6 0 1", out var left), Is.True);
        Assert.That(FenParser.TryParse("7k/8/8/1pPpP3/8/8/8/7K w - d6 0 1", out var right), Is.True);
        Span<int> moves = stackalloc int[MoveGen.MaxMoves];
        int count = LegalMoves.Write(left, moves);
        var leftKey = RepetitionKey.Create(left, moves[..count]);
        count = LegalMoves.Write(right, moves);
        var rightKey = RepetitionKey.Create(right, moves[..count]);
        Assert.That(leftKey, Is.Not.EqualTo(rightKey));
    }

    [Test]
    public void Dictionary_HashCollisionsDoNotMergeDifferentPositions()
    {
        var position = Position.CreateDefault();
        var white = RepetitionKey.Create(position, []);
        position.SwitchColor();
        var black = RepetitionKey.Create(position, []);
        var counts = new Dictionary<RepetitionKey, int>(new CollidingComparer())
        {
            [white] = 3,
            [black] = 1
        };
        Assert.That(counts, Has.Count.EqualTo(2));
        Assert.That(counts[white], Is.EqualTo(3));
        Assert.That(counts[black], Is.EqualTo(1));
    }

    [TestCase(17)]
    [TestCase(12345)]
    public void ReplayUndoAndBranch_MatchNormalizedFenIdentity(int seed)
    {
        var game = new ChessGame();
        var random = new Random(seed);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var identities = new Dictionary<RepetitionKey, string>();
        var line = new List<string>();
        AddPosition();
        // Start with an actual repeat, then compare less regular positions too.
        foreach (string move in "g1f3 g8f6 f3g1 f6g8".Split(' '))
        {
            game.MakeMove(CoordinateMove.Parse(move));
            AddPosition();
        }

        PlayRandom(120);
        int undoCount = Math.Min(30, game.PlyCount);
        for (int i = 0; i < undoCount; ++i)
        {
            --counts[line[^1]];
            line.RemoveAt(line.Count - 1);
            Assert.That(game.UndoMove(), Is.True);
            Assert.That(game.RepetitionCount, Is.EqualTo(counts[line[^1]]));
        }

        game = game.Clone();
        PlayRandom(60);

        void PlayRandom(int plies)
        {
            for (int i = 0; i < plies && !game.IsFinished; ++i)
            {
                var moves = game.GetLegalMoves();
                Assert.That(game.MakeMove(moves[random.Next(moves.Count)]), Is.Not.EqualTo(MoveResult.None));
                AddPosition();
            }
        }

        void AddPosition()
        {
            var position = game.Position;
            Span<int> moves = stackalloc int[MoveGen.MaxMoves];
            int count = LegalMoves.Write(position, moves);
            var key = RepetitionKey.Create(position, moves[..count]);
            bool legalEnPassant = false;
            foreach (int move in moves[..count])
            {
                legalEnPassant |= BinaryMoveOps.DecodeEnpassant(move) != 0;
            }

            string[] fields = game.ToFen().Split(' ');
            if (!legalEnPassant)
            {
                fields[3] = "-";
            }

            string reference = string.Join(' ', fields.Take(4));
            if (identities.TryGetValue(key, out string? previous))
            {
                Assert.That(reference, Is.EqualTo(previous));
            }
            else
            {
                Assert.That(identities.Values, Does.Not.Contain(reference));
                identities.Add(key, reference);
            }

            int occurrences = counts.GetValueOrDefault(reference) + 1;
            counts[reference] = occurrences;
            line.Add(reference);
            Assert.That(game.RepetitionCount, Is.EqualTo(occurrences));
        }
    }

    private sealed class CollidingComparer : IEqualityComparer<RepetitionKey>
    {
        public bool Equals(RepetitionKey x, RepetitionKey y) => x.Equals(y);
        public int GetHashCode(RepetitionKey obj) => 0;
    }
}
