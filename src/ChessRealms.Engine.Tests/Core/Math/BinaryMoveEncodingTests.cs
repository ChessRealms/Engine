using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;

namespace ChessRealms.Engine.Tests.Core.Math;

internal class BinaryMoveEncodingTests
{
    [Test]
    public void EncodeDecodeMoveTest()
    {
        int src = 63;
        int srcPiece = Pieces.King;
        int srcColor = Colors.White;

        int trg = 62;
        int trgPiece = Pieces.None;
        int trgColor = Colors.None;

        int capture = 1;
        int promotion = Promotions.Queen;
        int enpassant = 1;
        int doublePush = 1;
        int castling = Castlings.All;

        int move = BinaryMoveOps.EncodeMove(
            src, srcPiece, srcColor,
            trg, trgPiece, trgColor,
            capture, promotion,
            enpassant, doublePush, castling);

        Assert.Multiple(() =>
        {
            Assert.That(BinaryMoveOps.DecodeSrc(move), Is.EqualTo(src));
            Assert.That(BinaryMoveOps.DecodeSrcPiece(move), Is.EqualTo(srcPiece));
            Assert.That(BinaryMoveOps.DecodeSrcColor(move), Is.EqualTo(srcColor));

            Assert.That(BinaryMoveOps.DecodeTrg(move), Is.EqualTo(trg));

            Assert.That(BinaryMoveOps.DecodeCapture(move), Is.EqualTo(capture));
            Assert.That(BinaryMoveOps.DecodeEnpassant(move), Is.EqualTo(enpassant));
            Assert.That(BinaryMoveOps.DecodeDoublePush(move), Is.EqualTo(doublePush));
            Assert.That(BinaryMoveOps.DecodeCastling(move), Is.EqualTo(castling));
            Assert.That(BinaryMoveOps.DecodePromotion(move), Is.EqualTo(promotion));
        });
    }
}
