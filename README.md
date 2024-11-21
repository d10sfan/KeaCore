# KeaCore

Kea ported to .NET Core. Allows to download public comics from Webtoons. Based on the project Kea.

<img src="https://github.com/user-attachments/assets/6bc4eb25-af1f-45fd-82ad-4cd61a865cda" width="600px">


## Building

Make sure .net core 9 is installed and available. The project is multi-platform, although has just been tested on Linux.

Then you can run `dotnet run` to build and run the project, or use `dotnet build` just for building.

## Using

Using your build or one of the releases provided, you can use the UI to enter webtoon urls, add them to the queue, manage the queue, pick options like the save directory and what to save the chapters as, and then the Start button.

Once you have filled out all of the required fields, you can click the Start button, and then the progress will be shown at the bottom of the screen.

## Thanks To

* Original Project Kea - https://github.com/RustingRobot/Kea
* HtmlAgilityPack - for parsing HTML
* ITextSharp - for converting images to a PDF file
* Avalonia - For the cross-platform UI.
* Icon from https://icon-icons.com/icon/book-bookmark/34486 (CC 4.0)
