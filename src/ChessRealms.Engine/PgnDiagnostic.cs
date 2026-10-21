namespace ChessRealms.Engine;

/// <summary>The category of a PGN import failure.</summary>
public enum PgnErrorCode
{
    UnexpectedToken,
    InvalidTag,
    DuplicateTag,
    InvalidSetup,
    InvalidFen,
    InvalidResult,
    ResultMismatch,
    InvalidMoveNumber,
    InvalidSan,
    MoveAfterGameEnd,
    MissingResult,
    UnsupportedAnnotation,
    UnsupportedConstruct,
    UnsupportedVariant,
    MultipleGames
}

/// <summary>A PGN error location. Offset is zero-based UTF-16; line and column are one-based.
/// PlyIndex is the zero-based next mainline ply, or null for header errors.</summary>
public sealed record PgnDiagnostic(PgnErrorCode Code, int Offset, int Line, int Column, int? PlyIndex, string Message);

/// <summary>A failed PGN import with a structured diagnostic.</summary>
public sealed class PgnParseException : FormatException
{
    public PgnDiagnostic Diagnostic { get; }

    public PgnParseException(PgnDiagnostic diagnostic)
        : base(Describe(diagnostic))
    {
        Diagnostic = diagnostic;
    }

    private static string Describe(PgnDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return $"{diagnostic.Message} (line {diagnostic.Line}, column {diagnostic.Column}, offset {diagnostic.Offset}).";
    }
}
