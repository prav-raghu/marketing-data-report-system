using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApiGateway.Middleware;

public static class SensitiveDataMasker
{
    private const string Redacted = "[REDACTED]";

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "currentPassword",
        "newPassword",
        "confirmPassword",
        "token",
        "accessToken",
        "refreshToken",
        "secret",
        "apiKey",
        "api_key",
        "clientSecret",
        "privateKey",
        "creditCard",
        "cardNumber",
        "cvv",
        "cvc",
        "ssn",
        "nationalId",
        "pin",
        "otp",
        "twoFactorCode",
        "authorization",
        "cookie",
    };

    public static JsonNode? Mask(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonArray array:
                var maskedArray = new JsonArray();
                foreach (var item in array)
                {
                    maskedArray.Add(Mask(item?.DeepClone()));
                }
                return maskedArray;
            case JsonObject obj:
                var maskedObject = new JsonObject();
                foreach (var (key, value) in obj)
                {
                    maskedObject[key] = SensitiveKeys.Contains(key)
                        ? JsonValue.Create(Redacted)
                        : Mask(value?.DeepClone());
                }
                return maskedObject;
            default:
                return node.DeepClone();
        }
    }

    public static JsonNode? TryParseAndMask(string json)
    {
        try
        {
            return Mask(JsonNode.Parse(json));
        }
        catch (JsonException)
        {
            return JsonValue.Create("[non-JSON payload]");
        }
    }
}
