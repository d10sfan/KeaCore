// <copyright file="Webtoons.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace KeaCore
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using HtmlAgilityPack;
    using iTextSharp.text;
    using iTextSharp.text.pdf;

    public static class Webtoons
    {
        private static readonly HttpClient HttpClient = new ();

        public static event Action<string>? StatusUpdated;

        private static void OnStatusUpdated(string status)
        {
            StatusUpdated?.Invoke(status);
        }

        public class QueueItem
        {
            public string OriginalUrl { get; set; } = string.Empty;

            public string Name { get; set; } = string.Empty;

            public string StartAtChapter { get; set; } = "1";

            public string EndAtChapter { get; set; } = "end";
        }

        public static bool TryExtractNameFromUrl(string url, out string name)
        {
            name = string.Empty;

            // Validate URL structure
            if (!url.Contains("https://www.webtoons.com/") || !url.Contains("/list?title_no="))
            {
                return false;
            }

            if (url.Length - url.Replace("/", string.Empty).Length != 6)
            {
                return false;
            }

            try
            {
                int nameStart = 0;
                int nameEnd = 0;

                for (int i = 0; i < 6; i++)
                {
                    nameStart = nameEnd;
                    while (url[nameEnd] != '/')
                    {
                        nameEnd++;
                    }

                    nameEnd++;
                }

                name = url.Substring(nameStart, nameEnd - nameStart - 1);
                return !string.IsNullOrEmpty(name);
            }
            catch
            {
                return false;
            }
        }

        public static bool ValidateChapters(int start, string end)
        {
            if (start < 1)
            {
                return false;
            }

            if (int.TryParse(end, out int endInt))
            {
                if (endInt < 1 || endInt < start)
                {
                    return false;
                }
            }
            else if (end != "end")
            {
                return false;
            }

            return true;
        }

        public static async Task<List<List<(string, string)>>> GetChaptersAsync(List<string> urls)
        {
            var chapters = new List<List<(string, string)>>();

            foreach (var url in urls)
            {
                var chapterLinks = await GetChapterAsync(url);
                chapters.Add(chapterLinks);
            }

            return chapters;
        }

        private static async Task<List<(string, string)>> GetChapterAsync(string url)
        {
            var chapters = new List<(string, string)>();
            if (string.IsNullOrWhiteSpace(url))
            {
                return chapters;
            }

            int page = 1;
            string firstLink = string.Empty;
            bool foundEnd = false;

            while (true)
            {
                string pageUrl = $"{url}&page={page++}";
                OnStatusUpdated($"Getting page - {pageUrl}");

                string html = await HttpClient.GetStringAsync(pageUrl);
                var innerChapters = ParseChapterLinks(html, ref firstLink, ref foundEnd);

                if (innerChapters.Count == 0)
                {
                    break;
                }

                chapters.AddRange(innerChapters);
                if (foundEnd)
                {
                    break;
                }
            }

            chapters.Reverse();
            return chapters;
        }

        private static List<(string, string)> ParseChapterLinks(string html, ref string firstLink, ref bool foundEnd)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            var chapters = new List<(string, string)>();
            var div = doc.GetElementbyId("_listUl");

            if (div != null)
            {
                bool checkedForLink = false;
                foreach (var childNode in div.ChildNodes)
                {
                    if (childNode.HasChildNodes)
                    {
                        var chapterLink = childNode.ChildNodes[1].Attributes["href"]?.Value;
                        var chapterName = childNode.ChildNodes[1].ChildNodes[3]?.InnerText ?? string.Empty;

                        if (!checkedForLink && firstLink != chapterLink)
                        {
                            firstLink = chapterLink ?? string.Empty;
                            checkedForLink = true;
                        }
                        else if (!checkedForLink)
                        {
                            foundEnd = true;
                            break;
                        }

                        if (!string.IsNullOrEmpty(chapterLink) && !string.IsNullOrEmpty(chapterName))
                        {
                            chapters.Add((chapterLink, chapterName));
                        }
                    }
                }
            }

            return chapters;
        }

        public static async Task DownloadComicAsync(string savePath, string comicName, List<(string, string)> chapters, string saveAs)
        {
            Directory.CreateDirectory(savePath);
            int chapterIndex = 0;

            foreach (var (chapterUrl, chapterName) in chapters)
            {
                OnStatusUpdated($"Downloading {comicName} chapter {chapterIndex + 1}");
                await DownloadChapterAsync(savePath, comicName, chapterUrl, chapterName, saveAs, chapterIndex);
                chapterIndex++;
            }
        }

        private static string MakeFileNameSafe(string fileName)
        {
            // List of invalid characters for file names in common file systems
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();

            // Replace each invalid character with an underscore or another safe character
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            return fileName;
        }

        private static async Task DownloadChapterAsync(string savePath, string comicName, string chapterUrl, string chapterName, string saveAs, int chapterIndex)
        {
            string safeChapterName = MakeFileNameSafe(chapterName);
            string chapterPath = Path.Combine(savePath, $"({chapterIndex + 1}) {safeChapterName}");
            Directory.CreateDirectory(chapterPath);

            using var request = new HttpRequestMessage(HttpMethod.Get, chapterUrl);
            request.Headers.Add("Cookie", "pagGDPR=true;");

            using var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string html = await response.Content.ReadAsStringAsync();

            var imageUrls = ParseImageLinks(html);

            int imageIndex = 0;
            foreach (var imageUrl in imageUrls)
            {
                OnStatusUpdated($"Downloading image {imageIndex} of chapter {chapterIndex + 1} of {comicName}");

                string imagePath = Path.Combine(chapterPath, $"{comicName}_Ch{chapterIndex + 1}_Img{imageIndex}.jpg");

                using var imageRequest = new HttpRequestMessage(HttpMethod.Get, imageUrl);
                imageRequest.Headers.Add("Cookie", "pagGDPR=true;");
                imageRequest.Headers.Referrer = new Uri(chapterUrl);

                using var imageResponse = await HttpClient.SendAsync(imageRequest);
                imageResponse.EnsureSuccessStatusCode();

                await using var fileStream = new FileStream(imagePath, FileMode.Create);
                await imageResponse.Content.CopyToAsync(fileStream);

                imageIndex++;
            }

            if (saveAs == "PDF")
            {
                SaveChapterAsPdf(chapterPath, savePath, safeChapterName, chapterIndex);
            }
            else if (saveAs == "CBZ")
            {
                string cbzPath = Path.Combine(savePath, $"({chapterIndex + 1}) {safeChapterName}.cbz");
                ZipFile.CreateFromDirectory(chapterPath, cbzPath);
            }

            Directory.Delete(chapterPath, true);
        }

        private static void SaveChapterAsPdf(string chapterPath, string savePath, string chapterName, int chapterIndex)
        {
            var imageFiles = Directory.GetFiles(chapterPath, "*.jpg").OrderBy(f => f).ToArray();
            Document doc = new Document();

            try
            {
                string pdfPath = Path.Combine(savePath, $"({chapterIndex + 1}) {chapterName}.pdf");
                PdfWriter.GetInstance(doc, new FileStream(pdfPath, FileMode.Create));
                doc.Open();

                foreach (var imagePath in imageFiles)
                {
                    var img = Image.GetInstance(imagePath);
                    doc.SetPageSize(new Rectangle(img.Width, img.Height));
                    doc.NewPage();
                    doc.Add(img);
                }
            }
            finally
            {
                doc.Close();
            }
        }

        private static List<string> ParseImageLinks(string html)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            var imageUrls = new List<string>();
            var div = doc.GetElementbyId("_imageList");

            if (div != null)
            {
                foreach (var node in div.ChildNodes.Where(n => n.NodeType == HtmlNodeType.Element))
                {
                    var imageUrl = node.Attributes["data-url"]?.Value;
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        imageUrls.Add(imageUrl);
                    }
                }
            }

            return imageUrls;
        }
    }
}
