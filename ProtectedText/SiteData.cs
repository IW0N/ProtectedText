using System.Text.Json.Serialization;
namespace ProtectedText;
class SiteData
{
    [JsonInclude]
    public string eContent;
    [JsonInclude]
    public bool isNew;
    [JsonInclude]
    public int currentDBVersion;
    [JsonInclude]
    public int expectedDBVersion;
}
