namespace clevelandartScraper.Models;

public class Item
{
    public string Title { get; set; }
    public string Date { get; set; }
    public string Artist { get; set; }
    public string BornDeath { get; set; }
    public string Nationality { get; set; }
    public string  TypeOfArtwork { get; set; }
    public string  Medium { get; set; }
    public string Image { get; set; }
    public string LocalImage { get; set; }
    public string Url { get; set; }
    public bool ImageDownloaded { get; set; }
}