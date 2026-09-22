namespace ChessRealms.Engine.Common;

public static unsafe class UnsafeArrays
{
    public static HashSet<int> ToHashSet(int* ptr, int length)
    {
        HashSet<int> set = [];

        for (int i = 0; i < length; ++i)
        {
            set.Add(ptr[i]);
        }

        return set;
    }
}
