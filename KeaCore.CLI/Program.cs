using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KeaCore.Common;

const string PrefixEntry = "KEACORE_ENTRY_";
const string PrefixTitle = "KEACORE_TITLE_NUM_";

// Subscribe to Webtoons status updates
Webtoons.StatusUpdated += PrintStatusUpdate;

// Get all environment variables and filter for Webtoon entries
var envVars = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>();

// Get entries (folder paths)
var entries = envVars
.Where(entry => entry.Key is string key && key.StartsWith(PrefixEntry))
.Select(entry =>
{
    string key = entry.Key?.ToString() ?? string.Empty;
    string name = key.Length > PrefixEntry.Length ? key[PrefixEntry.Length..] : string.Empty; // Remove prefix
    string folderPath = entry.Value as string ?? string.Empty; // Ensure folder path is a string
    return new KeaCoreEntry(name, folderPath, TitleNum: null); // TitleNum will be assigned later
})
.ToDictionary(entry => entry.Name); // Store in a dictionary for fast lookup

// Get title numbers and map them to the correct entry
foreach (var entry in envVars.Where(e => e.Key is string key && key.StartsWith(PrefixTitle)))
{
    string key = entry.Key?.ToString() ?? string.Empty;
    string name = key.Length > PrefixTitle.Length ? key[PrefixTitle.Length..] : string.Empty; // Remove prefix
    string titleNum = entry.Value as string ?? string.Empty; // Ensure titleNum is a string

    if (entries.ContainsKey(name))
    {
        entries[name] = entries[name] with { TitleNum = titleNum }; // Assign TitleNum using record immutability
    }
}

// Convert back to array
var processedEntries = entries.Values.ToArray();

// Exit if no valid entries were found
if (processedEntries.Length == 0)
{
    Console.WriteLine("No valid KEACORE_ENTRY_ environment variables found.");
    return;
}

Console.WriteLine("Processing Webtoons...");

for (int i = 0; i < processedEntries.Length; i++)
{
    var entry = processedEntries[i];
    Console.WriteLine($"Processing: {entry.Name}");
    Console.WriteLine($"Saving to: {entry.FolderPath}");

    // Ensure TitleNum is available
    if (string.IsNullOrWhiteSpace(entry.TitleNum))
    {
        Console.WriteLine($"Skipping {entry.Name}: No matching KEACORE_TITLE_NUM_ value found.");
        continue;
    }

    // Construct Webtoon URL using TitleNum
    string webtoonUrl = $"https://www.webtoons.com/en/canvas/{entry.Name}/list?title_no={entry.TitleNum}";

    // ✅ Validate the name by extracting it from the URL
    if (!Webtoons.TryExtractNameFromUrl(webtoonUrl, out string extractedName))
    {
        Console.WriteLine($"Invalid Webtoon URL: {webtoonUrl}");
        continue;
    }

    // ✅ Fetch chapters
    Console.WriteLine("Fetching chapters...");
    var chapters = await Webtoons.GetChaptersAsync(new List<string> { webtoonUrl });

    // ✅ Fix: Ensure `chapters.Count` is being compared correctly
    if (chapters == null || chapters.Count == 0)
    {
        Console.WriteLine($"No chapters found for {entry.Name}");
        continue;
    }

    // ✅ Download each Webtoon chapter
    Console.WriteLine($"Downloading {chapters.Count} chapters...");
    await Webtoons.DownloadComicAsync(
        entry.FolderPath,
        extractedName,
        chapters.First(), // ✅ Fix: Ensure a valid chapter list is passed
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

// ✅ Move the record **below** top-level statements to avoid compilation errors
record KeaCoreEntry(string Name, string FolderPath, string? TitleNum);
