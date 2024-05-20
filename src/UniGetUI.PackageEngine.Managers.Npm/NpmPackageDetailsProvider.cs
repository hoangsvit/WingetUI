﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniGetUI.Core.IconEngine;
using UniGetUI.Core.Logging;
using UniGetUI.Core.Tools;
using UniGetUI.PackageEngine.Classes.Manager.BaseProviders;
using UniGetUI.PackageEngine.ManagerClasses.Manager;
using UniGetUI.PackageEngine.PackageClasses;

namespace UniGetUI.PackageEngine.Managers.NpmManager
{
    internal class NpmPackageDetailsProvider : BasePackageDetailsProvider<PackageManager>
    {
        public NpmPackageDetailsProvider(Npm manager) : base(manager) { }

        protected override async Task<PackageDetails> GetPackageDetails_Unsafe(Package package)
        {
            PackageDetails details = new(package);
            try
            {
                details.InstallerType = "Tarball";
                details.ManifestUrl = new Uri($"https://www.npmjs.com/package/{package.Id}");
                details.ReleaseNotesUrl = new Uri($"https://www.npmjs.com/package/{package.Id}?activeTab=versions");

                using (Process p = new())
                {
                    p.StartInfo = new ProcessStartInfo()
                    {
                        FileName = Manager.Status.ExecutablePath,
                        Arguments = Manager.Properties.ExecutableCallArgs + " info " + package.Id,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        StandardOutputEncoding = System.Text.Encoding.UTF8
                    };

                    p.Start();

                    List<string> output = new();
                    string? line;
                    while ((line = await p.StandardOutput.ReadLineAsync()) != null)
                    {
                        output.Add(line);
                    }

                    int lineNo = 0;
                    bool ReadingMaintainer = false;
                    foreach (string outLine in output)
                    {
                        try
                        {
                            lineNo++;
                            if (lineNo == 2)
                            {
                                details.License = outLine.Split("|")[1];
                            }
                            else if (lineNo == 3)
                            {
                                details.Description = outLine.Trim();
                            }
                            else if (lineNo == 4)
                            {
                                details.HomepageUrl = new Uri(outLine.Trim());
                            }
                            else if (outLine.StartsWith(".tarball"))
                            {
                                details.InstallerUrl = new Uri(outLine.Replace(".tarball: ", "").Trim());
                                details.InstallerSize = await CoreTools.GetFileSizeAsync(details.InstallerUrl);
                            }
                            else if (outLine.StartsWith(".integrity"))
                            {
                                details.InstallerHash = outLine.Replace(".integrity: sha512-", "").Replace("==", "").Trim();
                            }
                            else if (outLine.StartsWith("maintainers:"))
                            {
                                ReadingMaintainer = true;
                            }
                            else if (ReadingMaintainer)
                            {
                                ReadingMaintainer = false;
                                details.Author = outLine.Replace("-", "").Split('<')[0].Trim();
                            }
                            else if (outLine.StartsWith("published"))
                            {
                                details.Publisher = outLine.Split("by").Last().Split('<')[0].Trim();
                                details.UpdateDate = outLine.Replace("published", "").Split("by")[0].Trim();
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Warn(e);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            return details;
        }

        protected override Task<CacheableIcon?> GetPackageIcon_Unsafe(Package package)
        {
            throw new NotImplementedException();
        }

        protected override Task<Uri[]> GetPackageScreenshots_Unsafe(Package package)
        {
            throw new NotImplementedException();
        }

        protected override async Task<string[]> GetPackageVersions_Unsafe(Package package)
        {
            Process p = new()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = Manager.Status.ExecutablePath,
                    Arguments = Manager.Properties.ExecutableCallArgs + " show " + package.Id + " versions --json",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                }
            };

            string? line;
            List<string> versions = new();

            p.Start();

            while ((line = await p.StandardOutput.ReadLineAsync()) != null)
            {
                if (line.Contains("\""))
                    versions.Add(line.Trim().TrimStart('"').TrimEnd(',').TrimEnd('"'));
            }

            return versions.ToArray();
        }
    }
}
