using System.Runtime.CompilerServices;

namespace ChessRealms.Engine.Core.Constants;

public static class Colors
{
    public const int Black = 0;
    public const int White = 1;
    public const int None = 2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Mirror(int color)
    {
        return color ^ White;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(int color)
    {
        return color == Black || color == White;
    }
}

public static class Pieces
{
    public const int Pawn = 0;
    public const int Knight = 1;
    public const int Bishop = 2;
    public const int Rook = 3;
    public const int Queen = 4;
    public const int King = 5;
    public const int None = 6;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(int piece)
    {
        return piece >= Pawn && piece < None;
    }
}

public static class Promotions
{
    public const int None = 0;
    public const int Knight = 1;
    public const int Bishop = 2;
    public const int Rook = 3;
    public const int Queen = 4;

    public static bool IsValid(int promotion)
    {
        return promotion == Knight
            || promotion == Bishop
            || promotion == Rook
            || promotion == Queen;
    }
}

public static class Castlings
{
    public const int None = 0;
    public const int WK = 1;
    public const int WQ = 2;
    public const int BK = 4;
    public const int BQ = 8;

    public const int White = WK | WQ;
    public const int Black = BK | BQ;

    public const int All = White | Black;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(int castlings)
    {
        return (All & castlings) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidSingle(int castling)
    {
        return WK == castling 
            || WQ == castling
            || BK == castling
            || BQ == castling;
    }
}

public static class Directions
{
    public const int North = 8;
    public const int South = -8;
    public const int West = 1;
    public const int East = -1;

    public const int NorthEast = North + East;
    public const int NorthWest = North + West;

    public const int SouthEast = South + East;
    public const int SouthWest = South + West;
}

public static class BitboardIndicies
{
    public const int BPawn = 0;
    public const int BKnight = 1;
    public const int BBishop = 2;
    public const int BRook = 3;
    public const int BQueen = 4;
    public const int BKing = 5;

    public const int WPawn = 6;
    public const int WKnight = 7;
    public const int WBishop = 8;
    public const int WRook = 9;
    public const int WQueen = 10;
    public const int WKing = 11;

    public const int BBlockers = 0;
    public const int WBlockers = 1;
    public const int AllBlockers = 2;
}

public static class Squares
{
    public const int Empty = -1;

    public const int a1 = 0;
    public const int b1 = 1;
    public const int c1 = 2;
    public const int d1 = 3;
    public const int e1 = 4;
    public const int f1 = 5;
    public const int g1 = 6;
    public const int h1 = 7;

    public const int a2 = 8;
    public const int b2 = 9;
    public const int c2 = 10;
    public const int d2 = 11;
    public const int e2 = 12;
    public const int f2 = 13;
    public const int g2 = 14;
    public const int h2 = 15;

    public const int a3 = 16;
    public const int b3 = 17;
    public const int c3 = 18;
    public const int d3 = 19;
    public const int e3 = 20;
    public const int f3 = 21;
    public const int g3 = 22;
    public const int h3 = 23;

    public const int a4 = 24;
    public const int b4 = 25;
    public const int c4 = 26;
    public const int d4 = 27;
    public const int e4 = 28;
    public const int f4 = 29;
    public const int g4 = 30;
    public const int h4 = 31;

    public const int a5 = 32;
    public const int b5 = 33;
    public const int c5 = 34;
    public const int d5 = 35;
    public const int e5 = 36;
    public const int f5 = 37;
    public const int g5 = 38;
    public const int h5 = 39;

    public const int a6 = 40;
    public const int b6 = 41;
    public const int c6 = 42;
    public const int d6 = 43;
    public const int e6 = 44;
    public const int f6 = 45;
    public const int g6 = 46;
    public const int h6 = 47;

    public const int a7 = 48;
    public const int b7 = 49;
    public const int c7 = 50;
    public const int d7 = 51;
    public const int e7 = 52;
    public const int f7 = 53;
    public const int g7 = 54;
    public const int h7 = 55;

    public const int a8 = 56;
    public const int b8 = 57;
    public const int c8 = 58;
    public const int d8 = 59;
    public const int e8 = 60;
    public const int f8 = 61;
    public const int g8 = 62;
    public const int h8 = 63;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(int square)
    {
        return square >= a1 && square <= h8;
    }
}
