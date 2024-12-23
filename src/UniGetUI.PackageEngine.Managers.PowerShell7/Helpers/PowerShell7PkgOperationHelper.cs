using UniGetUI.PackageEngine.Classes.Manager.BaseProviders;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.Interfaces;

namespace UniGetUI.PackageEngine.Managers.PowerShell7Manager;
internal sealed class PowerShell7PkgOperationHelper : PackagePkgOperationHelper
{
    public PowerShell7PkgOperationHelper(PowerShell7 manager) : base(manager) { }

    protected override IEnumerable<string> _getOperationParameters(IPackage package, IInstallationOptions options, OperationType operation)
    {
        List<string> parameters = [operation switch {
            OperationType.Install => Manager.Properties.InstallVerb,
            OperationType.Update => Manager.Properties.UpdateVerb,
            OperationType.Uninstall => Manager.Properties.UninstallVerb,
            _ => throw new InvalidDataException("Invalid package operation")
        }];
        parameters.AddRange(["-Name", package.Id, "-Confirm:$false", "-Force"]);

        if (options.CustomParameters is not null)
            parameters.AddRange(options.CustomParameters);

        if(operation is not OperationType.Uninstall)
        {
            if (options.PreRelease)
                parameters.Add("-AllowPrerelease");

            if (package.OverridenOptions.Scope == PackageScope.Global ||
                (package.OverridenOptions.Scope is null && options.InstallationScope == PackageScope.Global))
                parameters.AddRange(["-Scope", "AllUsers"]);
            else
                parameters.AddRange(["-Scope", "CurrentUser"]);
        }

        if(operation is OperationType.Install)
        {
            if (options.SkipHashCheck)
                parameters.Add("-SkipPublisherCheck");

            if (options.Version != "")
                parameters.AddRange(["-RequiredVersion", options.Version]);
        }

        return parameters;
    }

    protected override OperationVeredict _getOperationResult(
        IPackage package,
        OperationType operation,
        IEnumerable<string> processOutput,
        int returnCode)
    {
        string output_string = string.Join("\n", processOutput);

        if (!package.OverridenOptions.RunAsAdministrator != true && output_string.Contains("AdminPrivilegesAreRequired"))
        {
            package.OverridenOptions.RunAsAdministrator = true;
            return OperationVeredict.AutoRetry;
        }

        return returnCode == 0 ? OperationVeredict.Succeeded : OperationVeredict.Failed;
    }
}
