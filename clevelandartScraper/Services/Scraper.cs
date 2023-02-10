using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using CG.Web.MegaApiClient;
using clevelandartScraper.Extensions;
using clevelandartScraper.Models;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace clevelandartScraper.Services
{
    public class Scraper
    {
        private HttpClient _client = new(new HttpClientHandler()
        {
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private async Task DownloadMain()
        {
            _mega.Login("jord914@gmail.com", "Riadh@@@");
            for (int i = 0; i < 10; i++)
            {
                if (!File.Exists($"items{i}"))
                    break;
                await DownloadImages(0);
            }

            await _mega.LogoutAsync();
        }

        public async Task Start()
        {
            //await GetUrls("https://www.clevelandart.org/art/collection/paginate?i=3&only-open-access=1&filter-type=Painting&filter-rights=open-access");
          //  await GetUrls("https://www.clevelandart.org/art/collection/paginate?i=4&only-open-access=1&filter-type=Drawing&filter-rights=open-access");
           // await GetUrls("https://www.clevelandart.org/art/collection/paginate?i=5&only-open-access=1&filter-type=Photograph&filter-rights=open-access");
          //  await GetUrls("https://www.clevelandart.org/art/collection/paginate?i=4&only-open-access=1&filter-type=Print");

          await GetDetailsMain();

          // var items = JsonConvert.DeserializeObject<List<Item>>(await File.ReadAllTextAsync("items"));
          // // await items.SaveToExcel("output.xlsx");
          // //await DownloadImage(items.First());
          // for (var i = 0; i < items.Count; i++)
          // {
          //     var item = items[i];
          //     item.LocalImage = $"{i + 1}.tif";
          // }
          // await File.WriteAllTextAsync("items",JsonConvert.SerializeObject(items));
        }

        private readonly MegaApiClient _mega = new();
        private INode _myFolder;

        private async Task DownloadImages(int b)
        {
            // var items = JsonConvert.DeserializeObject<List<Item>>(await File.ReadAllTextAsync("items"));
            // for (int i = 0; i < 10; i++)
            // {
            //     var sub = items.Skip(100000*i).Take(100000).ToList();
            //     await File.WriteAllTextAsync($"items{i}",JsonConvert.SerializeObject(sub));
            // }
            // for (int i = 0; i < 10; i++)
            //     {
            //         Console.WriteLine(JsonConvert.DeserializeObject<List<Item>>(await File.ReadAllTextAsync($"items{i}")).Count);
            //     }

            // var all = JsonConvert.DeserializeObject<List<Item>>(await File.ReadAllTextAsync($"items{b}"));
            // Console.WriteLine($"all : {all.Count}");
            // Console.WriteLine($"all : {all.Count(x => !x.ImageDownloaded)}");
            // return;


            //_mega.Login("supremespiders@gmail.com", "robin098hood");

            // var r =  await _mega.GetNodeFromLinkAsync(new Uri("https://mega.nz/folder/SVtDQYZJ#xxcP5gVac6daQUHFvir1SQ"));

            IEnumerable<INode> nodes = (await _mega.GetNodesAsync()).ToList();
            var root = nodes.Single(x => x.Type == NodeType.Root);
            _myFolder = nodes.FirstOrDefault(x => x.Name == $"img")
                        ?? await _mega.CreateFolderAsync($"img", root);
            // await File.WriteAllTextAsync("root",JsonConvert.SerializeObject(root));
            // Console.WriteLine("My folder : "+await _mega.GetDownloadLinkAsync(_myFolder));
            // var saved = nodes.Where(x => x.ParentId == _myFolder.Id);
            // var names = saved.Select(x => x.Name).ToList();
            // var downloadLink = await _mega.GetDownloadLinkAsync(myFile);
            // Console.WriteLine(downloadLink);
            var all = JsonConvert.DeserializeObject<List<Item>>(await File.ReadAllTextAsync($"items{b}"));
            // var uploaded = all.Where(x => names.Contains(x.LocalImage));
            // foreach (var u in uploaded)
            // {
            //     u.ImageDownloaded = true;
            // }

            var progress = new Progress<double>(Console.WriteLine);
            if (File.Exists($"img{b}.zip"))
            {
                await using (var fs = File.OpenRead($"img{b}.zip"))
                {
                    await _mega.UploadAsync(fs, $"img{b}.zip", _myFolder, progress);
                }

                //await _mega.UploadFileAsync($"img{b}.zip", _myFolder);

                File.Delete($"img{b}.zip");
            }

            
            do
            {
                Console.WriteLine($"Remaining : {all.Count(x => !x.ImageDownloaded)}");
                if (Directory.Exists("img"))
                    Directory.Delete("img", true);
                Directory.CreateDirectory("img");
                var items = all.Where(x => !x.ImageDownloaded).Take(10000).ToList();
                if (items.Count == 0)
                {
                    Console.WriteLine("all completed");
                    return;
                }

                await items.Parallel(DownloadImage, 10);
                Console.WriteLine("saving");
                await File.WriteAllTextAsync($"items{b}", JsonConvert.SerializeObject(all));
                ZipFile.CreateFromDirectory("img", $"img{b}.zip");
                Console.WriteLine("uploading");
                await using (var fs = File.OpenRead($"img{b}.zip"))
                {
                    await _mega.UploadAsync(fs, $"img{b}.zip", _myFolder, progress);
                }

                //await _mega.UploadFileAsync($"img{b}.zip", _myFolder);

                File.Delete($"img{b}.zip");
            } while (true);


            Console.WriteLine("completed downloading");

            // ZipFile.CreateFromDirectory("img", "img1.zip");
            // Console.WriteLine("completed compression");
            //Console.WriteLine("completed");
        }

        private async Task<Item> DownloadImage(Item item)
        {
            await _client.DownloadFile(item.Image, $"img/{item.LocalImage}");
            //File.Delete($"img/{item.LocalImage}");
            item.ImageDownloaded = true;
            return item;
        }

        private async Task GetUrlsThreaded()
        {
            var mainUrls = await File.ReadAllLinesAsync("mainUrls");
            var items = await mainUrls.Parallel(GetDetails, 10);
            await File.WriteAllTextAsync("items", JsonConvert.SerializeObject(items));
            Console.WriteLine("completed");
        }

        private async Task GetUrls(string ur)
        {
            var links = new List<string>();
            do
            {
                var json = await _client.GetHtml($"{ur}&limit=48&skip={links.Count}");

                var obj = JObject.Parse(json);
                var html = (string)obj.SelectToken("html_masonry");
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var nodes = doc.DocumentNode.SelectNodes("//div[@class='artwork-title']/a");
                Console.WriteLine(nodes?.Count);
                if (nodes == null || nodes.Count == 0) break;
                links.AddRange(nodes.Select(x=>x.GetAttributeValue("href","")));
            } while (true);

            if (File.Exists("urls"))
            {
                links.AddRange(await File.ReadAllLinesAsync("urls"));
            }
            await File.WriteAllLinesAsync("urls",links);
        }

        private Regex digitsOnly = new Regex(@"[^\d]");

        private async Task GetDetailsMain()
        {
            var urls = await File.ReadAllLinesAsync("urls");
            var items=await urls.Parallel(GetDetails, 20);
            await File.WriteAllTextAsync("items",JsonConvert.SerializeObject(items));
            Console.WriteLine("completed");
        }
        private async Task<Item> GetDetails(string url)
        {
            var doc = await _client.GetHtml($"https://www.clevelandart.org{url}").ToDoc();
            var panel = doc.DocumentNode.SelectSingleNode("//div[@typeof='sioc:Item foaf:Document']");
            var title = panel.SelectSingleNode(".//h1")?.InnerText;
            var date = panel.SelectSingleNode(".//p[@class='field field-name-field-date-text field-type-text field-label-hidden']")?.InnerText;
            var artist = panel.SelectSingleNode(".//span[@class='field field-name-field-artist-name']")?.InnerText;
            string nationality=null;
            string life=null;
            var origin = panel.SelectSingleNode(".//p[@class='field field-name-field-artist-origin']")?.InnerText.Replace("(","").Replace(")","");
            if (origin != null)
            {
                if (!origin.Contains(","))
                {
                    //throw new KnownException($"New format for origin in {url} : {origin}");
                    nationality = origin;
                }
                else
                {
                    var o = origin.Split(",");
                    nationality = o[0].Trim();
                    life = o[1].Trim();
                }
            }
            var typeOfArt = panel.SelectSingleNode(".//div[contains(text(),'Type of artwork:')]/../a")?.InnerText;
            var medium = panel.SelectSingleNode(".//div[@class='field field-name-field-medium']/a")?.InnerText;
            var img = doc.DocumentNode.SelectSingleNode("//li[@class='download-tiff']/a").GetAttributeValue("href","");
            return new Item()
            {
                Title = title,
                Date = date,
                Artist = artist,
                Medium = medium,
                Nationality = nationality,
                BornDeath = life,
                Url = url,
                TypeOfArtwork = typeOfArt,
                Image = img
            };
        }

        private async Task GetMainUrls()
        {
            var urls = new List<string>();
            var url = "https://www.christies.com/api/discoverywebsite/auctioncalendar/auctionresults?language=en&month=2&year=2023&component=e7d92272-7bcc-4dba-ae5b-28e4f3729ae8";
            do
            {
                Console.WriteLine(url);
                var json = await _client.GetHtml(url);
                var r = JsonConvert.DeserializeObject<AuctionResult>(json);
                //if(r.events.First())
                foreach (var e in r.events)
                {
                    //urls.Add(e.landing_url);
                    var eventId = e.event_id;
                    var saleId = e.analytics_id.Replace("Sale-", "");
                    urls.Add($"{eventId},{saleId}");
                }

                if (r.page_previous.title_txt.Contains("2008")) break;
                url = r?.page_previous?.url;
                if (string.IsNullOrEmpty(url)) break;
                url = $"https://www.christies.com/{url}";
            } while (true);

            await File.WriteAllLinesAsync("mainUrls", urls);
            Console.WriteLine("completed");
        }
    }
}