using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UniGetUI.Core.Classes;
using UniGetUI.Core.Tools;
using UniGetUI.PackageEngine.Classes.Manager.ManagerHelpers;
using UniGetUI.PackageEngine.ManagerClasses.Manager;
using UniGetUI.PackageEngine.PackageClasses;
using UniGetUI.PackageEngine.Serializable;

namespace UniGetUI.PackageEngine.Classes
{

    public class SerializableBundle_v1
    {
        public double export_version { get; set; } = 2.0;
        public List<SerializableValidPackage_v1> packages { get; set; } = [];
        public string incompatible_packages_info { get; set; } = "Incompatible packages cannot be installed from WingetUI, but they have been listed here for logging purposes.";
        public List<SerializableIncompatiblePackage_v1> incompatible_packages { get; set; } = [];

    }

    public class SerializableUpdatesOptions_v1
    {
        public bool UpdatesIgnored { get; set; }
        public string IgnoredVersion { get; set; } = "";
        public static async Task<SerializableUpdatesOptions_v1> FromPackageAsync(Package package)
        {
            SerializableUpdatesOptions_v1 Serializable = new()
            {
                UpdatesIgnored = await package.HasUpdatesIgnoredAsync(),
                IgnoredVersion = await package.GetIgnoredUpdatesVersionAsync()
            };
            return Serializable;
        }
    }

    public class SerializableValidPackage_v1
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Source { get; set; } = "";
        public string ManagerName { get; set; } = "";
        public SerializableInstallationOptions_v1 InstallationOptions { get; set; } = new();
        public SerializableUpdatesOptions_v1 Updates { get; set; } = new();
    }

    public class SerializableIncompatiblePackage_v1
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Source { get; set; } = "";
    }

    public class BundledPackage : INotifyPropertyChanged, IIndexableListItem
    {
        public Package Package { get; }
        public int Index { get; set; }
        public bool IsValid { get; set; } = true;
        public InstallationOptions InstallOptions { get; set; }
        public SerializableUpdatesOptions_v1 UpdateOptions;

        public double DrawOpacity = 1;

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool __is_checked = true;
        public bool IsChecked { get { return __is_checked; } set { __is_checked = value; OnPropertyChanged(); } }

        public virtual string Name
        {
            get
            {
                return Package.Name;
            }
        }

        public virtual string Id
        {
            get
            {
                return Package.Id;
            }
        }

        public virtual string version
        {
            get
            {
                if (UpdateOptions == null || !UpdateOptions.UpdatesIgnored)
                {
                    return CoreTools.Translate("Latest");
                }

                return Package.Version;
            }
        }

        public virtual string SourceAsString
        {
            get
            {
                return Package.Source.AsString;
            }
        }

        public virtual string IconId
        {
            get
            {
                return Package.Source.IconId;
            }
        }
        public static async Task<BundledPackage> FromPackageAsync(Package package)
        {
            InstallationOptions iOptions = await InstallationOptions.FromPackageAsync(package);
            SerializableUpdatesOptions_v1 uOptions = await SerializableUpdatesOptions_v1.FromPackageAsync(package);

            return new BundledPackage(package, iOptions, uOptions);
        }

        public BundledPackage(Package package, InstallationOptions options, SerializableUpdatesOptions_v1 updateOptions)
        {
            Package = package;
            InstallOptions = options;
            IsValid = !package.Source.IsVirtualManager;
            UpdateOptions = updateOptions;
        }

        public virtual async void ShowOptions(object sender, RoutedEventArgs e)
        {
            InstallOptions = await MainApp.Instance.MainWindow.NavigationPage.UpdateInstallationSettings(Package, InstallOptions);
        }

        public void RemoveFromList(object sender, RoutedEventArgs e)
        {
            MainApp.Instance.MainWindow.NavigationPage.BundlesPage.Packages.Remove(this);
            MainApp.Instance.MainWindow.NavigationPage.BundlesPage.FilteredPackages.Remove(this);
            MainApp.Instance.MainWindow.NavigationPage.BundlesPage.UpdateCount();
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public virtual SerializableValidPackage_v1 AsSerializable()
        {
            SerializableValidPackage_v1 Serializable = new()
            {
                Id = Id,
                Name = Name,
                Version = Package.Version,
                Source = Package.Source.Name,
                ManagerName = Package.Manager.Name,
                InstallationOptions = InstallOptions.AsSerializable(),
                Updates = UpdateOptions
            };
            return Serializable;
        }

        public virtual SerializableIncompatiblePackage_v1 AsSerializable_Incompatible()
        {
            SerializableIncompatiblePackage_v1 Serializable = new()
            {
                Id = Id,
                Name = Name,
                Version = version,
                Source = SourceAsString
            };
            return Serializable;
        }

        public static Package FromSerialized(SerializableValidPackage_v1 DeserializedPackage, PackageManager manager, ManagerSource source)
        {
            return new Package(DeserializedPackage.Name, DeserializedPackage.Id, DeserializedPackage.Version, source, manager);
        }
    }

    public class InvalidBundledPackage : BundledPackage
    {
        private readonly string __name;
        private readonly string __id;
        private readonly string __version;
        private readonly string __source;
        private readonly string __manager;

        public override string Name
        {
            get
            {
                return __name;
            }
        }

        public override string Id
        {
            get
            {
                return __id;
            }
        }

        public override string version
        {
            get
            {
                return __version;
            }
        }

        public override string SourceAsString
        {
            get
            {
                if (__source == "")
                {
                    return __manager;
                }

                return __manager + ": " + __source;
            }
        }

        public override string IconId
        {
            get
            {
                return "help";
            }
        }

        public InvalidBundledPackage(string name, string id, string version, string source, string manager) : this(new Package(name, id, version, PEInterface.WinGet.DefaultSource, PEInterface.WinGet))
        {
            IsValid = false;
            DrawOpacity = 0.5;
            __name = name;
            __id = id;
            __version = version;
            __source = source;
            __manager = manager;
        }

        public InvalidBundledPackage(Package package) : base(package, InstallationOptions.FromPackage(package), new SerializableUpdatesOptions_v1())
        {
            IsValid = false;
            DrawOpacity = 0.5;
            __name = package.Name;
            __id = package.Id;
            __version = package.Version;
            if (!package.Manager.Capabilities.SupportsCustomSources)
            {
                __source = "";
                __manager = package.Manager.Name;
            }
            else if (package.Source.IsVirtualManager)
            {
                __source = "";
                __manager = package.Source.Name;
            }
            else
            {
                __source = package.Source.Name;
                __manager = package.Manager.Name;
            }
        }
        public override async void ShowOptions(object sender, RoutedEventArgs e)
        {
            await Task.Delay(0);
        }

        public override SerializableValidPackage_v1 AsSerializable()
        {
            throw new InvalidOperationException("Cannot serialize an invalid package as a bundled package. Call Serialized_Incompatible() instead ");
        }

        public override SerializableIncompatiblePackage_v1 AsSerializable_Incompatible()
        {
            SerializableIncompatiblePackage_v1 Serializable = new()
            {
                Id = Id,
                Name = Name,
                Version = version,
                Source = SourceAsString
            };
            return Serializable;
        }
    }
}
