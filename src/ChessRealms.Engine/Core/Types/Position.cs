using ChessRealms.Engine.Core.Attacks;
using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Extensions;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Debugs;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Numerics;

namespace ChessRealms.Engine.Core.Types;


/// <summary>A value snapshot. Assignment copies bitboards; its BigInteger counters are immutable.</summary>
internal struct Position : IEquatable<Position>
{
    internal PieceBoards pieceBBs;
    internal OccupancyBoards blockers;

    internal int color;
    internal int castlings;
    internal int enpassant;

    internal BigInteger fullMoveCount;
    internal BigInteger halfMoveClock;

    public bool Equals(Position other)
    {
        for (int i = 0; i < 12; i++) if (pieceBBs[i] != other.pieceBBs[i]) return false;
        for (int i = 0; i < 3; i++) if (blockers[i] != other.blockers[i]) return false;
        return color == other.color && castlings == other.castlings && enpassant == other.enpassant
            && fullMoveCount == other.fullMoveCount && halfMoveClock == other.halfMoveClock;
    }

    public override bool Equals(object? obj) => obj is Position other && Equals(other);
    public override int GetHashCode()
    {
        HashCode hash = new();
        for (int i = 0; i < 12; i++) hash.Add(pieceBBs[i]);
        for (int i = 0; i < 3; i++) hash.Add(blockers[i]);
        hash.Add(color); hash.Add(castlings); hash.Add(enpassant);
        hash.Add(fullMoveCount); hash.Add(halfMoveClock);
        return hash.ToHashCode();
    }

    public static bool operator ==(Position left, Position right) => left.Equals(right);
    public static bool operator !=(Position left, Position right) => !left.Equals(right);

    public Position()
    {
        color = Colors.White;
        castlings = Castlings.None;
        enpassant = Squares.Empty;
        fullMoveCount = 1;
        halfMoveClock = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SwitchColor() => color = Colors.Mirror(color);

    public Piece GetPieceAt(int square, int color)
    {
        if (!Squares.IsValid(square)) throw new ArgumentOutOfRangeException(nameof(square));
        if (!Colors.IsValid(color)) throw new ArgumentOutOfRangeException(nameof(color));

        int bbIndex = BBIndex(Pieces.Pawn, color);
        int bbLastIndex = BBIndex(Pieces.King, color);

        Debug.Assert(IsValidBBIndex(bbIndex));
        Debug.Assert(IsValidBBIndex(bbLastIndex));

        while (bbIndex <= bbLastIndex)
        {
            if (BitboardOps.GetBitAt(pieceBBs[bbIndex], square) != 0)
            {
                int piece = PieceFromBBIndex(bbIndex, color);
                return new Piece(piece, color);
            }

            ++bbIndex;
        }

        return Piece.Empty;
    }

    internal void SetPieceAt(int square, int piece, int color)
    {
        if (!Squares.IsValid(square)) throw new ArgumentOutOfRangeException(nameof(square));
        DebugHelper.Assert.IsValidPiece(piece);
        if (!Colors.IsValid(color)) throw new ArgumentOutOfRangeException(nameof(color));

        int bbIndex = BBIndex(piece, color);
        Debug.Assert(IsValidBBIndex(bbIndex));

        BitboardOps.SetBitAt(ref pieceBBs[bbIndex], square);
        BitboardOps.SetBitAt(ref blockers[color], square);
        BitboardOps.SetBitAt(ref blockers[BitboardIndicies.AllBlockers], square);
    }

    internal void PopPieceAt(int square, int piece, int color)
    {
        if (!Squares.IsValid(square)) throw new ArgumentOutOfRangeException(nameof(square));
        DebugHelper.Assert.IsValidPiece(piece);
        if (!Colors.IsValid(color)) throw new ArgumentOutOfRangeException(nameof(color));

        int bbIndex = BBIndex(piece, color);
        Debug.Assert(IsValidBBIndex(bbIndex));

        BitboardOps.PopBitAt(ref pieceBBs[bbIndex], square);
        BitboardOps.PopBitAt(ref blockers[color], square);
        BitboardOps.PopBitAt(ref blockers[BitboardIndicies.AllBlockers], square);
    }

    internal void PopPieceAt(int square, int color)
    {
        if (!Squares.IsValid(square)) throw new ArgumentOutOfRangeException(nameof(square));
        if (!Colors.IsValid(color)) throw new ArgumentOutOfRangeException(nameof(color));

        int i = BBIndex(Pieces.Pawn, color);

        BitboardOps.PopBitAt(ref pieceBBs[i], square);
        BitboardOps.PopBitAt(ref pieceBBs[i + 1], square);
        BitboardOps.PopBitAt(ref pieceBBs[i + 2], square);
        BitboardOps.PopBitAt(ref pieceBBs[i + 3], square);
        BitboardOps.PopBitAt(ref pieceBBs[i + 4], square);
        BitboardOps.PopBitAt(ref pieceBBs[i + 5], square);

        BitboardOps.PopBitAt(ref blockers[color], square);
        BitboardOps.PopBitAt(ref blockers[BitboardIndicies.AllBlockers], square);
    }

    internal void MovePiece(int srcSquare, int trgSquare, int color, int piece)
    {
        PopPieceAt(srcSquare, piece, color);
        SetPieceAt(trgSquare, piece, color);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int BBIndex(int piece, int color)
    {
        DebugHelper.Assert.IsValidPiece(piece);
        if (!Colors.IsValid(color)) throw new ArgumentOutOfRangeException(nameof(color));

        return (color * 6) + piece;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int PieceFromBBIndex(int bbIndex, int color)
    {
        Debug.Assert(IsValidBBIndex(bbIndex));
        if (!Colors.IsValid(color)) throw new ArgumentOutOfRangeException(nameof(color));

        return bbIndex - color * 6;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidBBIndex(int bbIndex)
    {
        return bbIndex >= 0 && bbIndex < 12;
    }

    public bool IsKingChecked()
    {
        if (!PositionValidation.IsValid(this)) throw new InvalidOperationException("Invalid standard chess position.");
        return IsKingChecked(color);
    }

    internal bool IsKingChecked(int kingColor)
    {
        if (kingColor == Colors.Black)
        {
            int ks = BitboardOps.Lsb(pieceBBs[BitboardIndicies.BKing]);

            return IsSquareAttackedByWhite(ks);
        }
        else
        {
            int ks = BitboardOps.Lsb(pieceBBs[BitboardIndicies.WKing]);

            return IsSquareAttackedByBlack(ks);
        }
    }

    // Should run a bit faster than universal version
    // that calculates Bitboard indicies.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsSquareAttackedByWhite(int square)
    {
        return IsAttackedByWhitePawn(square)
            || IsAttackedByWhiteKnight(square)
            || IsAttackedByWhiteBishop(square)
            || IsAttackedByWhiteRook(square)
            || IsAttackedByWhiteKing(square);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsSquareAttackedByBlack(int square)
    {
        return IsAttackedByBlackPawn(square)
            || IsAttackedByBlackKnight(square)
            || IsAttackedByBlackBishop(square)
            || IsAttackedByBlackRook(square)
            || IsAttackedByBlackKing(square);
    }

    #region Is Attacked By Pawn
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAttackedByWhitePawn(int square)
    {
        ulong enemy = pieceBBs[BitboardIndicies.WPawn];
        ulong mask = PawnAttacks.GetAttackMask(Colors.Black, square);
        return (enemy & mask).IsTrue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAttackedByBlackPawn(int square)
    {
        ulong enemy = pieceBBs[BitboardIndicies.BPawn];
        ulong mask = PawnAttacks.GetAttackMask(Colors.White, square);
        return (enemy & mask).IsTrue();
    }
    #endregion

    #region Is Attacked By Knight
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAttackedByWhiteKnight(int square)
    {
        ulong enemy = pieceBBs[BitboardIndicies.WKnight];
        ulong mask = KnightAttacks.AttackMasks[square];

        return (enemy & mask).IsTrue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAttackedByBlackKnight(int square)
    {
        ulong enemy = pieceBBs[BitboardIndicies.BKnight];
        ulong mask = KnightAttacks.AttackMasks[square];

        return (enemy & mask).IsTrue();
    }
    #endregion

    #region Is Attacked By Bishop
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAttackedByWhiteBishop(int square)
    {
        ulong enemy = pieceBBs[BitboardIndicies.WBishop] | pieceBBs[BitboardIndicies.WQueen];
        ulong mask = BishopAttacks.GetSliderAttack(square, blockers[BitboardIndicies.AllBlockers]);

        return (mask & enemy).IsTrue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAttackedByBlackBishop(int square)
    {
        ulong enemy = pieceBBs[BitboardIndicies.BBishop] | pieceBBs[BitboardIndicies.BQueen];
        ulong mask = BishopAttacks.GetSliderAttack(square, blockers[BitboardIndicies.AllBlockers]);

        return (mask & enemy).IsTrue();
    }
    #endregion

    #region Is Attacked By Rook
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAttackedByWhiteRook(int square)
    {
        ulong enemy = pieceBBs[BitboardIndicies.WRook] | pieceBBs[BitboardIndicies.WQueen];
        ulong mask = RookAttacks.GetSliderAttack(square, blockers[BitboardIndicies.AllBlockers]);

        return (mask & enemy).IsTrue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAttackedByBlackRook(int square)
    {
        ulong enemy = pieceBBs[BitboardIndicies.BRook] | pieceBBs[BitboardIndicies.BQueen];
        ulong mask = RookAttacks.GetSliderAttack(square, blockers[BitboardIndicies.AllBlockers]);

        return (mask & enemy).IsTrue();
    }
    #endregion

    #region Is Attacked By King
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAttackedByWhiteKing(int square)
    {
        ulong enemy = pieceBBs[BitboardIndicies.WKing];
        ulong mask = KingAttacks.AttackMasks[square];

        return (enemy & mask).IsTrue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAttackedByBlackKing(int square)
    {
        ulong enemy = pieceBBs[BitboardIndicies.BKing];
        ulong mask = KingAttacks.AttackMasks[square];

        return (enemy & mask).IsTrue();
    }
    #endregion

    // Position assignment copies the inline bitboards; BigInteger is immutable.

    /// <summary>
    /// Creates default position filled with setuped pieces at its default positions.
    /// </summary>
    /// <returns> Default Position. </returns>
    public static Position CreateDefault()
    {
        const ulong bPawns      = SquareMapping.RANK_8 >> 8;
        const ulong bKnights    = (1ul << Squares.b8) | (1ul << Squares.g8);
        const ulong bBishops    = (1ul << Squares.c8) | (1ul << Squares.f8);
        const ulong bRooks      = (1ul << Squares.a8) | (1ul << Squares.h8);
        const ulong bQueen      = 1ul << Squares.d8;
        const ulong bKing       = 1ul << Squares.e8;
        const ulong bAll        = SquareMapping.RANK_8 | bPawns;

        const ulong wPawns      = SquareMapping.RANK_1 << 8;
        const ulong wKnights    = (1ul << Squares.b1) | (1ul << Squares.g1);
        const ulong wBishops    = (1ul << Squares.c1) | (1ul << Squares.f1);
        const ulong wRooks      = (1ul << Squares.a1) | (1ul << Squares.h1);
        const ulong wQueen      = 1ul << Squares.d1;
        const ulong wKing       = 1ul << Squares.e1;
        const ulong wAll        = SquareMapping.RANK_1 | wPawns;

        Position position = new();

        position.pieceBBs[BitboardIndicies.BPawn]   = bPawns;
        position.pieceBBs[BitboardIndicies.BKnight] = bKnights;
        position.pieceBBs[BitboardIndicies.BBishop] = bBishops;
        position.pieceBBs[BitboardIndicies.BRook]   = bRooks;
        position.pieceBBs[BitboardIndicies.BQueen]  = bQueen;
        position.pieceBBs[BitboardIndicies.BKing]   = bKing;

        position.pieceBBs[BitboardIndicies.WPawn]   = wPawns;
        position.pieceBBs[BitboardIndicies.WKnight] = wKnights;
        position.pieceBBs[BitboardIndicies.WBishop] = wBishops;
        position.pieceBBs[BitboardIndicies.WRook]   = wRooks;
        position.pieceBBs[BitboardIndicies.WQueen]  = wQueen;
        position.pieceBBs[BitboardIndicies.WKing]   = wKing;

        position.blockers[BitboardIndicies.BBlockers]   = bAll;
        position.blockers[BitboardIndicies.WBlockers]   = wAll;
        position.blockers[BitboardIndicies.AllBlockers] = bAll | wAll;

        position.color          = Colors.White;
        position.castlings      = Castlings.All;
        position.enpassant      = Squares.Empty;
        position.fullMoveCount  = 1;
        position.halfMoveClock  = 0;

        return position;
    }
}
[InlineArray(12)]
internal struct PieceBoards { private ulong first; }

[InlineArray(3)]
internal struct OccupancyBoards { private ulong first; }
