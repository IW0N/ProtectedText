using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;

namespace ProtectedText;

public class ProtectedTextClient : IDisposable
{
    public string Site { get; }
    string password;
    readonly HttpClient client;
    string initHashContent = "";
    string currentHashContent = "";
    const string base_path = "https://www.protectedtext.com";
    readonly string site_url;
    readonly static Regex hash_extractor;
    public readonly static string hash_tab_separator;
    int expected_dbVersion;
    int current_dbVersion;
    readonly string site_hash;
    string total_text;
    bool read_later = false;
    static ProtectedTextClient()
    {
        hash_tab_separator = GetHexSHA512("-- tab separator --");
        hash_extractor = new("(([0-9a-f]{2}){64})$");
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="site">site name (not path to site). Example mySite. Path-https://www.protectedtext.com/mySite</param>
    /// <param name="password">password for your site</param>
    public ProtectedTextClient(string site, string password)
    {
        Site = site;
        this.password = password;
        client = new();
        site_hash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes('/' + site))).ToLower();
        site_url = base_path + '/' + site;
        total_text = string.Empty;
    }
    /// <summary>
    /// Read the full content of the site
    /// </summary>
    /// <returns></returns>
    public async Task<string> ReadSite()
    {
        SiteData data = await GetSiteData();
        string encrypted = data.eContent;
        expected_dbVersion = data.expectedDBVersion;
        current_dbVersion = data.currentDBVersion;
        read_later = true;
        if (data.isNew)
            return "";

        string decrypted = CryptoSharpAes.Decrypt(encrypted, password);
        CheckSiteHash(decrypted);
        decrypted = hash_extractor.Replace(decrypted, "");
        initHashContent = ComputeHashContentForDBVersion(decrypted, password, current_dbVersion);
        total_text = decrypted;
        return decrypted;
    }
    /// <summary>
    /// Completely rewrite the content of the site
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public async Task WriteSite(string data)
    {
        if (!read_later)
            await ReadSite();
        string newHashContent = ComputeHashContentForDBVersion(data, password, expected_dbVersion);
        using HttpRequestMessage save_msg = new(HttpMethod.Post, site_url);
        string new_encrypted = CryptoSharpAes.Encrypt(data + site_hash, password);
        Dictionary<string, string> content = new()
        {
            { "initHashContent",initHashContent },
            { "currentHashContent",newHashContent },
            { "encryptedContent", new_encrypted },
            { "action", "save" }
        };
        save_msg.Content = new FormUrlEncodedContent(content);
        var response = client.Send(save_msg);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new OperationUnsuccessException();

        string dat_str = await response.Content.ReadAsStringAsync();

        ActionResponse dat = JsonSerializer.Deserialize<ActionResponse>(dat_str)!;
        if (dat.status == "success")
        {
            initHashContent = newHashContent;
        }
        else
            throw new OperationUnsuccessException();

    }
    public async Task DeleteSite()
    {
        if (string.IsNullOrEmpty(initHashContent))
            await ReadSite();
        using HttpRequestMessage msg = new(HttpMethod.Post, site_url);
        Dictionary<string, string> dict = new()
        {
            { "initHashContent", initHashContent },
            { "action", "delete" }
        };
        msg.Content = new FormUrlEncodedContent(dict);
        var response = await client.SendAsync(msg);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new Exception("Something went wrong!");
        string resp_str = await response.Content.ReadAsStringAsync();
        ActionResponse resp = JsonSerializer.Deserialize<ActionResponse>(resp_str);
        if (resp.status != "success")
            throw new OperationUnsuccessException();

    }
    /// <summary>
    /// Read tabs of site
    /// </summary>
    /// <returns>Array of tabs content</returns>
    public async Task<string[]> ReadTabs()
    {
        string text = await ReadSite();
        return string.IsNullOrEmpty(text) ? [] : text.Split(hash_tab_separator);
    }
    /// <summary>
    /// Set new tab and return its index
    /// </summary>
    /// <param name="tab_content"></param>
    /// <returns>Index of pushed tab</returns>
    public async Task<int> PushTab(string tab_content)
    {
        string[] tabs = await ReadTabs();
        if (tabs.Length > 0)
            total_text += hash_tab_separator;
        total_text += tab_content;

        await WriteSite(total_text);
        return tabs.Length;//new index
    }
    public async Task RemoveTab(int tab_index)
    {
        string[] tabs = await ReadTabs();
        tabs = tabs.Where((data, index) => index != tab_index).ToArray();
        UpdateTotalText(tabs);
        await WriteSite(total_text);
    }
    /// <summary>
    /// Rewrite tab content 
    /// </summary>
    /// <param name="new_data"></param>
    /// <param name="tab_index"></param>
    /// <returns></returns>
    public async Task EditTab(string new_data, int tab_index)
    {
        string[] tabs = await ReadTabs();
        tabs[tab_index] = new_data;
        UpdateTotalText(tabs);
        await WriteSite(total_text);
    }
    public async Task ChangePassword(string new_password)
    {
        string current_text = await ReadSite();
        password = new_password;
        await WriteSite(current_text);
    }
    async Task<SiteData> GetSiteData()
    {
        using var msg = new HttpRequestMessage(HttpMethod.Get, $"{site_url}?action=getJSON");
        using var response = await client.SendAsync(msg);
        SiteData payload=(await response.Content.ReadFromJsonAsync<SiteData>())!;
        return payload;
    }
    void CheckSiteHash(string decrypted)
    {
        var match = hash_extractor.Match(decrypted);
        string site_hash = match.Groups[1].Value;
        if (this.site_hash != site_hash)
            throw new IncorrectSiteHashException();
    }
    async Task<string> ReadMainPage()
    {
        using HttpRequestMessage msg = new(HttpMethod.Get, base_path + '/' + Site);
        using HttpResponseMessage response = await client.SendAsync(msg);
        string html = await response.Content.ReadAsStringAsync();
        html = html.Replace("\r", "").Replace("\n", "");
        return html;
    }
    void UpdateTotalText(string[] tabs)
    {
        total_text = "";
        int last_index = tabs.Length - 1;
        for (int i = 0; i <= last_index; i++)
        {
            total_text += tabs[i];
            if (i < last_index)
                total_text += hash_tab_separator;
        }
    }
    
    string ComputeHashContentForDBVersion(string content, string passphrase, int dbVersion) =>
        dbVersion switch
        {
            1 => GetHexSHA512(content),
            2 => GetHexSHA512(content + GetHexSHA512(passphrase)) + dbVersion,
            _ => throw new UnknowDbVersionException()
        };
    static string GetHexSHA512(string data)
    {
        byte[] bin_data = Encoding.UTF8.GetBytes(data);
        byte[] hash = SHA512.HashData(bin_data);
        string hash_str = Convert.ToHexString(hash).ToLower();
        return hash_str;
    }
    public void Dispose()
    {
        client.Dispose();
        GC.SuppressFinalize(this);
    }
}
