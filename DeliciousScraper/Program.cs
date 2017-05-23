using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace DeliciousScraper
{
    class Program
    {
        private const string Template = "<!DOCTYPE NETSCAPE-Bookmark-file-1>< META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\"><!-- This is an automatically generated file.It will be read and overwritten.Do Not Edit! --><TITLE>Bookmarks</TITLE><H1>Bookmarks</H1><DL><p>{0}</DL><p>";

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Scraper accepts only 1 argument, the username to scrape.");
                return;
            }
            string userName = args[0];
            WebClient webClient = new WebClient();
            var firstPage = webClient.DownloadString($"https://del.icio.us/{userName}");
            var pageLinks = Regex.Matches(firstPage, "/.*?\\?&amp;page=(?<page>\\d+)");
            var lastPage = int.Parse(pageLinks[pageLinks.Count-2].Groups["page"].Value); //TODO:DF better way to do this? Or at least slightly more defensive
            var bookmarks = new List<Bookmark>();
            bookmarks.AddRange(GetBookmarksFromHtml(firstPage));
            Console.WriteLine($"Fetched first of {lastPage} pages.");
            for (int x = 2; x <= lastPage; x++)
            {
                var data = DownloadString(webClient, $"https://del.icio.us/{userName}?page={x}");
                var newBookmarks = GetBookmarksFromHtml(data);
                bookmarks.AddRange(newBookmarks);
                Console.WriteLine($"Fetched page {x} with {newBookmarks.Count} bookmarks");
            }

            string content = string.Format(Template, string.Join(Environment.NewLine, bookmarks.Select(GenerateOutput)));
            File.WriteAllText("Bookmarks.html", content);
            Console.WriteLine("Fetched all bookmarks. Created Bookmarks.html Press any key to continue");
            Console.ReadLine();
        }

        private static string DownloadString(WebClient webClient, string url)
        {
            try
            {
                return webClient.DownloadString(url);
            }
            catch
            {
                Console.WriteLine("Request failed, sleeping and trying again");
                Thread.Sleep(1000);
            }
            return DownloadString(webClient, url);
        }

        private static string GenerateOutput(Bookmark bookmark)
        {
            return $"<DT><a href=\"{bookmark.Url}\" ADD_DATE=\"{bookmark.Date}\" PRIVATE=\"0\" TAGS=\"{string.Join(",", bookmark.Tags)}\">{bookmark.Title}</a>{Environment.NewLine}<DD>{bookmark.Description}{Environment.NewLine}";
        }

        private static List<Bookmark> GetBookmarksFromHtml(string data)
        {
            List<Bookmark> results = new List<Bookmark>();
            var bookmarks = data.Split(new[] {"<div class=\"articleThumbBlockOuter\""}, StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(c => c.Substring(0, c.IndexOf("<div class=\"sharePanel")));//Shave off the bottom, important for the last one on the page
            foreach (var bookmark in bookmarks)
            {
                var result = new Bookmark
                {
                    Title = Regex.Match(bookmark, "<h3><a\\ href=\"/url/.*?\"\\ class=\"title\"\\ title=\"(?<title>.*?)\">.*?</a></h3>").Groups["title"].Value,
                    Url = Regex.Match(bookmark, "<a href=\"(?<url>.*?)\"\\ target=\"_blank\"\\ rel=\"nofollow\">").Groups["url"].Value,
                    Date = Regex.Match(bookmark, "date=\"(?<date>\\d+)\">").Groups["date"].Value,
                    Description = Regex.Match(bookmark, "<div class=\"thumbTBriefTxt\">.+?<p><p>(?<desc>.*?)</p>", RegexOptions.Singleline).Groups["desc"]?.Value
                };
                foreach (Match tag in Regex.Matches(bookmark, "<li><a\\ href=\".*?\">(?<tag>.*?)</a></li>"))
                {
                    result.Tags.Add(tag.Groups["tag"].Value);
                }
                results.Add(result);
            }
            return results;
        }
    }
    
    public class Bookmark
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Date { get; set; }
        public List<string> Tags { get; set; }
        public string Description { get; set; }

        public Bookmark()
        {
            Tags = new List<string>();
        }
    }
}
