using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Core.Types;

namespace ChessRealms.Engine;

public sealed partial class ChessGame
{
    // Canonical SAN is at most seven ASCII characters, for example Qa6xb7#.
    private const int MaxSanLength = 7;

    /// <summary>Formats a playable move as canonical Standard Algebraic Notation in the current position.</summary>
    /// <remarks>Call before applying the move. Does not change the game.</remarks>
    /// <exception cref="ArgumentException">The move is invalid, illegal, or the game is finished.</exception>
    public string FormatSan(CoordinateMove move)
    {
        Span<char> text = stackalloc char[MaxSanLength];
        return TryFormatSan(move, text, out int written)
            ? new string(text[..written])
            : throw new ArgumentException("The move must be playable in the current game.", nameof(move));
    }

    /// <summary>Writes canonical SAN for a playable move into a character span without allocating a string.</summary>
    /// <remarks>Uses the position before the move and does not change the game. Returns false with zero
    /// characters written and leaves the destination unchanged for an invalid or illegal move, a finished
    /// game, or insufficient space. Seven characters suffice for any canonical SAN move.</remarks>
    public bool TryFormatSan(CoordinateMove move, Span<char> destination, out int charsWritten)
    {
        charsWritten = 0;
        if (IsFinished || !move.IsValid || destination.Length < 2)
        {
            return false;
        }

        Span<int> legalMoves = stackalloc int[MoveGen.MaxMoves];
        int count = WriteLegalMoves(position, legalMoves);
        if (!TryFindMove(move, legalMoves[..count], out int encodedMove))
        {
            return false;
        }

        Span<char> text = stackalloc char[MaxSanLength];
        int length = WriteSan(position, encodedMove, legalMoves[..count], text);
        if (destination.Length < length)
        {
            return false;
        }

        text[..length].CopyTo(destination);
        charsWritten = length;
        return true;
    }

    /// <summary>Resolves canonical SAN to a legal coordinate move without changing the game.</summary>
    /// <remarks>Requires exact case, minimal disambiguation, and the correct check or mate suffix.
    /// Whitespace, annotations and alternative spellings are not accepted.</remarks>
    /// <exception cref="FormatException">The text is not canonical SAN for a playable move in this game.</exception>
    public CoordinateMove ParseSan(ReadOnlySpan<char> text)
        => TryParseSan(text, out var move) ? move : throw new FormatException("Expected canonical SAN for a playable move.");

    /// <summary>Tries to resolve canonical SAN to a legal coordinate move without changing the game.</summary>
    /// <remarks>Returns false and a default move for malformed, ambiguous, noncanonical or illegal input,
    /// or a finished game. The check or mate suffix must be exact. Does not allocate strings.</remarks>
    public bool TryParseSan(ReadOnlySpan<char> text, out CoordinateMove move)
    {
        move = default;
        if (!TryFindSanMove(text, out int encodedMove))
        {
            return false;
        }

        move = ToPublicMove(encodedMove);
        return true;
    }

    /// <summary>Resolves and plays canonical SAN without repeating the current-position legal-move search.</summary>
    /// <remarks>Uses the same notation rules as ParseSan and the same history and outcome rules as MakeMove.
    /// Returns MoveResult.None without changing the game for invalid input or a finished game.</remarks>
    public MoveResult MakeSanMove(ReadOnlySpan<char> text)
        => TryFindSanMove(text, out int encodedMove)
            ? ApplyLegalMove(ToPublicMove(encodedMove), encodedMove)
            : MoveResult.None;

    private bool TryFindSanMove(ReadOnlySpan<char> text, out int encodedMove)
    {
        encodedMove = 0;
        if (IsFinished || !TryReadSanPattern(text, out var pattern))
        {
            return false;
        }

        Span<int> legalMoves = stackalloc int[MoveGen.MaxMoves];
        int count = WriteLegalMoves(position, legalMoves);
        int selected = 0;
        foreach (int candidate in legalMoves[..count])
        {
            if (!pattern.Matches(candidate))
            {
                continue;
            }

            // A check suffix cannot disambiguate two otherwise matching moves.
            if (selected != 0)
            {
                return false;
            }

            selected = candidate;
        }

        if (selected == 0)
        {
            return false;
        }

        // Only the selected move needs check/mate evaluation. This also rejects
        // unnecessary source qualifiers and all noncanonical spellings.
        Span<char> canonical = stackalloc char[MaxSanLength];
        int length = WriteSan(position, selected, legalMoves[..count], canonical);
        if (!text.SequenceEqual(canonical[..length]))
        {
            return false;
        }

        encodedMove = selected;
        return true;
    }

    private static int WriteSan(Position before, int move, ReadOnlySpan<int> legalMoves, Span<char> text)
    {
        int length = 0;
        int castling = BinaryMoveOps.DecodeCastling(move);
        if (castling != Castlings.None)
        {
            ReadOnlySpan<char> castle = (castling & (Castlings.WK | Castlings.BK)) != 0 ? "O-O" : "O-O-O";
            castle.CopyTo(text);
            length = castle.Length;
        }
        else
        {
            int piece = BinaryMoveOps.DecodeSrcPiece(move);
            int source = BinaryMoveOps.DecodeSrc(move);
            int target = BinaryMoveOps.DecodeTrg(move);
            bool capture = BinaryMoveOps.DecodeCapture(move) != 0;
            if (piece != Pieces.Pawn)
            {
                text[length++] = "PNBRQK"[piece];
                bool alternative = false;
                bool sameFile = false;
                bool sameRank = false;
                foreach (int candidate in legalMoves)
                {
                    int otherSource = BinaryMoveOps.DecodeSrc(candidate);
                    if (otherSource == source || BinaryMoveOps.DecodeSrcPiece(candidate) != piece
                        || BinaryMoveOps.DecodeTrg(candidate) != target)
                    {
                        continue;
                    }

                    alternative = true;
                    sameFile |= otherSource % 8 == source % 8;
                    sameRank |= otherSource / 8 == source / 8;
                }

                if (alternative)
                {
                    if (!sameFile || sameRank)
                    {
                        text[length++] = (char)('a' + source % 8);
                    }

                    if (sameFile)
                    {
                        text[length++] = (char)('1' + source / 8);
                    }
                }
            }
            else if (capture)
            {
                text[length++] = (char)('a' + source % 8);
            }

            if (capture)
            {
                text[length++] = 'x';
            }

            text[length++] = (char)('a' + target % 8);
            text[length++] = (char)('1' + target / 8);
            int promotion = BinaryMoveOps.DecodePromotion(move);
            if (promotion != Promotions.None)
            {
                text[length++] = '=';
                text[length++] = "PNBRQK"[promotion];
            }
        }

        // SAN needs board geometry, not counters or repetition history. In
        // particular an automatic draw with check must not be mistaken for mate.
        var after = before;
        MoveDriver.MakeMove(ref after, move, updateCounters: false);
        after.SwitchColor();
        if (after.IsKingChecked(after.color))
        {
            Span<int> replies = stackalloc int[MoveGen.MaxMoves];
            text[length++] = WriteLegalMoves(after, replies) == 0 ? '#' : '+';
        }

        return length;
    }

    private static bool TryReadSanPattern(ReadOnlySpan<char> text, out SanPattern pattern)
    {
        pattern = default;
        if (text.Length is < 2 or > MaxSanLength)
        {
            return false;
        }

        if (text[^1] is '+' or '#')
        {
            text = text[..^1];
        }

        if (text.SequenceEqual("O-O") || text.SequenceEqual("O-O-O"))
        {
            int rights = text.Length == 3 ? Castlings.WK | Castlings.BK : Castlings.WQ | Castlings.BQ;
            pattern = new(rights, Pieces.King, 0, Promotions.None, false, -1, -1);
            return true;
        }

        int promotion = Promotions.None;
        if (text.Length >= 2 && text[^2] == '=')
        {
            promotion = ReadSanPiece(text[^1]);
            if (!Promotions.IsValid(promotion))
            {
                return false;
            }

            text = text[..^2];
        }

        if (text.Length < 2 || !Square.TryParse(text[^2..], out var target))
        {
            return false;
        }

        var prefix = text[..^2];
        int piece = prefix.IsEmpty ? Pieces.None : ReadSanPiece(prefix[0]);
        if (piece != Pieces.None)
        {
            prefix = prefix[1..];
        }
        else
        {
            piece = Pieces.Pawn;
        }

        bool capture = !prefix.IsEmpty && prefix[^1] == 'x';
        if (capture)
        {
            prefix = prefix[..^1];
        }

        int file = -1;
        int rank = -1;
        if (!prefix.IsEmpty && prefix[0] is >= 'a' and <= 'h')
        {
            file = prefix[0] - 'a';
            prefix = prefix[1..];
        }

        if (!prefix.IsEmpty && prefix[0] is >= '1' and <= '8')
        {
            rank = prefix[0] - '1';
            prefix = prefix[1..];
        }

        if (!prefix.IsEmpty || (piece == Pieces.Pawn && (rank != -1 || capture != (file != -1)))
            || (piece != Pieces.Pawn && promotion != Promotions.None))
        {
            return false;
        }

        pattern = new(Castlings.None, piece, target.Index, promotion, capture, file, rank);
        return true;
    }

    private static int ReadSanPiece(char symbol) => symbol switch
    {
        'N' => Pieces.Knight,
        'B' => Pieces.Bishop,
        'R' => Pieces.Rook,
        'Q' => Pieces.Queen,
        'K' => Pieces.King,
        _ => Pieces.None
    };

    private readonly record struct SanPattern(int Castling, int Piece, int Target, int Promotion,
        bool Capture, int File, int Rank)
    {
        public bool Matches(int move)
        {
            int castling = BinaryMoveOps.DecodeCastling(move);
            if (Castling != Castlings.None)
            {
                return (castling & Castling) != 0;
            }

            int source = BinaryMoveOps.DecodeSrc(move);
            return castling == Castlings.None && BinaryMoveOps.DecodeSrcPiece(move) == Piece
                && BinaryMoveOps.DecodeTrg(move) == Target && BinaryMoveOps.DecodePromotion(move) == Promotion
                && (BinaryMoveOps.DecodeCapture(move) != 0) == Capture
                && (File == -1 || source % 8 == File) && (Rank == -1 || source / 8 == Rank);
        }
    }
}
