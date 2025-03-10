// <copyright file="MainWindow.axaml.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using KeaCore.Common;

namespace KeaCore.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        // Bind the QueueItems collection to the DataGrid
        this.QueueTable.ItemsSource = this.QueueItems;

        // Default values for RadioButtons
        this.SaveChaptersCBZRadioButton.IsChecked = true; // Default to "CBZ"

        // Monitor changes in the UrlsTextBox
        this.UrlsTextBox.KeyUp += this.OnUrlsTextBoxChanged;

        // Initial state for buttons
        this.UpdateButtonStates();

        // Subscribe to the Webtoons status updates
        Webtoons.StatusUpdated += this.UpdateStatusLabel;
    }

    // Define QueueItems as an ObservableCollection
    public ObservableCollection<Webtoons.QueueItem> QueueItems { get; } = new ObservableCollection<Webtoons.QueueItem>();

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Unsubscribe from the event to avoid memory leaks
        Webtoons.StatusUpdated -= this.UpdateStatusLabel;
    }

    private void UpdateStatusLabel(string status)
    {
        Console.WriteLine(status);

        // Ensure the update happens on the UI thread
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            this.CurrentlyProcessingLabel.Text = status; // Update the label
        });
    }

    private async void OnDirectoryButtonClick(object? sender, RoutedEventArgs e)
    {
        if (this.StorageProvider != null)
        {
            var folder = await this.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Select Directory",
                AllowMultiple = false,
            });

            if (folder.Count > 0 && folder[0] != null)
            {
                this.DirectoryTextBox.Text = folder[0].Path.LocalPath;
                this.UpdateButtonStates(); // Reevaluate button states after selecting a directory
            }
        }
    }

    private void OnAddAllToQueueClicked(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(this.UrlsTextBox.Text))
        {
            var urls = this.UrlsTextBox.Text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            var invalidUrls = new StringBuilder();

            foreach (var url in urls)
            {
                if (Webtoons.TryExtractNameFromUrl(url, out string name))
                {
                    this.QueueItems.Add(new Webtoons.QueueItem
                    {
                        OriginalUrl = url,
                        Name = name,
                        StartAtChapter = "1",
                        EndAtChapter = "end",
                    });
                }
                else
                {
                    invalidUrls.AppendLine(url);
                }
            }

            if (invalidUrls.Length > 0)
            {
                this.ShowErrorDialog("Invalid URLs", $"The following URLs could not be processed:\n\n{invalidUrls}");
            }

            this.UrlsTextBox.Text = string.Empty;
            this.UpdateButtonStates();
        }
    }

    private async void ShowErrorDialog(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 300,
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = new StackPanel
                {
                    Margin = new Thickness(10),
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            MaxWidth = 380,
                        },
                        new Button
                        {
                            Content = "Close",
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        },
                    },
                },
            },
        };

        // Add event handler to close the dialog
        var scrollViewer = dialog.Content as ScrollViewer;
        var stackPanel = scrollViewer?.Content as StackPanel;
        var closeButton = stackPanel?.Children.OfType<Button>().FirstOrDefault();

        if (closeButton != null)
        {
            closeButton.Click += (_, _) => dialog.Close();
        }

        await dialog.ShowDialog(this);
    }

    private void OnUrlsTextBoxChanged(object? sender, EventArgs e)
    {
        // Update button states when the UrlsTextBox content changes
        this.UpdateButtonStates();
    }

    private void OnQueueTableSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Enable or disable the Remove Selected button based on selection
        this.RemoveSelectedButton.IsEnabled = this.QueueTable.SelectedItem != null;
    }

    private void OnRemoveSelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (this.QueueTable.SelectedItem is Webtoons.QueueItem selectedItem)
        {
            this.QueueItems.Remove(selectedItem);

            // Update button states
            this.UpdateButtonStates();
        }
    }

    private void OnRemoveAllClicked(object? sender, RoutedEventArgs e)
    {
        this.QueueItems.Clear();

        // Update button states
        this.UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        this.RemoveAllButton.IsEnabled = this.QueueItems.Count > 0;
        this.RemoveSelectedButton.IsEnabled = this.QueueTable.SelectedItem != null;
        this.AddAllToQueueButton.IsEnabled = !string.IsNullOrWhiteSpace(this.UrlsTextBox.Text);
        this.StartButton.IsEnabled = this.QueueItems.Count > 0 && !string.IsNullOrWhiteSpace(this.DirectoryTextBox.Text);
    }

    private string GetSelectedSaveOption()
    {
        // Iterate through each RadioButton in the visual tree
        foreach (var control in this.FindLogicalChildren<RadioButton>())
        {
            // Check if the RadioButton belongs to the "SaveChaptersAs" group and is checked
            if (control.GroupName == "SaveChaptersAs" && control.IsChecked == true)
            {
                return control.Content?.ToString() ?? string.Empty;
            }
        }

        // Default value if no selection is made
        return string.Empty;
    }

    private async void StartBtn_Click(object? sender, RoutedEventArgs e)
    {
        this.UpdateStatusLabel("Starting download...");
        foreach (var item in this.QueueItems)
        {
            int start = int.Parse(item.StartAtChapter);
            string end = item.EndAtChapter;

            if (!Webtoons.ValidateChapters(start, end))
            {
                this.ShowErrorDialog("Invalid Chapters", "Start and end chapters are not valid.");
                return;
            }
        }

        this.DisableAllControls(this);
        string saveAs = this.GetSelectedSaveOption();
        string savePath = this.DirectoryTextBox.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(savePath))
        {
            this.ShowErrorDialog("Invalid Save Path", "Please select a valid directory.");
            return;
        }

        var urls = this.QueueItems.Select(q => q.OriginalUrl).ToList();

        try
        {
            this.UpdateStatusLabel("Getting chapters..");
            var chapters = await Webtoons.GetChaptersAsync(urls);

            for (int i = 0; i < chapters.Count; i++)
            {
                this.UpdateStatusLabel($"Getting comic {i + 1}..");
                await Webtoons.DownloadComicAsync(savePath, this.QueueItems[i].Name, chapters[i], saveAs, this.QueueItems[i].StartAtChapter, this.QueueItems[i].EndAtChapter);
            }

            this.UpdateStatusLabel("Download complete!");
        }
        catch (Exception ex)
        {
            this.UpdateStatusLabel($"Error downloading: {ex.Message}");
        }

        this.EnableAllControls(this);

        this.UpdateButtonStates();
    }

    private void DisableAllControls(Control parent)
    {
        foreach (var child in parent.GetLogicalChildren())
        {
            if (child is Control control)
            {
                control.IsEnabled = false;

                // Recursively disable controls for nested containers
                if (control is Panel || control is ContentControl)
                {
                    this.DisableAllControls(control);
                }
            }
        }
    }

    private void EnableAllControls(Control parent)
    {
        foreach (var child in parent.GetLogicalChildren())
        {
            if (child is Control control)
            {
                control.IsEnabled = true;

                // Recursively disable controls for nested containers
                if (control is Panel || control is ContentControl)
                {
                    this.EnableAllControls(control);
                }
            }
        }
    }
}

public static class ControlExtensions
{
    public static IEnumerable<T> FindLogicalChildren<T>(this ILogical logical)
        where T : class
    {
        foreach (var child in logical.LogicalChildren)
        {
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            if (child is ILogical logicalChild)
            {
                foreach (var descendant in FindLogicalChildren<T>(logicalChild))
                {
                    yield return descendant;
                }
            }
        }
    }
}
