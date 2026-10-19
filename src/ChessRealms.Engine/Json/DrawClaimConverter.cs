using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChessRealms.Engine.Json;

internal sealed class DrawClaimConverter : JsonStringEnumConverter<DrawClaim>
{
    public DrawClaimConverter() : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    {
    }
}
