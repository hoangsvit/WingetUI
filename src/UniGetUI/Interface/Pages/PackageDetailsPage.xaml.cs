using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UniGetUI.Core;
using UniGetUI.Core.Data;
using UniGetUI.PackageEngine.Classes;
using UniGetUI.PackageEngine.Operations;
using UniGetUI.Core.Logging;
using Windows.Storage;
using Windows.Storage.Pickers;
using UniGetUI.PackageEngine.PackageClasses;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.Core.Tools;
using UniGetUI.Core.IconEngine;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UniGetUI.Interface.Dialogs
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PackageDetailsPage : Page
    {
        public Package Package;
        private InstallOptionsPage InstallOptionsPage;
        public event EventHandler? Close;
        private PackageDetails? Info;
        OperationType FutureOperation;
        bool PackageHasScreenshots = false;
        public ObservableCollection<TextBlock> ShowableTags = new();

        private enum LayoutMode
        {
            Normal,
            Wide,
            Unloaded
        }

        private LayoutMode __layout_mode = LayoutMode.Unloaded;
        public PackageDetailsPage(Package package, OperationType futureOperation)
        {
            FutureOperation = futureOperation;
            Package = package;

            InitializeComponent();

            InstallOptionsPage = new InstallOptionsPage(package, futureOperation);
            InstallOptionsExpander.Content = InstallOptionsPage;

            SizeChanged += PackageDetailsPage_SizeChanged;

            if (futureOperation == OperationType.None)
                futureOperation = OperationType.Install;

            switch (futureOperation)
            {
                case OperationType.Install:
                    ActionButton.Content = CoreTools.Translate("Install");
                    break;
                case OperationType.Uninstall:
                    ActionButton.Content = CoreTools.Translate("Uninstall");
                    break;
                case OperationType.Update:
                    ActionButton.Content = CoreTools.Translate("Update");
                    break;
            }

            IdTextBlock.Text = package.Id;
            VersionTextBlock.Text = package.Version;
            if (package.IsUpgradable)
                VersionTextBlock.Text += " - " + CoreTools.Translate("Update to {0} available").Replace("{0}", package.NewVersion);
            PackageName.Text = package.Name;
            SourceNameTextBlock.Text = package.SourceAsString;


            string LoadingString = CoreTools.Translate("Loading...");
            LoadingIndicator.Visibility = Visibility.Visible;


            HomepageUrlButton.Content = LoadingString;
            PublisherTextBlock.Text = LoadingString;
            AuthorTextBlock.Text = LoadingString;
            LicenseTextBlock.Text = LoadingString;
            LicenseUrlButton.Content = LoadingString;

            DescriptionBox.Text = LoadingString;
            ManifestUrlButton.Content = LoadingString;
            HashTextBlock.Text = LoadingString;
            InstallerUrlButton.Content = LoadingString;
            InstallerTypeTextBlock.Text = LoadingString;
            UpdateDateTextBlock.Text = LoadingString;
            ReleaseNotesBlock.Text = LoadingString;
            InstallerSizeTextBlock.Text = LoadingString;
            DownloadInstallerButton.IsEnabled = false;
            ReleaseNotesUrlButton.Content = LoadingString;

            _ = LoadInformation();

        }
        public async Task LoadInformation()
        {
            LoadingIndicator.Visibility = Visibility.Visible;

            LoadIcon();
            LoadScreenshots();

            string NotFound = CoreTools.Translate("Not available");
            Uri InvalidUri = new("about:blank");
            Info = await Package.Manager.GetPackageDetails(Package);
            Logger.Debug("Received info " + Info);

            string command = "";

            switch (FutureOperation)
            {
                case OperationType.Install:
                    command = Package.Manager.Properties.ExecutableFriendlyName + " " + String.Join(' ', Package.Manager.GetInstallParameters(Package, await InstallationOptions.FromPackageAsync(Package)));
                    break;

                case OperationType.Uninstall:
                    command = Package.Manager.Properties.ExecutableFriendlyName + " " + String.Join(' ', Package.Manager.GetUninstallParameters(Package, await InstallationOptions.FromPackageAsync(Package)));
                    break;

                case OperationType.Update:
                    command = Package.Manager.Properties.ExecutableFriendlyName + " " + String.Join(' ', Package.Manager.GetUpdateParameters(Package, await InstallationOptions.FromPackageAsync(Package)));
                    break;
            }
            CommandTextBlock.Text = command;

            LoadingIndicator.Visibility = Visibility.Collapsed;

            HomepageUrlButton.Content = Info.HomepageUrl != null ? Info.HomepageUrl : NotFound;
            HomepageUrlButton.NavigateUri = Info.HomepageUrl != null ? Info.HomepageUrl : InvalidUri;
            PublisherTextBlock.Text = Info.Publisher != "" ? Info.Publisher : NotFound;
            AuthorTextBlock.Text = Info.Author != "" ? Info.Author : NotFound;
            LicenseTextBlock.Text = Info.License != "" ? Info.License : NotFound;
            if (Info.License != "" && Info.LicenseUrl != null)
            {
                LicenseTextBlock.Text = Info.License;
                LicenseUrlButton.Content = "(" + Info.LicenseUrl + ")";
                LicenseUrlButton.NavigateUri = Info.LicenseUrl;
            }
            else if (Info.License != "" && Info.LicenseUrl == null)
            {
                LicenseTextBlock.Text = Info.License;
                LicenseUrlButton.Content = "";
                LicenseUrlButton.NavigateUri = InvalidUri;
            }
            else if (Info.License == "" && Info.LicenseUrl != null)
            {
                LicenseTextBlock.Text = "";
                LicenseUrlButton.Content = Info.LicenseUrl;
                LicenseUrlButton.NavigateUri = Info.LicenseUrl;
            }
            else
            {
                LicenseTextBlock.Text = NotFound;
                LicenseUrlButton.Content = "";
                LicenseUrlButton.NavigateUri = InvalidUri;
            }

            DescriptionBox.Text = Info.Description != "" ? Info.Description : NotFound;
            ManifestUrlButton.Content = Info.ManifestUrl != null ? Info.ManifestUrl : NotFound;
            ManifestUrlButton.NavigateUri = Info.ManifestUrl != null ? Info.ManifestUrl : InvalidUri;
            HashTextBlock.Text = Info.InstallerHash != "" ? Info.InstallerHash : NotFound;
            InstallerUrlButton.Content = Info.InstallerUrl != null ? Info.InstallerUrl : NotFound;
            InstallerUrlButton.NavigateUri = Info.InstallerUrl != null ? Info.InstallerUrl : InvalidUri;
            InstallerTypeTextBlock.Text = Info.InstallerType != "" ? Info.InstallerType : NotFound;
            UpdateDateTextBlock.Text = Info.UpdateDate != "" ? Info.UpdateDate : NotFound;
            ReleaseNotesBlock.Text = Info.ReleaseNotes != "" ? Info.ReleaseNotes : NotFound;
            InstallerSizeTextBlock.Text = Info.InstallerSize != 0.0 ? Info.InstallerSize.ToString() + " MB" : NotFound;
            DownloadInstallerButton.IsEnabled = Info.InstallerUrl != null;
            ReleaseNotesUrlButton.Content = Info.ReleaseNotesUrl != null ? Info.ReleaseNotesUrl : NotFound;
            ReleaseNotesUrlButton.NavigateUri = Info.ReleaseNotesUrl != null ? Info.ReleaseNotesUrl : InvalidUri;

            ShowableTags.Clear();
            foreach (string tag in Info.Tags)
                ShowableTags.Add(new TextBlock() { Text = tag });
        }

        public async void LoadIcon()
        {
            PackageIcon.Source = new BitmapImage() { UriSource = (await Package.GetIconUrl()) };
        }

        public async void LoadScreenshots()
        {
            var screenshots = await Package.GetPackageScreenshots();
            PackageHasScreenshots = screenshots.Count() > 0;
            if (PackageHasScreenshots)
            {
                PackageHasScreenshots = true;
                IconsExtraBanner.Visibility = Visibility.Visible;
                ScreenshotsCarroussel.Items.Clear();
                foreach (Uri image in screenshots)
                    ScreenshotsCarroussel.Items.Add(new Image() { Source = new BitmapImage(image) });
            }

            __layout_mode = LayoutMode.Unloaded;
            PackageDetailsPage_SizeChanged();

        }

        public void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            Close?.Invoke(this, new EventArgs());
            InstallOptionsPage.SaveToDisk();
            switch (FutureOperation)
            {
                case OperationType.Install:
                    MainApp.Instance.AddOperationToList(new InstallPackageOperation(Package, InstallOptionsPage.Options));
                    break;
                case OperationType.Uninstall:
                    MainApp.Instance.MainWindow.NavigationPage.InstalledPage.ConfirmAndUninstall(Package, InstallOptionsPage.Options);
                    break;
                case OperationType.Update:
                    MainApp.Instance.AddOperationToList(new UpdatePackageOperation(Package, InstallOptionsPage.Options));
                    break;
            }
        }

        public void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            MainApp.Instance.MainWindow.SharePackage(Package);
        }

        public async void DownloadInstallerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Info?.InstallerUrl == null)
                    return;

                ErrorOutput.Text = "";
                FileSavePicker savePicker = new();
                MainWindow window = MainApp.Instance.MainWindow;
                IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);
                savePicker.SuggestedStartLocation = PickerLocationId.Downloads;
                savePicker.SuggestedFileName = Package.Id + " installer." + Info.InstallerUrl.ToString().Split('.')[^1];
                if (Info.InstallerUrl.ToString().Split('.')[^1] == "nupkg")
                    savePicker.FileTypeChoices.Add("Compressed Manifest File", new System.Collections.Generic.List<string>() { ".zip" });
                savePicker.FileTypeChoices.Add("Default", new System.Collections.Generic.List<string>() { "." + Info.InstallerUrl.ToString().Split('.')[^1] });
                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    DownloadInstallerButton.Content = CoreTools.Translate("Downloading");
                    DownloadInstallerButtonProgress.Visibility = Visibility.Visible;
                    Logger.Debug($"Downloading installer ${file.Path.ToString()}");
                    using HttpClient httpClient = new();
                    await using Stream s = await httpClient.GetStreamAsync(Info.InstallerUrl);
                    await using FileStream fs = File.OpenWrite(file.Path.ToString());
                    await s.CopyToAsync(fs);
                    fs.Dispose();
                    Logger.ImportantInfo($"Installer for {Package.Id} has been downloaded successfully");
                    DownloadInstallerButtonProgress.Visibility = Visibility.Collapsed;
                    System.Diagnostics.Process.Start("explorer.exe", "/select," + file.Path.ToString());
                    DownloadInstallerButton.Content = CoreTools.Translate("Download succeeded");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"An error occurred while downloading the installer for the package {Package.Id}");
                Logger.Error(ex);
                DownloadInstallerButton.Content = CoreTools.Translate("An error occurred");
                DownloadInstallerButtonProgress.Visibility = Visibility.Collapsed;
                ErrorOutput.Text = ex.Message;
            }


        }
        public void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close?.Invoke(this, new EventArgs());
        }

        public void PackageDetailsPage_SizeChanged(object? sender = null, SizeChangedEventArgs? e = null)
        {
            if (MainApp.Instance.MainWindow.AppWindow.Size.Width < 950)
            {
                if (__layout_mode != LayoutMode.Normal)
                {
                    __layout_mode = LayoutMode.Normal;

                    MainGrid.ColumnDefinitions.Clear();
                    MainGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                    Grid.SetColumn(TitlePanel, 0);
                    Grid.SetColumn(BasicInfoPanel, 0);
                    Grid.SetColumn(ScreenshotsPanel, 0);
                    Grid.SetColumn(ActionsPanel, 0);
                    Grid.SetColumn(InstallOptionsBorder, 0);
                    Grid.SetColumn(MoreDataStackPanel, 0);

                    MainGrid.RowDefinitions.Clear();
                    MainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
                    MainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
                    MainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
                    MainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
                    MainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
                    MainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
                    MainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
                    Grid.SetRow(TitlePanel, 0);
                    Grid.SetRow(DescriptionPanel, 1);
                    Grid.SetRow(BasicInfoPanel, 2);
                    Grid.SetRow(ActionsPanel, 3);
                    Grid.SetRow(InstallOptionsBorder, 4);
                    Grid.SetRow(ScreenshotsPanel, 5);
                    Grid.SetRow(MoreDataStackPanel, 6);

                    LeftPanel.Children.Clear();
                    RightPanel.Children.Clear();
                    MainGrid.Children.Clear();
                    MainGrid.Children.Add(TitlePanel);
                    MainGrid.Children.Add(DescriptionPanel);
                    MainGrid.Children.Add(BasicInfoPanel);
                    MainGrid.Children.Add(ScreenshotsPanel);
                    MainGrid.Children.Add(ActionsPanel);
                    MainGrid.Children.Add(InstallOptionsBorder);
                    MainGrid.Children.Add(MoreDataStackPanel);
                    ScreenshotsCarroussel.Height = PackageHasScreenshots ? 225 : 150;

                    InstallOptionsExpander.IsExpanded = false;

                }
            }
            else
            {
                if (__layout_mode != LayoutMode.Wide)
                {
                    __layout_mode = LayoutMode.Wide;

                    MainGrid.ColumnDefinitions.Clear();
                    MainGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), MinWidth = 550 });
                    MainGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                    Grid.SetColumn(LeftPanel, 0);
                    Grid.SetColumn(RightPanel, 1);
                    Grid.SetColumn(TitlePanel, 0);
                    Grid.SetColumnSpan(TitlePanel, 1);

                    MainGrid.RowDefinitions.Clear();
                    MainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
                    MainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
                    Grid.SetRow(LeftPanel, 1);
                    Grid.SetRow(RightPanel, 0);
                    Grid.SetRow(TitlePanel, 0);
                    Grid.SetRowSpan(RightPanel, 2);

                    LeftPanel.Children.Clear();
                    RightPanel.Children.Clear();
                    MainGrid.Children.Clear();
                    LeftPanel.Children.Add(DescriptionPanel);
                    LeftPanel.Children.Add(BasicInfoPanel);
                    RightPanel.Children.Add(ScreenshotsPanel);
                    LeftPanel.Children.Add(ActionsPanel);
                    LeftPanel.Children.Add(InstallOptionsBorder);
                    RightPanel.Children.Add(MoreDataStackPanel);
                    ScreenshotsCarroussel.Height = PackageHasScreenshots ? 400 : 150;

                    InstallOptionsExpander.IsExpanded = true;

                    MainGrid.Children.Add(LeftPanel);
                    MainGrid.Children.Add(RightPanel);
                    MainGrid.Children.Add(TitlePanel);

                }
            }
        }
    }
}
