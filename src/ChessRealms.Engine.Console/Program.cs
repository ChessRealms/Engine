using ChessRealms.Engine;

MoveResult lastMoveResult = MoveResult.None;
ChessGame chessGame = new();

Console.WriteLine("Moves: e2e4, a7a8q/r/b/n (promotion suffix required).");
Console.WriteLine("Commands: moves, fen, undo, claim3 [move], claim50 [move], quit.");
while (true)
{
    Console.WriteLine("Last move: {0}; side to move: {1}", lastMoveResult, chessGame.CurrentColor);
    PrintBoard(chessGame);
    Console.WriteLine("State: {0}; result: {1}; winner: {2}; reason: {3}",
        chessGame.State, chessGame.Outcome.Result, chessGame.Outcome.Winner, chessGame.Outcome.Reason);
    Console.WriteLine("Draw claims: {0}", chessGame.AvailableDrawClaims);
    Console.Write("> ");
    string? input = Console.ReadLine();
    if (input is null || input == "quit") break;
    if (input == "fen") { Console.WriteLine(chessGame.ToFen()); continue; }
    if (input == "moves") { Console.WriteLine(string.Join(" ", chessGame.GetLegalMoves())); continue; }
    if (input == "undo")
    {
        Console.WriteLine(chessGame.UndoMove() ? "Move undone." : "No move to undo.");
        lastMoveResult = MoveResult.None;
        continue;
    }
    string[] command = input.Split(' ');
    if (command[0] is "claim3" or "claim50")
    {
        DrawClaim reason = command[0] == "claim3" ? DrawClaim.ThreefoldRepetition : DrawClaim.FiftyMoveRule;
        bool claimed = command.Length == 1 ? chessGame.ClaimDraw(reason)
            : command.Length == 2 && CoordinateMove.TryParse(command[1], out var intended)
                && chessGame.ClaimDraw(reason, intended);
        Console.WriteLine(claimed ? "Draw claimed." : "Draw claim unavailable.");
        continue;
    }
    bool success = CoordinateMove.TryParse(input, out var move)
        && (lastMoveResult = chessGame.MakeMove(move)) != MoveResult.None;
    if (!success) Console.WriteLine("Invalid move or game already finished.");
}
static void PrintBoard(ChessGame chessGame)
{
    Span<ChessPiece> pieceSpan = stackalloc ChessPiece[64];
    chessGame.CopyBoardTo(pieceSpan);

    Console.WriteLine("   a b c d e f g h");

    for (int r = 7; r >= 0; --r)
    {
        Console.Write(" {0} ", r + 1);

        for (int f = 0; f < 8; ++f)
        {
            int square = r * 8 + f;

            if (pieceSpan[square].IsEmpty)
            {
                Console.Write('.');
            }
            else
            {
                Console.Write(PieceToString(ref pieceSpan[square]));
            }

            Console.Write(' ');
        }

        Console.WriteLine();
    }
}

static char PieceToString(ref ChessPiece piece)
{
    char p = piece.Value switch
    {
        PieceValue.Pawn => 'p',
        PieceValue.Knight => 'n',
        PieceValue.Bishop => 'b',
        PieceValue.Rook => 'r',
        PieceValue.Queen => 'q',
        PieceValue.King => 'k',
        _ => '\0'
    };

    return piece.Color == PieceColor.White ? char.ToUpper(p) : p;
}
