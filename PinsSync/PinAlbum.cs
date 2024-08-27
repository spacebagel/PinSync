using Newtonsoft.Json.Linq;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;

namespace PinsSync;
public class PinAlbum
{
    private static HttpClient httpClient = new HttpClient();

    public static async Task StartPageParser(string boardLink)
    {
        string boardId = string.Empty, bookmark = string.Empty;
        int i = 0;

        try
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless");

            using (IWebDriver driver = new ChromeDriver(options))
            {
                driver.Navigate().GoToUrl(boardLink);
                string jsonData = driver.FindElement(By.Id("__PWS_INITIAL_PROPS__")).GetAttribute("innerHTML");

                var jsonObject = JObject.Parse(jsonData);
                bookmark = FindAllNextBookmarks(jsonObject).LastOrDefault();
                File.WriteAllText("page.html", jsonObject["initialReduxState"].ToString());

                boardId = ExtractBoardId(jsonObject);
                var pins = ExtractPins(jsonObject);

                foreach (var pinUrl in pins)
                {
                    PrintAndSave(++i, pinUrl);
                }

                driver.Quit();
            }

            string albumURL = boardLink.Substring(boardLink.IndexOf('m') + 1);

            if (albumURL != "-")
            {
                await NextPageParser(boardId, bookmark, albumURL, i);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occurred: {ex.Message}");
        }
    }

    private static async Task NextPageParser(string boardId, string bookmark, string albumURL, int i)
    {
        try
        {
            DateTimeOffset dto = new DateTimeOffset(DateTime.UtcNow);
            string unixTimeMilliSeconds = dto.ToUnixTimeMilliseconds().ToString();
            string requestUrl = $"https://ru.pinterest.com/resource/BoardFeedResource/get/?source_url=" + albumURL
                + "&data=" + '{' + "\"options\":{\"add_vase\":true,\"board_id\":\""
                + boardId + "\",\"field_set_key\":\"react_grid_pin\",\"filter_section_pins\":false,\"is_react\":true,\"prepend\":false,\"page_size\":55,\"bookmarks\":[\""
                + bookmark + "\"]},\"context\":{}}&_=" + $"{unixTimeMilliSeconds}";

            var response = await httpClient.GetStringAsync(requestUrl);
            var jsonObject = JObject.Parse(response);
            var feeds = jsonObject["resource_response"]?["data"] as JArray;

            if (feeds != null)
            {
                foreach (var feed in feeds)
                {
                    var origImage = feed["images"]?["orig"]?["url"]?.ToString();

                    if (!string.IsNullOrEmpty(origImage))
                    {
                        PrintAndSave(++i, origImage);
                    }
                }

                var nextBookmarkId = jsonObject["resource"]?["options"]?["bookmarks"]?.FirstOrDefault()?.ToString();

                if (!string.IsNullOrEmpty(nextBookmarkId) && nextBookmarkId != "-end-")
                {
                    await NextPageParser(boardId, nextBookmarkId, albumURL, i);
                }
            }
            else
            {
                Console.WriteLine("No data found in resource_response.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occurred during pagination: {ex.Message}");
        }
    }

    private static List<string> FindAllNextBookmarks(JToken token)
    {
        var bookmarks = new List<string>();

        if (token.Type == JTokenType.Object)
        {
            foreach (var property in token.Children<JProperty>())
            {
                if (property.Name == "nextBookmark")
                {
                    bookmarks.Add(property.Value.ToString());
                }
                else
                {
                    bookmarks.AddRange(FindAllNextBookmarks(property.Value));
                }
            }
        }
        else if (token.Type == JTokenType.Array)
        {
            foreach (var item in token.Children())
            {
                bookmarks.AddRange(FindAllNextBookmarks(item));
            }
        }
        return bookmarks;
    }

    private static string ExtractBoardId(JObject jsonObject)
    {
        var board = jsonObject["initialReduxState"]?["boards"] as JObject;
        return board?.Properties().FirstOrDefault()?.Name;
    }

    private static IEnumerable<string> ExtractPins(JObject jsonObject)
    {
        var feed = (jsonObject["initialReduxState"]?["feeds"] as JObject)?.Properties().FirstOrDefault()?.Value as JArray;
        var pins = new List<string>();

        if (feed != null)
        {
            foreach (var pin in feed)
            {
                string pinId = pin["id"]?.ToString();
                string pinUrl = jsonObject["initialReduxState"]?["pins"]?[pinId]?["images"]?["orig"]?["url"]?.ToString();
                if (!string.IsNullOrEmpty(pinUrl))
                {
                    pins.Add(pinUrl);
                }
            }
        }
        return pins;
    }

    private static void PrintAndSave(int i, string url)
    {
        Console.WriteLine($"{i} Pin URL: {url}");
        File.AppendAllText("pinLinks.txt", $"{url}\n");
    }

    public static async Task SaveToFolder()
    {
        string folderPath = "pins";

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var links = File.ReadAllLines("pinLinks.txt");
        foreach (var link in links)
        {
            if (!string.IsNullOrWhiteSpace(link))
            {
                try
                {
                    var imageData = await httpClient.GetByteArrayAsync(link);
                    var pinName = link.Substring(link.LastIndexOf('/') + 1);
                    string fileName = Path.Combine(folderPath, pinName);

                    await File.WriteAllBytesAsync(fileName, imageData);

                    Console.WriteLine($"Downloaded: {fileName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to download {link}: {ex.Message}");
                }
            }
        }
    }
}
