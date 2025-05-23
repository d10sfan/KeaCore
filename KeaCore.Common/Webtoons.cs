// <copyright file="Webtoons.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace KeaCore.Common
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

        public static async Task<List<List<(string, string)>>> GetChaptersAsync(List<string> urls, int maxPages = -1)
        {
            var chapters = new List<List<(string, string)>>();

            foreach (var url in urls)
            {
                var chapterLinks = await GetChapterAsync(url, maxPages);
                chapters.Add(chapterLinks);
            }

            return chapters;
        }

        public static async Task DownloadComicAsync(string savePath, string comicName, List<(string, string)> chapters, string saveAs, string startAtChapter, string endAtChapter)
        {
            string comicSavePath = Path.Combine(savePath, comicName);
            Directory.CreateDirectory(comicSavePath);
            int chapterIndex = 0;

            if (!int.TryParse(endAtChapter, out int endInt))
            {
                endInt = -1;
            }

            if (!int.TryParse(startAtChapter, out int startInt))
            {
                startInt = 1;
            }

            OnStatusUpdated($"DownloadComicAsync {comicName} startInt = {startInt} endInt = {endInt}");

            foreach (var (chapterUrl, chapterName) in chapters)
            {
                if ((chapterIndex + 1) < startInt)
                {
                    OnStatusUpdated($"Skipping {comicName} chapter {chapterIndex + 1} because of startInt {startInt}");
                }
                else if (endInt != -1 && (chapterIndex + 1) > endInt)
                {
                    OnStatusUpdated($"Skipping {comicName} chapter {chapterIndex + 1} because of endInt {endInt}");
                }
                else
                {
                    OnStatusUpdated($"Downloading {comicName} chapter {chapterIndex + 1}");
                    await DownloadChapterAsync(comicSavePath, comicName, chapterUrl, chapterName, saveAs, chapterIndex);
                }

                chapterIndex++;
            }
        }

        private static void OnStatusUpdated(string status)
        {
            StatusUpdated?.Invoke(status);
        }

        private static async Task<List<(string, string)>> GetChapterAsync(string url, int maxPages)
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

                if(maxPages != -1 && page > (maxPages + 1)) {
                    break;
                }

                OnStatusUpdated($"Getting page - {pageUrl}");

                await Task.Delay(TimeSpan.FromSeconds(5));

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

            var uri = new Uri(chapterUrl);
            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var episodeNumber = queryParams["episode_no"];

            string chapterPath = Path.Combine(savePath, $"({episodeNumber}) {safeChapterName}");

            string chapterFilePath = string.Empty;
            if (saveAs == "PDF")
            {
                chapterFilePath = Path.Combine(savePath, $"({episodeNumber}) {safeChapterName}.pdf");
            }
            else if (saveAs == "CBZ")
            {
                chapterFilePath = Path.Combine(savePath, $"({episodeNumber}) {safeChapterName}.cbz");
            }

            if (System.IO.File.Exists(chapterFilePath))
            {
                OnStatusUpdated($"Skipping chapter {episodeNumber} of {comicName}, already downloaded.");
                return;
            }

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
                OnStatusUpdated($"Downloading image {imageIndex} of chapter {episodeNumber} of {comicName}");

                string imagePath = Path.Combine(chapterPath, $"{comicName}_Ch{episodeNumber}_Img{imageIndex}.jpg");

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
                SaveChapterAsPdf(chapterPath, savePath, safeChapterName, chapterIndex, chapterFilePath);
            }
            else if (saveAs == "CBZ")
            {
                ZipFile.CreateFromDirectory(chapterPath, chapterFilePath);
            }

            Directory.Delete(chapterPath, true);
        }

        private static void SaveChapterAsPdf(string chapterPath, string savePath, string chapterName, int chapterIndex, string pdfPath)
        {
            var imageFiles = Directory.GetFiles(chapterPath, "*.jpg").OrderBy(f => f).ToArray();
            Document doc = new Document();

            try
            {
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

        public class QueueItem
        {
            public string OriginalUrl { get; set; } = string.Empty;

            public string Name { get; set; } = string.Empty;

            public string StartAtChapter { get; set; } = "1";

            public string EndAtChapter { get; set; } = "end";
        }
    }
}
