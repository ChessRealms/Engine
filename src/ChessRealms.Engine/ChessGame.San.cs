using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine;

public sealed partial class ChessGame
{
    /// <summary>Formats a playable move as canonical Standard Algebraic Notation in the current position.</summary>
    /// <remarks>Call before applying the move. Does not change the game.</remarks>
    /// <exception cref="ArgumentException">The move is invalid, illegal, or the game is finished.</exception>
    public string FormatSan(CoordinateMove move)
    {
        Span<char> text = stackalloc char[SanFormatter.MaxLength];
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
        int count = LegalMoves.Write(position, legalMoves);
        if (!LegalMoves.TryFind(move, legalMoves[..count], out int encodedMove))
        {
            return false;
        }

        Span<char> text = stackalloc char[SanFormatter.MaxLength];
        int length = SanFormatter.Write(position, encodedMove, legalMoves[..count], text);
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

        move = LegalMoves.ToCoordinateMove(encodedMove);
        return true;
    }

    /// <summary>Resolves and plays canonical SAN without repeating the current-position legal-move search.</summary>
    /// <remarks>Uses the same notation rules as ParseSan and the same history and outcome rules as MakeMove.
    /// Returns MoveResult.None without changing the game for invalid input or a finished game.</remarks>
    public MoveResult MakeSanMove(ReadOnlySpan<char> text)
        => TryFindSanMove(text, out int encodedMove)
            ? ApplyLegalMove(LegalMoves.ToCoordinateMove(encodedMove), encodedMove)
            : MoveResult.None;

    private bool TryFindSanMove(ReadOnlySpan<char> text, out int encodedMove)
    {
        encodedMove = 0;
        if (IsFinished || !TryReadSanPattern(text, out var pattern))
        {
            return false;
        }

        Span<int> legalMoves = stackalloc int[MoveGen.MaxMoves];
        int count = LegalMoves.Write(position, legalMoves);
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
        Span<char> canonical = stackalloc char[SanFormatter.MaxLength];
        int length = SanFormatter.Write(position, selected, legalMoves[..count], canonical);
        if (!text.SequenceEqual(canonical[..length]))
        {
            return false;
        }

        encodedMove = selected;
        return true;
    }

    private static bool TryReadSanPattern(ReadOnlySpan<char> text, out SanPattern pattern)
    {
        pattern = default;
        if (text.Length is < 2 or > SanFormatter.MaxLength)
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
