using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;

namespace ChessRealms.Engine.Core.Types;

/// <summary>Exact position identity for repetition, excluding move counters.</summary>
internal readonly struct RepetitionKey : IEquatable<RepetitionKey>
{
    // Three bitplanes encode empty=0, pawn=1, knight=2, bishop=3, rook=4,
    // queen=5, king=6 on all 64 squares; the fourth records black occupancy.
    // This is a lossless board encoding, not a probabilistic board hash.
    private readonly ulong typeLow;
    private readonly ulong typeMiddle;
    private readonly ulong typeHigh;
    private readonly ulong blackPieces;
    private readonly int state;

    private RepetitionKey(in Position position, int enPassant)
    {
        ulong pawns = position.pieceBBs[BitboardIndicies.BPawn] | position.pieceBBs[BitboardIndicies.WPawn];
        ulong knights = position.pieceBBs[BitboardIndicies.BKnight] | position.pieceBBs[BitboardIndicies.WKnight];
        ulong bishops = position.pieceBBs[BitboardIndicies.BBishop] | position.pieceBBs[BitboardIndicies.WBishop];
        ulong rooks = position.pieceBBs[BitboardIndicies.BRook] | position.pieceBBs[BitboardIndicies.WRook];
        ulong queens = position.pieceBBs[BitboardIndicies.BQueen] | position.pieceBBs[BitboardIndicies.WQueen];
        ulong kings = position.pieceBBs[BitboardIndicies.BKing] | position.pieceBBs[BitboardIndicies.WKing];
        typeLow = pawns | bishops | queens;
        typeMiddle = knights | bishops | kings;
        typeHigh = rooks | queens | kings;
        blackPieces = position.pieceBBs[BitboardIndicies.BPawn] | position.pieceBBs[BitboardIndicies.BKnight]
            | position.pieceBBs[BitboardIndicies.BBishop] | position.pieceBBs[BitboardIndicies.BRook]
            | position.pieceBBs[BitboardIndicies.BQueen] | position.pieceBBs[BitboardIndicies.BKing];

        // One side bit, four castling bits, then en passant (zero means absent).
        state = position.color | (position.castlings << 1) | ((enPassant + 1) << 5);
    }

    // The caller supplies the legal moves it already generated. A pinned pawn
    // does not retain an en passant right when that capture would expose its king.
    internal static RepetitionKey Create(in Position position, ReadOnlySpan<int> legalMoves)
    {
        int enPassant = Squares.Empty;
        if (position.enpassant != Squares.Empty)
        {
            foreach (int move in legalMoves)
            {
                if (BinaryMoveOps.DecodeEnpassant(move) != 0)
                {
                    enPassant = position.enpassant;
                    break;
                }
            }
        }

        return new RepetitionKey(position, enPassant);
    }

    public bool Equals(RepetitionKey other) => typeLow == other.typeLow
        && typeMiddle == other.typeMiddle && typeHigh == other.typeHigh
        && blackPieces == other.blackPieces && state == other.state;

    public override bool Equals(object? obj) => obj is RepetitionKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(typeLow, typeMiddle, typeHigh, blackPieces, state);

    public static bool operator ==(RepetitionKey left, RepetitionKey right) => left.Equals(right);

    public static bool operator !=(RepetitionKey left, RepetitionKey right) => !left.Equals(right);
}
