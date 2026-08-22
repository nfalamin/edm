# QUICK_REFERENCE

This document contains quick reference notes and implementation decisions extracted from development work on the EDM WPF application.

## Changes made (summary)

- Responsive WPF Layout
  - Wrapped AddUrlWindow content in a ScrollViewer to prevent clipping on small screens.
  - Ensured Grid rows/columns use Auto and * sizing appropriately.
  - DownloadProgressWindow made resizable and content scrollable with a ScrollViewer.

- Format Field (MVVM)
  - Moved format and quality lists into AddUrlViewModel (AvailableFormats / AvailableQualities).
  - Restored ItemsSource and SelectedItem bindings in AddUrlWindow.xaml for FormatComboBox and ResolutionComboBox.

- Path categorization
  - Added Helpers/PathHelper.cs which uses the user's Downloads folder (Downloads/EDM) and categorizes files into Audio, Video, Documents, or Others based on extension.
  - PathHelper safely creates directories and ensures unique filenames.

- Documentation
  - Removed an in-repo .cs quick reference file that blocked compilation and re-added its contents as this markdown file under docs/ so it no longer affects build.

## Notes

- The project builds successfully with these changes. There are some non-blocking compiler warnings (nullable/async). These can be addressed in follow-up tasks.

- If you want the format/quality logic to be extended (for example to populate based on metadata discovery), the FetchMetadata code in AddUrlWindow.xaml.cs already updates the ViewModel's AvailableFormats and AvailableQualities collections dynamically.

