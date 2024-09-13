using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace StarterAssets
{
    public static class PackageChecker
    {
        private static ListRequest _clientList;
        private static SearchRequest _compatibleList;
        private static List<PackageEntry> _packagesToAdd;

        private static AddRequest[] _addRequests;
        private static bool[] _installRequired;

        private static readonly string _editorFolderRoot = "Assets/StarterAssets/";
        private static readonly string _packagesToImportDataFile = "PackageImportList.txt";
        public static readonly string _packageCheckerScriptingDefine = "STARTER_ASSETS_PACKAGES_CHECKED";

        [InitializeOnLoadMethod]
        private static void CheckPackage()
        {
            // if we dont have the scripting define, it means the check has not been done
            if (!ScriptingDefineUtils.CheckScriptingDefine(_packageCheckerScriptingDefine))
            {
                _packagesToAdd = new List<PackageEntry>();
                _clientList = null;
                _compatibleList = null;

                // find the projects required package list
                var requiredPackagesListFile = Directory.GetFiles(Application.dataPath, _packagesToImportDataFile,
                    SearchOption.AllDirectories);

                // if no PackageImportList.txt exists
                if (requiredPackagesListFile.Length == 0)
                {
                    Debug.LogError(
                        "[Auto Package] : Couldn't find the packages list. Be sure there is a file called PackageImportList in your project");
                }
                else
                {
                    _packagesToAdd = new List<PackageEntry>();

                    string packageListPath = requiredPackagesListFile[0];
                    string[] content = File.ReadAllLines(packageListPath);

                    foreach (string line in content)
                    {
                        string[] split = line.Split('@');

                        // if no version is given, return null
                        PackageEntry entry = new PackageEntry
                            {_name = split[0], _version = split.Length > 1 ? split[1] : null};

                        _packagesToAdd.Add(entry);
                    }

                    // Create a file in library that is queried to see if CheckPackage() has been run already
                    ScriptingDefineUtils.SetScriptingDefine(_packageCheckerScriptingDefine);

                    // create a list of compatible packages for current engine version
                    _compatibleList = Client.SearchAll();

                    while (!_compatibleList.IsCompleted)
                    {
                        if (_compatibleList.Status == StatusCode.Failure || _compatibleList.Error != null)
                        {
                            Debug.LogError(_compatibleList.Error.message);
                            break;
                        }
                    }

                    // create a list of packages found in the engine
                    _clientList = Client.List();

                    while (!_clientList.IsCompleted)
                    {
                        if (_clientList.Status == StatusCode.Failure || _clientList.Error != null)
                        {
                            Debug.LogError(_clientList.Error.message);
                            break;
                        }
                    }

                    _addRequests = new AddRequest[_packagesToAdd.Count];
                    _installRequired = new bool[_packagesToAdd.Count];

                    // default new packages to install = false. we will mark true after validating they're required
                    for (int i = 0; i < _installRequired.Length; i++)
                    {
                        _installRequired[i] = false;
                    }

                    // build data collections compatible packages for this project, and packages within the project
                    List<PackageInfo> compatiblePackages =
                        new List<PackageInfo>();
                    List<PackageInfo> clientPackages =
                        new List<PackageInfo>();

                    foreach (var result in _compatibleList.Result)
                    {
                        compatiblePackages.Add(result);
                    }

                    foreach (var result in _clientList.Result)
                    {
                        clientPackages.Add(result);
                    }

                    // check for the latest verified package version for each package that is missing a version
                    for (int i = 0; i < _packagesToAdd.Count; i++)
                    {
                        // if a version number is not provided
                        if (_packagesToAdd[i]._version == null)
                        {
                            foreach (var package in compatiblePackages)
                            {
                                // if no latest verified version found, PackageChecker will just install latest release
                                if (_packagesToAdd[i]._name == package.name && package.versions.recommended != string.Empty)
                                {
                                    // add latest verified version number to the packagetoadd list version
                                    // so that we get the latest verified version only
                                    _packagesToAdd[i]._version = package.versions.recommended;

                                    // add to our install list
                                    _installRequired[i] = true;

                                    //Debug.Log(string.Format("Requested {0}. Latest verified compatible package found: {1}",
                                    //    packagesToAdd[i].name, packagesToAdd[i].version));
                                }
                            }
                        }

                        // we don't need to catch packages that are not installed as their latest version has been collected
                        // from the campatiblelist result
                        foreach (var package in clientPackages)
                        {
                            if (_packagesToAdd[i]._name == package.name)
                            {
                                // see what version we have installed
                                switch (CompareVersion(_packagesToAdd[i]._version, package.version))
                                {
                                    // latest verified is ahead of installed version
                                    case 1:
                                        _installRequired[i] = EditorUtility.DisplayDialog("Confirm Package Upgrade",
                                            $"The version of \"{_packagesToAdd[i]._name}\" in this project is {package.version}. The latest verified " +
                                            $"version is {_packagesToAdd[i]._version}. Would you like to upgrade it to the latest version? (Recommended)",
                                            "Yes", "No");

                                        Debug.Log(
                                            $"<b>Package version behind</b>: {package.packageId} is behind latest verified " +
                                            $"version {package.versions.recommended}. prompting user install");
                                        break;

                                    // latest verified matches installed version
                                    case 0:
                                        _installRequired[i] = false;

                                        Debug.Log(
                                            $"<b>Package version match</b>: {package.packageId} matches latest verified version " +
                                            $"{package.versions.recommended}. Skipped install");
                                        break;

                                    // latest verified is behind installed version
                                    case -1:
                                        _installRequired[i] = EditorUtility.DisplayDialog("Confirm Package Downgrade",
                                            $"The version of \"{_packagesToAdd[i]._name}\" in this project is {package.version}. The latest verified version is {_packagesToAdd[i]._version}. " +
                                            $"{package.version} is unverified. Would you like to downgrade it to the latest verified version? " +
                                            "(Recommended)", "Yes", "No");

                                        Debug.Log(
                                            $"<b>Package version ahead</b>: {package.packageId} is newer than latest verified " +
                                            $"version {package.versions.recommended}, skipped install");
                                        break;
                                }
                            }
                        }
                    }

                    // install our packages and versions
                    for (int i = 0; i < _packagesToAdd.Count; i++)
                    {
                        if (_installRequired[i])
                        {
                            _addRequests[i] = InstallSelectedPackage(_packagesToAdd[i]._name, _packagesToAdd[i]._version);
                        }
                    }

                    ReimportPackagesByKeyword();
                }
            }
        }

        private static AddRequest InstallSelectedPackage(string packageName, string packageVersion)
        {
            if (packageVersion != null)
            {
                packageName = packageName + "@" + packageVersion;
                Debug.Log($"<b>Adding package</b>: {packageName}");
            }

            AddRequest newPackage = Client.Add(packageName);

            while (!newPackage.IsCompleted)
            {
                if (newPackage.Status == StatusCode.Failure || newPackage.Error != null)
                {
                    Debug.LogError(newPackage.Error.message);
                    return null;
                }
            }

            return newPackage;
        }

        private static void ReimportPackagesByKeyword()
        {
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(_editorFolderRoot, ImportAssetOptions.ImportRecursive);
        }

        public static int CompareVersion(string latestVerifiedVersion, string projectVersion)
        {
            string[] latestVersionSplit = latestVerifiedVersion.Split('.');
            string[] projectVersionSplit = projectVersion.Split('.');
            int iteratorA = 0;
            int iteratorB = 0;

            while (iteratorA < latestVersionSplit.Length || iteratorB < projectVersionSplit.Length)
            {
                int latestVerified = 0;
                int installed = 0;

                if (iteratorA < latestVersionSplit.Length)
                {
                    latestVerified = Convert.ToInt32(latestVersionSplit[iteratorA]);
                }

                if (iteratorB < projectVersionSplit.Length)
                {
                    installed = Convert.ToInt32(projectVersionSplit[iteratorB]);
                }

                // latest verified is ahead of installed version
                if (latestVerified > installed) return 1;

                // latest verified is behind installed version
                if (latestVerified < installed) return -1;

                iteratorA++;
                iteratorB++;
            }

            // if the version is the same
            return 0;
        }

        public class PackageEntry
        {
            public string _name;
            public string _version;
        }
    }
}
