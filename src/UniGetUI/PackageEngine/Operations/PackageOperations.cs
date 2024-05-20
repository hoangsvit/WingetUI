﻿using CommunityToolkit.WinUI.Notifications;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UniGetUI.Core;
using UniGetUI.Core.Data;
using UniGetUI.Interface.Widgets;
using UniGetUI.Interface.Enums;
using UniGetUI.Core.Logging;
using UniGetUI.PackageEngine.Classes;
using UniGetUI.Core.SettingsEngine;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.PackageClasses;
using UniGetUI.PackageEngine.ManagerClasses;
using UniGetUI.Core.Tools;

namespace UniGetUI.PackageEngine.Operations
{

    public class OperationCancelledEventArgs : EventArgs
    {
        public OperationStatus OldStatus;
        public OperationCancelledEventArgs(OperationStatus OldStatus)
        {
            this.OldStatus = OldStatus;
        }
    }

    public abstract class PackageOperation : AbstractOperation
    {

        public Package Package;
        protected InstallationOptions Options;
        public PackageOperation(Package package, InstallationOptions options)
        {
            Package = package;
            Options = options;
            MainProcedure();
        }

        protected override async Task WaitForAvailability()
        {
            if (Settings.Get("AllowParallelInstalls") || Settings.Get("AllowParallelInstallsForManager" + Package.Manager.Name))
            {
                Logger.Debug("Parallel installs are allowed. Skipping queue check");
                return;
            }

            Package.SetTag(PackageTag.OnQueue);

            AddToQueue();
            int currentIndex = -2;
            int oldIndex = -1;
            while (currentIndex != 0)
            {
                if (Status == OperationStatus.Cancelled)
                {
                    Package.Tag = PackageTag.Default;
                    return; // If th operation has been cancelled
                }
                currentIndex = MainApp.Instance.OperationQueue.IndexOf(this);
                if (currentIndex != oldIndex)
                {
                    LineInfoText = CoreTools.Translate("Operation on queue (position {0})...").Replace("{0}", currentIndex.ToString());
                    oldIndex = currentIndex;
                }
                await Task.Delay(100);
            }

            Package.SetTag(PackageTag.BeingProcessed);
        }

        public PackageOperation(Package package) : this(package, new InstallationOptions(package)) { }
    }

    public class InstallPackageOperation : PackageOperation
    {

        public InstallPackageOperation(Package package, InstallationOptions options) : base(package, options) { }
        public InstallPackageOperation(Package package) : base(package) { }
        protected override Process BuildProcessInstance(ProcessStartInfo startInfo)
        {
            if (Options.RunAsAdministrator || Settings.Get("AlwaysElevate" + Package.Manager.Name))
            {
                if (Settings.Get("DoCacheAdminRights") || Settings.Get("DoCacheAdminRightsForBatches"))
                {
                    Logger.Info("Caching admin rights for process id " + Process.GetCurrentProcess().Id);
                    Process p = new();
                    p.StartInfo.FileName = MainApp.Instance.GSudoPath;
                    p.StartInfo.Arguments = "cache on --pid " + Process.GetCurrentProcess().Id + " -d 1";
                    p.Start();
                    p.WaitForExit();
                }
                startInfo.FileName = MainApp.Instance.GSudoPath;
                startInfo.Arguments = "\"" + Package.Manager.Status.ExecutablePath + "\" " + Package.Manager.Properties.ExecutableCallArgs + " " + string.Join(" ", Package.Manager.GetInstallParameters(Package, Options));

            }
            else
            {
                startInfo.FileName = Package.Manager.Status.ExecutablePath;
                startInfo.Arguments = Package.Manager.Properties.ExecutableCallArgs + " " + string.Join(" ", Package.Manager.GetInstallParameters(Package, Options));
            }
            Process process = new();
            process.StartInfo = startInfo;

            return process;
        }

        protected override string[] GenerateProcessLogHeader()
        {
            return new string[]
            {
                "Starting package install operation for package id=" + Package.Id + "with Manager name=" + Package.Manager.Name,
                "Given installation options are " + Options.ToString()
            };
        }

        protected override OperationVeredict GetProcessVeredict(int ReturnCode, string[] Output)
        {
            return Package.Manager.GetInstallOperationVeredict(Package, Options, ReturnCode, Output);
        }

        protected override async Task<AfterFinshAction> HandleFailure()
        {
            LineInfoText = CoreTools.Translate("{package} installation failed").Replace("{package}", Package.Name);

            Package.SetTag(PackageTag.Failed);

            if (!Settings.Get("DisableErrorNotifications") && !Settings.Get("DisableNotifications"))
                try
                {
                    new ToastContentBuilder()
                        .AddArgument("action", "OpenUniGetUI")
                        .AddArgument("notificationId", CoreData.VolatileNotificationIdCounter)
                        .AddText(CoreTools.Translate("Installation failed"))
                        .AddText(CoreTools.Translate("{package} could not be installed").Replace("{package}", Package.Name)).Show();

                }
                catch (Exception ex)
                {
                    Logger.Warn("Failed to show toast notification");
                    Logger.Warn(ex);
                }
            ContentDialog dialog = new();
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.XamlRoot = XamlRoot;
            dialog.Resources["ContentDialogMaxWidth"] = 750;
            dialog.Resources["ContentDialogMaxHeight"] = 1000;
            dialog.Title = CoreTools.Translate("{package} installation failed").Replace("{package}", Package.Name);

            StackPanel panel = new() { Spacing = 16 };
            panel.Children.Add(new TextBlock() { TextWrapping = TextWrapping.WrapWholeWords, Text = CoreTools.Translate("{package} could not be installed").Replace("{package}", Package.Name) + ". " + CoreTools.Translate("Please see the Command-line Output or refer to the Operation History for further information about the issue.") });

            Expander expander = new() { CornerRadius = new CornerRadius(8) };

            StackPanel HeaderPanel = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
            HeaderPanel.Children.Add(new LocalIcon("console") { VerticalAlignment = VerticalAlignment.Center, Height = 24, Width = 24, HorizontalAlignment = HorizontalAlignment.Left });
            HeaderPanel.Children.Add(new TextBlock() { Text = CoreTools.Translate("Command-line Output"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });

            expander.Header = HeaderPanel;
            expander.HorizontalAlignment = HorizontalAlignment.Stretch;
            panel.Children.Add(expander);

            RichTextBlock output = new() { FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap };
            ScrollViewer sv = new();
            sv.MaxHeight = 500;
            Paragraph par = new();
            foreach (string line in ProcessOutput)
                par.Inlines.Add(new Run() { Text = line + "\x0a" });
            output.Blocks.Add(par);

            sv.Content = output;
            expander.Content = sv;

            dialog.Content = panel;
            dialog.PrimaryButtonText = CoreTools.Translate("Retry");
            dialog.CloseButtonText = CoreTools.Translate("Close");
            dialog.DefaultButton = ContentDialogButton.Primary;

            ContentDialogResult result = await MainApp.Instance.MainWindow.ShowDialogAsync(dialog);

            if (result == ContentDialogResult.Primary)
                return AfterFinshAction.Retry;
            else
                return AfterFinshAction.ManualClose;
        }

        protected override async Task<AfterFinshAction> HandleSuccess()
        {
            LineInfoText = CoreTools.Translate("{package} was installed successfully").Replace("{package}", Package.Name);

            Package.SetTag(PackageTag.AlreadyInstalled);
            MainApp.Instance.MainWindow.NavigationPage.InstalledPage.AddInstalledPackage(Package);

            if (!Settings.Get("DisableSuccessNotifications") && !Settings.Get("DisableNotifications"))

                try
                {
                    new ToastContentBuilder()
                    .AddArgument("action", "OpenUniGetUI")
                    .AddArgument("notificationId", CoreData.VolatileNotificationIdCounter)
                    .AddText(CoreTools.Translate("Installation succeeded"))
                    .AddText(CoreTools.Translate("{package} was installed successfully").Replace("{package}", Package.Name)).Show();

                }
                catch (Exception ex)
                {
                    Logger.Warn("Failed to show toast notification");
                    Logger.Warn(ex);
                }
            await Task.Delay(0);
            return AfterFinshAction.TimeoutClose;
        }

        protected override async void Initialize()
        {
            OperationTitle = CoreTools.Translate("{package} Installation").Replace("{package}", Package.Name);
            IconSource = await Package.GetIconUrl();
        }
    }

    public class UpdatePackageOperation : PackageOperation
    {

        public UpdatePackageOperation(Package package, InstallationOptions options) : base(package, options) { }
        public UpdatePackageOperation(Package package) : base(package) { }
        protected override Process BuildProcessInstance(ProcessStartInfo startInfo)
        {
            if (Options.RunAsAdministrator || Settings.Get("AlwaysElevate" + Package.Manager.Name))
            {
                if (Settings.Get("DoCacheAdminRights") || Settings.Get("DoCacheAdminRightsForBatches"))
                {
                    Logger.Info("Caching admin rights for process id " + Process.GetCurrentProcess().Id);
                    Process p = new();
                    p.StartInfo.FileName = MainApp.Instance.GSudoPath;
                    p.StartInfo.Arguments = "cache on --pid " + Process.GetCurrentProcess().Id + " -d 1";
                    p.Start();
                    p.WaitForExit();
                }
                startInfo.FileName = MainApp.Instance.GSudoPath;
                startInfo.Arguments = "\"" + Package.Manager.Status.ExecutablePath + "\" " + Package.Manager.Properties.ExecutableCallArgs + " " + string.Join(" ", Package.Manager.GetUpdateParameters(Package, Options));
            }
            else
            {
                startInfo.FileName = Package.Manager.Status.ExecutablePath;
                startInfo.Arguments = Package.Manager.Properties.ExecutableCallArgs + " " + string.Join(" ", Package.Manager.GetUpdateParameters(Package, Options));
            }
            Process process = new();
            process.StartInfo = startInfo;

            return process;
        }

        protected override string[] GenerateProcessLogHeader()
        {
            return new string[]
            {
                "Starting package update operation for package id=" + Package.Id + "with Manager name=" + Package.Manager.Name,
                "Given installation options are " + Options.ToString()
            };
        }

        protected override OperationVeredict GetProcessVeredict(int ReturnCode, string[] Output)
        {
            return Package.Manager.GetUpdateOperationVeredict(Package, Options, ReturnCode, Output);
        }

        protected override async Task<AfterFinshAction> HandleFailure()
        {
            LineInfoText = CoreTools.Translate("{package} update failed. Click here for more details.").Replace("{package}", Package.Name);

            Package.SetTag(PackageTag.Failed);

            if (!Settings.Get("DisableErrorNotifications") && !Settings.Get("DisableNotifications"))
                try
                {
                    new ToastContentBuilder()
                    .AddArgument("action", "OpenUniGetUI")
                    .AddArgument("notificationId", CoreData.VolatileNotificationIdCounter)
                    .AddText(CoreTools.Translate("Update failed"))
                    .AddText(CoreTools.Translate("{package} could not be updated").Replace("{package}", Package.Name)).Show();

                }
                catch (Exception ex)
                {
                    Logger.Warn("Failed to show toast notification");
                    Logger.Warn(ex);
                }
            ContentDialog dialog = new();
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.XamlRoot = XamlRoot;
            dialog.Resources["ContentDialogMaxWidth"] = 750;
            dialog.Resources["ContentDialogMaxHeight"] = 1000;
            dialog.Title = CoreTools.Translate("{package} update failed").Replace("{package}", Package.Name);

            StackPanel panel = new() { Spacing = 16 };
            panel.Children.Add(new TextBlock() { TextWrapping = TextWrapping.WrapWholeWords, Text = CoreTools.Translate("{package} could not be updated").Replace("{package}", Package.Name) + ". " + CoreTools.Translate("Please see the Command-line Output or refer to the Operation History for further information about the issue.") });

            Expander expander = new() { CornerRadius = new CornerRadius(8) };

            StackPanel HeaderPanel = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
            HeaderPanel.Children.Add(new LocalIcon("console") { VerticalAlignment = VerticalAlignment.Center, Height = 24, Width = 24, HorizontalAlignment = HorizontalAlignment.Left });
            HeaderPanel.Children.Add(new TextBlock() { Text = CoreTools.Translate("Command-line Output"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });

            expander.Header = HeaderPanel;
            expander.HorizontalAlignment = HorizontalAlignment.Stretch;
            panel.Children.Add(expander);

            RichTextBlock output = new() { FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap };
            ScrollViewer sv = new();
            sv.MaxHeight = 500;
            Paragraph par = new();
            foreach (string line in ProcessOutput)
                par.Inlines.Add(new Run() { Text = line + "\x0a" });
            output.Blocks.Add(par);

            sv.Content = output;
            expander.Content = sv;

            dialog.Content = panel;
            dialog.PrimaryButtonText = CoreTools.Translate("Retry");
            dialog.CloseButtonText = CoreTools.Translate("Close");
            dialog.DefaultButton = ContentDialogButton.Primary;

            ContentDialogResult result = await MainApp.Instance.MainWindow.ShowDialogAsync(dialog);

            if (result == ContentDialogResult.Primary)
                return AfterFinshAction.Retry;
            else
                return AfterFinshAction.ManualClose;
        }

        protected override async Task<AfterFinshAction> HandleSuccess()
        {
            LineInfoText = CoreTools.Translate("{package} was updated successfully").Replace("{package}", Package.Name);

            Package.GetInstalledPackage()?.SetTag(PackageTag.Default);
            Package.GetAvailablePackage()?.SetTag(PackageTag.AlreadyInstalled);
            MainApp.Instance.MainWindow.NavigationPage.UpdatesPage.RemoveCorrespondingPackages(Package);

            if (!Settings.Get("DisableSuccessNotifications") && !Settings.Get("DisableNotifications"))
                try
                {
                    new ToastContentBuilder()
                .AddArgument("action", "OpenUniGetUI")
                .AddArgument("notificationId", CoreData.VolatileNotificationIdCounter)
                .AddText(CoreTools.Translate("Update succeeded"))
                .AddText(CoreTools.Translate("{package} was updated successfully").Replace("{package}", Package.Name)).Show();

                }
                catch (Exception ex)
                {
                    Logger.Warn("Failed to show toast notification");
                    Logger.Warn(ex);
                }

            if (Package.Version == "Unknown")
                await Package.AddToIgnoredUpdatesAsync(Package.NewVersion);

            return AfterFinshAction.TimeoutClose;
        }

        protected override async void Initialize()
        {
            OperationTitle = CoreTools.Translate("{package} Update").Replace("{package}", Package.Name);
            IconSource = await Package.GetIconUrl();
        }
    }

    public class UninstallPackageOperation : PackageOperation
    {

        public UninstallPackageOperation(Package package, InstallationOptions options) : base(package, options) { }
        public UninstallPackageOperation(Package package) : base(package) { }
        protected override Process BuildProcessInstance(ProcessStartInfo startInfo)
        {
            if (Options.RunAsAdministrator || Settings.Get("AlwaysElevate" + Package.Manager.Name))
            {
                if (Settings.Get("DoCacheAdminRights") || Settings.Get("DoCacheAdminRightsForBatches"))
                {
                    Logger.Info("Caching admin rights for process id " + Process.GetCurrentProcess().Id);
                    Process p = new();
                    p.StartInfo.FileName = MainApp.Instance.GSudoPath;
                    p.StartInfo.Arguments = "cache on --pid " + Process.GetCurrentProcess().Id + " -d 1";
                    p.Start();
                    p.WaitForExit();
                }
                startInfo.FileName = MainApp.Instance.GSudoPath;
                startInfo.Arguments = "\"" + Package.Manager.Status.ExecutablePath + "\" " + Package.Manager.Properties.ExecutableCallArgs + " " + string.Join(" ", Package.Manager.GetUninstallParameters(Package, Options));
            }
            else
            {
                startInfo.FileName = Package.Manager.Status.ExecutablePath;
                startInfo.Arguments = Package.Manager.Properties.ExecutableCallArgs + " " + string.Join(" ", Package.Manager.GetUninstallParameters(Package, Options));
            }
            Process process = new();
            process.StartInfo = startInfo;


            return process;
        }

        protected override string[] GenerateProcessLogHeader()
        {
            return new string[]
            {
                "Starting package uninstall operation for package id=" + Package.Id + "with Manager name=" + Package.Manager.Name,
                "Given installation options are " + Options.ToString()
            };
        }

        protected override OperationVeredict GetProcessVeredict(int ReturnCode, string[] Output)
        {
            return Package.Manager.GetUninstallOperationVeredict(Package, Options, ReturnCode, Output);
        }

        protected override async Task<AfterFinshAction> HandleFailure()
        {
            LineInfoText = CoreTools.Translate("{package} uninstall failed").Replace("{package}", Package.Name);

            Package.SetTag(PackageTag.Failed);

            if (!Settings.Get("DisableErrorNotifications") && !Settings.Get("DisableNotifications"))
                try
                {
                    new ToastContentBuilder()
                    .AddArgument("action", "OpenUniGetUI")
                    .AddArgument("notificationId", CoreData.VolatileNotificationIdCounter)
                    .AddText(CoreTools.Translate("Uninstall failed"))
                    .AddText(CoreTools.Translate("{package} could not be uninstalled").Replace("{package}", Package.Name)).Show();

                }
                catch (Exception ex)
                {
                    Logger.Warn("Failed to show toast notification");
                    Logger.Warn(ex);
                }

            ContentDialog dialog = new();
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.XamlRoot = XamlRoot;
            dialog.Resources["ContentDialogMaxWidth"] = 750;
            dialog.Resources["ContentDialogMaxHeight"] = 1000;
            dialog.Title = CoreTools.Translate("{package} uninstall failed").Replace("{package}", Package.Name);

            StackPanel panel = new() { Spacing = 16 };
            panel.Children.Add(new TextBlock() { TextWrapping = TextWrapping.WrapWholeWords, Text = CoreTools.Translate("{package} could not be uninstalled").Replace("{package}", Package.Name) + ". " + CoreTools.Translate("Please see the Command-line Output or refer to the Operation History for further information about the issue.") });

            Expander expander = new() { CornerRadius = new CornerRadius(8) };

            StackPanel HeaderPanel = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
            HeaderPanel.Children.Add(new LocalIcon("console") { VerticalAlignment = VerticalAlignment.Center, Height = 24, Width = 24, HorizontalAlignment = HorizontalAlignment.Left });
            HeaderPanel.Children.Add(new TextBlock() { Text = CoreTools.Translate("Command-line Output"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });

            expander.Header = HeaderPanel;
            expander.HorizontalAlignment = HorizontalAlignment.Stretch;
            panel.Children.Add(expander);

            RichTextBlock output = new() { FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap };
            ScrollViewer sv = new();
            sv.MaxHeight = 500;
            Paragraph par = new();
            foreach (string line in ProcessOutput)
                par.Inlines.Add(new Run() { Text = line + "\x0a" });
            output.Blocks.Add(par);

            sv.Content = output;
            expander.Content = sv;

            dialog.Content = panel;
            dialog.PrimaryButtonText = CoreTools.Translate("Retry");
            dialog.CloseButtonText = CoreTools.Translate("Close");
            dialog.DefaultButton = ContentDialogButton.Primary;

            ContentDialogResult result = await MainApp.Instance.MainWindow.ShowDialogAsync(dialog);

            if (result == ContentDialogResult.Primary)
                return AfterFinshAction.Retry;
            else
                return AfterFinshAction.ManualClose;
        }

        protected override async Task<AfterFinshAction> HandleSuccess()
        {
            LineInfoText = CoreTools.Translate("{package} was uninstalled successfully").Replace("{package}", Package.Name);

            Package.GetAvailablePackage()?.SetTag(PackageTag.Default);
            MainApp.Instance.MainWindow.NavigationPage.UpdatesPage.RemoveCorrespondingPackages(Package);
            MainApp.Instance.MainWindow.NavigationPage.InstalledPage.RemoveCorrespondingPackages(Package);

            if (!Settings.Get("DisableSuccessNotifications") && !Settings.Get("DisableNotifications"))
                try
                {
                    new ToastContentBuilder()
                .AddArgument("action", "OpenUniGetUI")
                .AddArgument("notificationId", CoreData.VolatileNotificationIdCounter)
                .AddText(CoreTools.Translate("Uninstall succeeded"))
                .AddText(CoreTools.Translate("{package} was uninstalled successfully").Replace("{package}", Package.Name)).Show();

                }
                catch (Exception ex)
                {
                    Logger.Warn("Failed to show toast notification");
                    Logger.Warn(ex);
                }
            await Task.Delay(0);
            return AfterFinshAction.TimeoutClose;
        }

        protected override async void Initialize()
        {
            OperationTitle = CoreTools.Translate("{package} Uninstall").Replace("{package}", Package.Name);
            IconSource = await Package.GetIconUrl();
        }
    }
}
