using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KeaCore.Common;

const string PrefixTitle = "KEACORE_TITLE_NUM_";
const string PrefixGenre = "KEACORE_TITLE_GENRE_";
const string FolderPathVar = "KEACORE_FOLDER_PATH";

// Subscribe to Webtoons status updates
Webtoons.StatusUpdated += PrintStatusUpdate;

// Get the global folder path
string? folderPath = Environment.GetEnvironmentVariable(FolderPathVar);

// Ensure folder path is provided
if (string.IsNullOrWhiteSpace(folderPath))
{
    Console.WriteLine($"Error: The environment variable {FolderPathVar} is missing or empty.");
    return;
}

// Get all environment variables and filter for Webtoon title numbers & genres
var envVars = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>();

// Get title numbers
var titleEntries = envVars
.Where(entry => entry.Key is string key && key.StartsWith(PrefixTitle))
.Select(entry =>
{
    string key = entry.Key?.ToString() ?? string.Empty;
    string name = key.Length > PrefixTitle.Length ? key[PrefixTitle.Length..] : string.Empty; // Remove prefix
    string titleNum = entry.Value as string ?? string.Empty; // Ensure titleNum is a string
    return new KeaCoreEntry(name, titleNum, Genre: null); // Genre will be assigned later
})
.ToDictionary(entry => entry.Name); // Store in a dictionary for fast lookup

// Get genres and map them to the correct entry
foreach (var entry in envVars.Where(e => e.Key is string key && key.StartsWith(PrefixGenre)))
{
    string key = entry.Key?.ToString() ?? string.Empty;
    string name = key.Length > PrefixGenre.Length ? key[PrefixGenre.Length..] : string.Empty; // Remove prefix
    string genre = entry.Value as string ?? "canvas"; // Default to "canvas" if missing

    if (titleEntries.ContainsKey(name))
    {
        titleEntries[name] = titleEntries[name] with { Genre = genre }; // Assign Genre
    }
}

// Convert back to array
var processedEntries = titleEntries.Values.ToArray();

// Exit if no valid entries were found
if (processedEntries.Length == 0)
{
    Console.WriteLine("No valid KEACORE_TITLE_NUM_ environment variables found.");
    return;
}

Console.WriteLine("Processing Webtoons...");

for (int i = 0; i < processedEntries.Length; i++)
{
    var entry = processedEntries[i];
    Console.WriteLine($"Processing: {entry.Name}");
    Console.WriteLine($"Genre: {entry.Genre}");
    Console.WriteLine($"Saving to: {folderPath}");

    // Ensure TitleNum is available
    if (string.IsNullOrWhiteSpace(entry.TitleNum))
    {
        Console.WriteLine($"Skipping {entry.Name}: No title number found.");
        continue;
    }

    // Construct Webtoon URL using Genre and TitleNum
    string webtoonUrl = $"https://www.webtoons.com/en/{entry.Genre}/{entry.Name}/list?title_no={entry.TitleNum}";

    // ✅ Validate the name by extracting it from the URL
    if (!Webtoons.TryExtractNameFromUrl(webtoonUrl, out string extractedName))
    {
        Console.WriteLine($"Invalid Webtoon URL: {webtoonUrl}");
        continue;
    }

    // ✅ Fetch chapters
    Console.WriteLine("Fetching chapters...");
    var chapters = await Webtoons.GetChaptersAsync(new List<string> { webtoonUrl }, 4);

    // ✅ Ensure chapters exist
    if (chapters == null || chapters.Count == 0)
    {
        Console.WriteLine($"No chapters found for {entry.Name}");
        continue;
    }

    // ✅ Download each Webtoon chapter
    Console.WriteLine($"Downloading {chapters.Count} chapters...");
    await Webtoons.DownloadComicAsync(
        folderPath,
        extractedName,
        chapters.First(),
                                      "CBZ",
                                      "1",
                                      "end"
    );

    Console.WriteLine($"Download complete for: {entry.Name}\n");

    // ✅ Sleep for 2 minutes **only if there's more than one entry** and **not the last entry**
    if (processedEntries.Length > 1 && i < processedEntries.Length - 1)
    {
        Console.WriteLine("Waiting for 2 minutes before processing the next entry...\n");
        await Task.Delay(TimeSpan.FromMinutes(2));
    }
}

Console.WriteLine("All Webtoon downloads completed.");

// ✅ Unsubscribe from Webtoons.StatusUpdated to prevent memory leaks
Webtoons.StatusUpdated -= PrintStatusUpdate;

// ✅ Status update handler
void PrintStatusUpdate(string status)
{
    Console.WriteLine($"[Status] {status}");
}

// ✅ Updated record with Genre field
record KeaCoreEntry(string Name, string TitleNum, string? Genre);
