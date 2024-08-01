using UniGetUI.Core.Logging;
using UniGetUI.Interface.Enums;
using UniGetUI.PackageEngine.Interfaces;
using UniGetUI.PackageEngine.PackageClasses;

namespace UniGetUI.PackageEngine.PackageLoader
{
    public class PackageBundlesLoader : AbstractPackageLoader
    {
        public PackageBundlesLoader(IEnumerable<IPackageManager> managers)
        : base(managers, "PACKAGE_BUNDLES", AllowMultiplePackageVersions: true, DisableReload: true)
        {
        }

#pragma warning disable
        protected override async Task<bool> IsPackageValid(IPackage package)
        {
            return true;
        }
#pragma warning restore

        protected override Task<IPackage[]> LoadPackagesFromManager(IPackageManager manager)
        {
            return Task.Run(Array.Empty<IPackage>);
        }

#pragma warning disable CS1998
        protected override async Task WhenAddingPackage(IPackage package)
        {
            if(package.GetInstalledPackage() != null)
                package.SetTag(PackageTag.AlreadyInstalled);
        }
#pragma warning restore CS1998

        public async Task AddPackagesAsync(IEnumerable<IPackage> foreign_packages)
        {
            foreach (IPackage foreign in foreign_packages)
            {
                IPackage? package = null;

                if (foreign is Package native && native is not null)
                {
                    if (native.Source.IsVirtualManager)
                    {
                        Logger.Debug($"Adding native package with id={native.Id} to bundle as an INVALID package...");
                        package = new InvalidImportedPackage(native.AsSerializable_Incompatible(), NullSource.Instance);
                    }
                    else
                    {
                        Logger.Debug($"Adding native package with id={native.Id} to bundle as a VALID package...");
                        package = new ImportedPackage(await native.AsSerializable(), native.Manager, native.Source);
                    }
                }
                else if (foreign is ImportedPackage imported && imported is not null)
                {
                    Logger.Debug($"Adding loaded imported package with id={imported.Id} to bundle...");
                    package = imported;
                }
                else if (foreign is InvalidImportedPackage invalid && invalid is not null)
                {
                    Logger.Debug($"Adding loaded incompatible package with id={invalid.Id} to bundle...");
                    package = invalid;
                }
                else
                {
                    Logger.Error($"An IPackage instance id={foreign.Id} did not match the types Package, ImportedPackage or InvalidImportedPackage. This should never be the case");
                }
                if(package is not null && !Contains(package)) AddPackage(package);
            }
            InvokePackagesChangedEvent();
        }

        public void RemoveRange(IEnumerable<IPackage> packages)
        {
            foreach(IPackage package in packages)
            {
                if (!Contains(package)) continue;
                PackageReference.Remove(HashPackage(package));
            }
            InvokePackagesChangedEvent();
        }
    }
}
