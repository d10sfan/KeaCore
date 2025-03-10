# KeaCore

Kea ported to .NET Core. Allows to download public comics from Webtoons. Based on the project Kea.

<img src="https://github.com/user-attachments/assets/6bc4eb25-af1f-45fd-82ad-4cd61a865cda" width="600px">


## Building

Make sure .net core 9 is installed and available. The project is multi-platform, although has just been tested on Linux.

Then you can run `dotnet run --project KeaCore.UI` to build and run the project, or use `dotnet build` just for building.

There is also a CLI project, which has a Dockerfile for using.

## Using

Using your build or one of the releases provided, you can use the UI to enter webtoon urls, add them to the queue, manage the queue, pick options like the save directory and what to save the chapters as, and then the Start button.

Once you have filled out all of the required fields, you can click the Start button, and then the progress will be shown at the bottom of the screen.

## Using (Docker Version)

This can be used to download on a schedule, for example.

Note that this will only grab the first 4 pages to avoid rate limiting. It's recommended to download the whole series as needed using the UI, and then use this way to keep up to date from there.

```yaml
  keacore-cli:
    image: ghcr.io/d10sfan/keacore-cli:latest
    container_name: keacore-cli
    restart: "no"  # Prevents automatic restart after completion
    stdin_open: true  # Keep stdin open for interactive mode
    tty: true         # Allocate a pseudo-TTY
    environment:
      - KEACORE_FOLDER_PATH=/data
      - KEACORE_TITLE_NUM_<toon_url_name_here>=<number_here>
    volumes:
      - <your_path_here>:/data
    labels:
      - "ofelia.enabled=true"
      - "ofelia.job-exec.run.schedule=0 3 * * *"  # Runs daily at 3 AM
      - "ofelia.job-exec.run.command=docker start -a keacore-cli"  # Restart the stopped container

  ofelia:
    image: mcuadros/ofelia:latest
    container_name: ofelia
    restart: always
    command: daemon --docker
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock  # Required to control Docker
```

## Thanks To

* Original Project Kea - https://github.com/RustingRobot/Kea
* HtmlAgilityPack - for parsing HTML
* ITextSharp - for converting images to a PDF file
* Avalonia - For the cross-platform UI.
* Icon from https://icon-icons.com/icon/book-bookmark/34486 (CC 4.0)
