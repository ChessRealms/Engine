using ChessRealms.Engine.Core.Attacks;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Parsing;
using System.Diagnostics;

string fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
int depth = 6;

Console.WriteLine("Fen: {0}", fen);
Console.WriteLine("Depth: {0}", depth);
Console.WriteLine();

AttackLookups.InvokeInit();

_ = FenStrings.TryParse(fen, out Position pos);

Stopwatch stopwatch = Stopwatch.StartNew();

var nodes = Perft.PerftDriver.Test(pos, depth);

stopwatch.Stop();

Console.WriteLine();
Console.WriteLine();
Console.WriteLine("{0}", nodes);
Console.WriteLine();
Console.WriteLine("Seconds: {0}", stopwatch.Elapsed.TotalSeconds);
Console.WriteLine("Elapsed: {0}", stopwatch.Elapsed);
Console.WriteLine("ElapsedMilliseconds: {0}", stopwatch.ElapsedMilliseconds);
Console.WriteLine();
Console.WriteLine("Nodes/s: {0:n0}", nodes.Nodes / stopwatch.Elapsed.TotalSeconds);
Console.WriteLine();
