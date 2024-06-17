import os
import sys

os.chdir(os.path.join(os.path.dirname(__file__), ".."))  # move to root project

try:
    versionCode = float(input("Enter version code (X.XXX)  : "))
    versionName = str(input("Enter version name (string) : "))
    versionISS = str(input("Enter version     (X.X.X.X) : "))

    def fileReplaceLinesWith(filename: str, list: dict[str, str], encoding="utf-8"):
        with open(filename, "r+", encoding=encoding, errors="ignore") as f:
            data = ""
            for line in f.readlines():
                match = False
                for key, value in list.items():
                    if (line.startswith(key)):
                        data += f"{key}{value}"
                        match = True
                        continue
                if (not match):
                    data += line
            f.seek(0)
            f.write(data)
            f.truncate()

    fileReplaceLinesWith("src/UniGetUI.Core.Data/CoreData.cs", {
        "        public const string VersionName = ": f" \"{versionName}\"; // Do not modify this line, use file scripts/apply_versions.py\n",
        "        public const double VersionNumber = ": f" {versionCode}; // Do not modify this line, use file scripts/apply_versions.py\n",
    }, encoding="utf-8-sig")

    fileReplaceLinesWith("src/Solution.props", {
        "\t<FileVersion>": f"{versionISS}</FileVersion>\n",
        "\t<InformationalVersion>": f"{versionName}</InformationalVersion>\n",
        "\t<ApplicationVersion>": f"{versionName}</ApplicationVersion>\n",
    }, encoding="utf-8-sig")

    fileReplaceLinesWith("WingetUI.iss", {
        "#define MyAppVersion": f" \"{versionName}\"\n",
        "VersionInfoVersion=": f"{versionISS}\n",
    }, encoding="utf-8-sig")

    fileReplaceLinesWith("src/UniGetUI/app.manifest", {
        "	  version=": f" \"{versionISS}\"\n",
    }, encoding="utf-8-sig")

    print("done!")
except FileNotFoundError as e:
    print(f"Error: {e.strerror}: {e.filename}")
    os.system("pause")
except Exception as e:
    print(f"Error: {str(e)}")
    os.system("pause")
