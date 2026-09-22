namespace ChessRealms.Engine;

public readonly struct Square(int value)
{
    public readonly int Value = value;

    public static implicit operator Square(int value) => new(value);
    public static implicit operator int(Square sqaure) => sqaure.Value;
}
