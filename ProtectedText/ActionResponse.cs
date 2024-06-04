using System.Text.Json.Serialization;

namespace ProtectedText;
class ActionResponse
{
    [JsonInclude]
    public string status;
}