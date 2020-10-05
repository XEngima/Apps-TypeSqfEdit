using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TypeSqf.Model;
using TypeSqf.WebService;

namespace TypeSqf.Edit.Service
{
    public partial class CPackService
    {
        private static bool FileIsLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

        private static bool HandleInitFiles(string projectRootDirectory, CPack metaPack, CPack installedPack, CrudAction action, IProgress<string> progress, bool isSimulation)
        {
            string endingComment = "";
            var installedInitFiles = new List<StringCouple>()
                            {
                                new StringCouple() {Key = "init.sqf", Value = installedPack.InitLine != null ? installedPack.InitLine : ""},
                                new StringCouple() {Key = "initplayerlocal.sqf", Value = installedPack.InitPlayerLocalLine != null ? installedPack.InitPlayerLocalLine : ""},
                                new StringCouple() {Key = "initplayerserver.sqf", Value = installedPack.InitPlayerServerLine != null ? installedPack.InitPlayerServerLine : ""},
                                new StringCouple() {Key = "initserver.sqf", Value = installedPack.InitServerLine != null ? installedPack.InitServerLine : ""},
                                new StringCouple() {Key = "description.ext", Value = installedPack.DescriptionExtLine != null ? installedPack.DescriptionExtLine : ""},
                            };

            List<StringCouple> metaInitFiles = new List<StringCouple>();
            if (action != CrudAction.Delete)
            {
                endingComment = " // Added by " + metaPack.Name;
                metaInitFiles = new List<StringCouple>()
                            {
                                new StringCouple() {Key = "init.sqf", Value = metaPack.InitLine != null ? metaPack.InitLine : ""},
                                new StringCouple() {Key = "initplayerlocal.sqf", Value = metaPack.InitPlayerLocalLine != null ? metaPack.InitPlayerLocalLine : ""},
                                new StringCouple() {Key = "initplayerserver.sqf", Value = metaPack.InitPlayerServerLine != null ? metaPack.InitPlayerServerLine : ""},
                                new StringCouple() {Key = "initserver.sqf", Value = metaPack.InitServerLine != null ? metaPack.InitServerLine : ""},
                                new StringCouple() {Key = "description.ext", Value = metaPack.DescriptionExtLine != null ? metaPack.DescriptionExtLine : ""},
                            };
            }

            if (isSimulation)
            {
                // Kolla om det går att uppdatera init-filerna
                foreach (var installedInitFile in installedInitFiles)
                {
                    var metaInitFile = metaInitFiles.FirstOrDefault(f => f.Key == installedInitFile.Key);

                    // Kolla om filen ska tas bort ur, ändras i, läggas till i eller ingenting
                    CrudAction lineAction = GetInitFileLineAction(action, installedInitFile, metaInitFile);

                    if (lineAction != CrudAction.None)
                    {
                        string initFileName = Path.Combine(projectRootDirectory, installedInitFile.Key);

                        if (File.Exists(initFileName))
                        {
                            if (FileIsLocked(new FileInfo(initFileName))) {
                                progress.Report(string.Format("File '{0}' is locked by another process. No files have changed. Aborting...", initFileName));
                                return false;
                            }
                        }
                    }
                }
            }
            else
            {
                // Uppdatera init-filerna
                foreach (var installedInitFile in installedInitFiles)
                {
                    var metaInitFile = metaInitFiles.FirstOrDefault(f => f.Key == installedInitFile.Key);

                    // Kolla om filen ska tas bort ur, ändras i, läggas till i eller ingenting
                    CrudAction lineAction = GetInitFileLineAction(action, installedInitFile, metaInitFile);

                    if (lineAction != CrudAction.None)
                    {
                        string initFileName = Path.Combine(projectRootDirectory, installedInitFile.Key);
                        string fileContent = "";

                        if (File.Exists(initFileName))
                        {
                            fileContent = File.ReadAllText(initFileName);
                        }

                        if (lineAction == CrudAction.Update || lineAction == CrudAction.Delete)
                        {
                            // Hitta raden och ta bort den
                            string lineToDelete = installedInitFile.Value + endingComment;
                            bool lineExists = fileContent.Contains(lineToDelete);

                            if (!lineExists)
                            {
                                lineToDelete = installedInitFile.Value;
                                lineExists = fileContent.Contains(lineToDelete);
                            }

                            if (lineExists)
                            {
                                fileContent = fileContent.Replace(lineToDelete, "// " + lineToDelete);
                            }
                        }

                        if (lineAction == CrudAction.Create || lineAction == CrudAction.Update)
                        {
                            string lineToAdd = metaInitFile.Value;
                            fileContent = lineToAdd + endingComment + "\r\n" + fileContent;
                        }

                        File.WriteAllText(initFileName, fileContent);
                    }
                }

                if (action == CrudAction.Delete)
                {
                    installedPack.InitLine = "";
                    installedPack.InitPlayerLocalLine = "";
                    installedPack.InitPlayerServerLine = "";
                    installedPack.InitServerLine = "";
                    installedPack.DescriptionExtLine = "";
                }
                else
                {
                    installedPack.InitLine = metaPack.InitLine;
                    installedPack.InitPlayerLocalLine = metaPack.InitPlayerLocalLine;
                    installedPack.InitPlayerServerLine = metaPack.InitPlayerServerLine;
                    installedPack.InitServerLine = metaPack.InitServerLine;
                    installedPack.DescriptionExtLine = metaPack.DescriptionExtLine;
                }
            }

            return true;
        }

        private static CrudAction GetInitFileLineAction(CrudAction action, StringCouple installedInitFile, StringCouple metaInitFile)
        {
            CrudAction lineAction = CrudAction.None;

            if (action == CrudAction.Delete)
            {
                if (installedInitFile.Value != "")
                {
                    lineAction = CrudAction.Delete;
                }
            }
            else if (action == CrudAction.Create)
            {
                if (metaInitFile.Value != "")
                {
                    lineAction = CrudAction.Create;
                }
            }
            else if (action == CrudAction.Update)
            {
                if (installedInitFile.Value == metaInitFile.Value)
                {
                }
                else if (installedInitFile.Value != "" && metaInitFile.Value == "")
                {
                    lineAction = CrudAction.Delete;
                }
                else if (installedInitFile.Value == "" && metaInitFile.Value != "")
                {
                    lineAction = CrudAction.Create;
                }
                else
                {
                    lineAction = CrudAction.Update;
                }
            }

            return lineAction;
        }

        private static void DeleteFolderIfEmpty(string folderPathName, string projectRootDirectory)
        {
            if (folderPathName.ToLower() != projectRootDirectory.ToLower())
            {
                folderPathName = folderPathName.Replace("/", "\\");

                string relativeFolderPathName = folderPathName.Substring(projectRootDirectory.Length + 1);

                string[] folders = relativeFolderPathName.Split("\\".ToCharArray());

                for (int noOfFolders = folders.Length; noOfFolders > 0; noOfFolders--)
                {
                    var sbFolderName = new StringBuilder();
                    sbFolderName.Append(projectRootDirectory);

                    for (int i = 0; i < noOfFolders; i++)
                    {
                        sbFolderName.Append("\\");
                        sbFolderName.Append(folders[i]);
                    }

                    if (Directory.Exists(sbFolderName.ToString()) && !Directory.EnumerateFileSystemEntries(sbFolderName.ToString()).Any())
                    {
                        Directory.Delete(sbFolderName.ToString());
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        public static CPackSettings GetLocalCPackSettings(string projectRootDirectory)
        {
            CPackSettings localCPackSettings = null;
            string cPackSettingsFileName = Path.Combine(projectRootDirectory, "CPack.Config");

            if (File.Exists(cPackSettingsFileName))
            {
                XmlSerializer reader = new XmlSerializer(typeof(CPackSettings));
                using (StringReader sXml = new StringReader(File.ReadAllText(cPackSettingsFileName)))
                {
                    localCPackSettings = (CPackSettings)reader.Deserialize(sXml);
                }
            }
            else
            {
                localCPackSettings = new CPackSettings();
            }

            return localCPackSettings;
        }

        /// <summary>
        /// Hämtar inställningar för ett CPack från TypeSqf-sidan.
        /// </summary>
        /// <param name="packageName">The full package name.</param>
        /// <param name="allowBeta">Wether a beta version i ok to get or not.</param>
        /// <param name="targetVersion">Optional. The target version to get.</param>
        /// <returns>CPack settings for the package.</returns>
        private static CPackSettings GetMetaCPackSettingsFromInternet(string packageName, bool allowBeta, AppVersion targetVersion = null)
        {
            var webClient = new WebClient();
            string metaUri = CurrentApplication.TypeSqfDomain + "/Download/CPackMeta/" + packageName + "?beta=" + (allowBeta ? "true" : "false") + (targetVersion != null ? "&version=" + targetVersion.ToString().Replace(".", "-") : "");

            string metaData = webClient.DownloadString(metaUri);
            XmlSerializer reader = new XmlSerializer(typeof(CPackSettings));
            CPackSettings metaCPackSettings;
            using (StringReader sXml = new StringReader(metaData))
            {
                metaCPackSettings = (CPackSettings)reader.Deserialize(sXml);
            }

            return metaCPackSettings;
        }

        private static bool DownloadPackageFile(string packageName, bool allowBeta, AppVersion version, out string fileName)
        {
            // First check if file is already downloaded
            string packageFileName = CurrentWebService.ToDownloadPackageFileName(packageName, version);
            if (File.Exists(Path.Combine(CurrentApplication.PackageDownloadDirectory, packageFileName)))
            {
                fileName = packageFileName;
                return true;
            }

            WebClient webClient = new WebClient();
            fileName = null;

            try
            {
                string remoteUri = CurrentApplication.TypeSqfDomain + "/Download/CPack/" + packageName + "?beta=" + (allowBeta ? "true" : "false") + (version != null ? "&version=" + version.ToString().Replace(".", "-") : "");
                byte[] data = webClient.DownloadData(remoteUri);

                // Extract the filename from the Content-Disposition header
                if (string.IsNullOrEmpty(webClient.ResponseHeaders["Content-Disposition"]))
                {
                    return false;
                }

                fileName =
                    webClient.ResponseHeaders["Content-Disposition"].Substring(
                        webClient.ResponseHeaders["Content-Disposition"].IndexOf("filename=") + 9).Replace("\"", "");

                Directory.CreateDirectory(CurrentApplication.PackageDownloadDirectory);
                File.WriteAllBytes(Path.Combine(CurrentApplication.PackageDownloadDirectory, fileName), data);
            }
            catch
            {
                return false;
            }
            return true;
        }

        private static string ToMd5String(string fileName)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(File.ReadAllBytes(fileName));
                return Convert.ToBase64String(hash);
            }
        }

        private static void WriteSettingsFileToDisk(CPackSettings localCPackSettings, string projectRootDirectory)
        {
            using (var writer = new StreamWriter(Path.Combine(projectRootDirectory, "CPack.Config")))
            {
                var serializer = new XmlSerializer(typeof(CPackSettings));
                serializer.Serialize(writer, localCPackSettings);
                writer.Flush();
            }
        }

        private static bool InstallPackage(string packageName, string projectRootDirectory, AppVersion targetVersion, bool updateDependencies, bool overwrite, bool allowBeta, IProgress<string> progress)
        {
            try
            {
                // Ladda in den lokala CPack.Config-filen (eller skapa en ny)
                var localCPackSettings = GetLocalCPackSettings(projectRootDirectory);

                // Kör installeraren

                CPackInstallationResult installationResult = CPackInstallationResult.Failure;
                CPackInstallationResult simulationResult = InstallPackage(packageName, localCPackSettings,
                    projectRootDirectory, targetVersion, updateDependencies, overwrite, allowBeta, progress, true);

                if (simulationResult == CPackInstallationResult.Success)
                {
                    // Ladda in den lokala CPack.Config-filen (eller skapa en ny)
                    localCPackSettings = GetLocalCPackSettings(projectRootDirectory);

                    installationResult = InstallPackage(packageName, localCPackSettings, projectRootDirectory, targetVersion, updateDependencies, overwrite, allowBeta, progress,
                        false);
                }

                if (installationResult == CPackInstallationResult.Success)
                {
                    // Skriv den lokala inställningsfilen
                    WriteSettingsFileToDisk(localCPackSettings, projectRootDirectory);
                }

                return installationResult == CPackInstallationResult.Success;
            }
            catch (WebException)
            {
                progress.Report("Server was not found.");
                return false;
            }
#if !DEBUG
            catch (Exception ex)
            {
                progress.Report(ex.Message);
                return false;
            }
#endif
        }

        private static CPackInstallationResult InstallPackage(string packageName, CPackSettings localCPackSettings, string projectRootDirectory, AppVersion targetVersion, bool updateDependencies, bool overwrite, bool allowBeta, IProgress<string> progress, bool isSimulation, AppVersion requiredVersion = null)
        {
            // Hämta informationen för ett eventuellt redan installerat CPack.
            CPack installedPack = localCPackSettings.CPacks.FirstOrDefault(p => String.Equals(p.Name, packageName, StringComparison.InvariantCultureIgnoreCase));

            if (installedPack == null)
            {
                // Om paketet inte finns, skapa ett nytt och ge det rätt namn
                installedPack = new CPack()
                {
                    Name = packageName
                };
            }
            else
            {
                // Om paketet finns installerat redan, kolla om det behöver uppdateras, och ge användaren
                // information om hur man gör det.
                packageName = installedPack.Name;

                if (installedPack.Version < requiredVersion)
                {
                    progress.Report(packageName + " " + installedPack.Version + ": Package needs to be updated. Update the package first, or use the flag -updatedep to update all dependencies automatically. Aborting...");
                    return CPackInstallationResult.Failure;
                }

                if (isSimulation)
                {
                    if (targetVersion != null)
                    {
                        progress.Report(packageName + " " + installedPack.Version + ": Package is already installed. To install an earlier version, first remove the current package.");
                    }
                    else
                    {
                        progress.Report(packageName + " " + installedPack.Version + ": Package is already installed" + (requiredVersion != null ? " (required version is " + requiredVersion + ")" : "") + ".");
                    }
                }

                return CPackInstallationResult.NotNeeded;
            }

            // Hämta metadata för paketet som ska installeras.
            var metaCPackSettings = GetMetaCPackSettingsFromInternet(packageName, allowBeta, targetVersion);
            if (metaCPackSettings.Version != CurrentWebService.Version)
            {
                progress.Report("The online CPack service has changed. Please update the TypeSqf editor and try again.");
                return CPackInstallationResult.Failure;
            }

            CPackSettings latestMetaCPackSettings = null;
            if (targetVersion != null)
            {
                latestMetaCPackSettings = GetMetaCPackSettingsFromInternet(packageName, allowBeta);
            }

            if (metaCPackSettings.CPacks.Length == 0)
            {
                if (targetVersion != null)
                {
                    progress.Report(packageName + " " + targetVersion + ": No such package was found.");
                }
                else
                {
                    progress.Report(packageName + ": No such package was found.");
                }

                return CPackInstallationResult.Failure;
            }

            // Add meta CPack settings to local CPack settings
            CPack metaPack = metaCPackSettings.CPacks[0]; // Alltid exakt en
            installedPack.Name = metaPack.Name;
            packageName = metaPack.Name;

            installedPack.Major = metaPack.Major;
            installedPack.Minor = metaPack.Minor;
            installedPack.Build = metaPack.Build;

            WebClient webClient = new WebClient();

            // Kolla efter dependencies, och börja med att installera dem om de inte finns
            installedPack.DependenciesList.Clear();

            foreach (CPackDependency metaDependency in metaPack.Dependencies)
            {
                // Uppdatera beroendelistan lokalt
                installedPack.DependenciesList.Add(metaDependency);

                // Om dependenciet redan är installerat, uppdatera det om det behövs.
                CPackInstallationResult result = CPackInstallationResult.NotNeeded;
                CPack localDependencyPack =
                    localCPackSettings.CPacks.FirstOrDefault(
                        p => String.Equals(p.Name, metaDependency.Name, StringComparison.InvariantCultureIgnoreCase));

                if (localDependencyPack != null)
                {

                    if (localDependencyPack.Version < metaDependency.Version)
                    {
                        if (!updateDependencies)
                        {
                            progress.Report(metaDependency.Name + " " + localDependencyPack.Version + ": Package needs to be updated. Use flag -updatedep to update dependencies automatically. Aborting...");
                            return CPackInstallationResult.Failure;
                        }

                        result = UpdatePackage(metaDependency.Name, localCPackSettings, projectRootDirectory,
                            metaDependency.Version, updateDependencies, overwrite, false, progress,
                            isSimulation, metaDependency.Version);
                    }
                    else if (localDependencyPack.Version > metaDependency.Version)
                    {
                        if (isSimulation)
                        {
                            progress.Report(metaDependency.Name + " " + localDependencyPack.Version + ": The required version is " + metaDependency.Version + ", but a later version is already installed. No action have been taken.");
                        }
                    }
                }
                else
                {
                    result = InstallPackage(metaDependency.Name, localCPackSettings, projectRootDirectory,
                        metaDependency.Version, updateDependencies, overwrite, false, progress, isSimulation,
                        metaDependency.Version);
                }

                if (result == CPackInstallationResult.Failure)
                {
                    return result;
                }
            }

            // Alla dependencies installerade. Installera nu det här paketet.

            // Ladda ner filen
            string fileName;
            AppVersion downloadVersion = targetVersion != null ? targetVersion : metaPack.Version;
            if (!DownloadPackageFile(packageName, allowBeta, downloadVersion, out fileName))
            {
                progress.Report(packageName + " " + metaPack.Version + ": Failed to download file. Aborting...");
                return CPackInstallationResult.Failure;
            }

            using (ZipArchive zip = ZipFile.Open(Path.Combine(CurrentApplication.PackageDownloadDirectory, fileName), ZipArchiveMode.Read))
            {
                bool filesCollide = false;

                // Börja med att kolla om någon av filerna finns. I sådana fall kan inte paketet packas upp.
                // Filerna kolliderar om nya filer som packas upp inte ersätter gamla versioner
                if (isSimulation)
                {
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        if (!entry.FullName.EndsWith("/")) // if it's a file and not a folder
                        {
                            bool fileAlreadyInstalled = false;

                            if (installedPack != null)
                            {
                                CPackFile installedFile = installedPack.Files.FirstOrDefault();
                                if (installedFile != null)
                                {
                                    fileAlreadyInstalled = installedFile.Name == entry.FullName;
                                }
                            }

                            if (!fileAlreadyInstalled && File.Exists(Path.Combine(projectRootDirectory, entry.FullName)))
                            {
                                progress.Report(packageName + " " + metaPack.Version + ": File already exists (" + entry.FullName + ").");
                                filesCollide = true;
                                break;
                            }
                        }
                    }

                    if (filesCollide && !overwrite)
                    {
                        progress.Report("Existing files will not be overwritten. Use flag -overwrite to explicitly force files to be overwritten. Aborting...");
                        return CPackInstallationResult.Failure;
                    }
                }

                // Do the actual decompression of files

                if (isSimulation)
                {
                    if (!HandleInitFiles(projectRootDirectory, metaPack, installedPack, CrudAction.Create, progress, true))
                    {
                        return CPackInstallationResult.Failure;
                    }
                }

                installedPack.FilesList.Clear();

                if (!isSimulation)
                {
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRootDirectory, entry.FullName)));
                        if (!entry.FullName.EndsWith("/"))
                        {
                            string filePath = Path.Combine(projectRootDirectory, entry.FullName);
                            entry.ExtractToFile(filePath, overwrite);

                            installedPack.FilesList.Add(new CPackFile()
                            {
                                Name = entry.FullName,
                                CheckSum = ToMd5String(filePath)
                            });
                        }
                    }

                    HandleInitFiles(projectRootDirectory, metaPack, installedPack, CrudAction.Create, progress, false);

                    if (overwrite)
                    {
                        progress.Report("Files overwritten.");
                    }

                    progress.Report(packageName + " " + metaPack.Version + ": Package installed successfully.");

                    if (latestMetaCPackSettings != null && latestMetaCPackSettings.CPacks[0].Version > metaCPackSettings.CPacks[0].Version)
                    {
                        progress.Report(packageName + " " + metaPack.Version + ": A new version (" + latestMetaCPackSettings.CPacks[0].Version + ") is available.");
                    }
                }

                // Ta bort den nedladdade filen
                try
                {
                    //File.Delete(fileName);
                }
                catch
                { }

                //var alreadyExistingPack = localCPackSettings.CPacksList.FirstOrDefault(p => p.Name.ToLower() == installedPack.Name.ToLower() && p.Version == installedPack.Version);
                //if (alreadyExistingPack == null)
                //{
                localCPackSettings.CPacksList.Add(installedPack);
                //}

                return CPackInstallationResult.Success;
            }
        }

        private static CPackInstallationResult UpdatePackage(string packageName, CPackSettings localCPackSettings, string projectRootDirectory, AppVersion targetVersion, bool updateDependencies, bool overwrite, bool allowBeta, IProgress<string> progress, bool isSimulation, AppVersion requiredVersion = null)
        {
            CPack installedPack = localCPackSettings.CPacks.FirstOrDefault(p => String.Equals(p.Name, packageName, StringComparison.InvariantCultureIgnoreCase));

            // If package is not installed, exit with failure.
            if (installedPack == null)
            {
                progress.Report(packageName + ": Package is not installed.");
                return CPackInstallationResult.Failure;
            }

            packageName = installedPack.Name;

            if (isSimulation)
            {
                if (installedPack.Version < requiredVersion && !updateDependencies)
                {
                    progress.Report(packageName + " " + installedPack.Version + ": Package needs to be updated. Update the package first, or use the flag -updatedep to update dependencies automatically. Aborting...");
                    return CPackInstallationResult.Failure;
                }

                if (targetVersion < installedPack.Version)
                {
                    progress.Report(packageName + " " + installedPack.Version + ": Package cannot be downgraded. To install an earlier version, first remove the current package.");
                    return CPackInstallationResult.Failure;
                }
                else if (targetVersion == installedPack.Version)
                {
                    progress.Report(packageName + " " + installedPack.Version + ": Package is already installed" + (requiredVersion != null ? " (required version is " + requiredVersion + ")" : "") + ".");
                    return CPackInstallationResult.NotNeeded;
                }
            }

            // Check for package of newer version
            var metaCPackSettings = GetMetaCPackSettingsFromInternet(packageName, allowBeta, targetVersion);
            if (metaCPackSettings.Version != CurrentWebService.Version)
            {
                progress.Report("The online CPack service has changed. Please update the TypeSqf editor and try again.");
                return CPackInstallationResult.Failure;
            }

            // Om inte paketet ens finns är det något kajko, men tala om det för användaren iaf...
            if (metaCPackSettings.CPacks.Length == 0)
            {
                progress.Report(packageName + ": No such package was found.");
                return CPackInstallationResult.Failure;
            }

            // Add meta CPack settings to local CPack settings
            CPack metaPack = metaCPackSettings.CPacks[0]; // Alltid exakt en
            packageName = metaPack.Name;

            // Kolla att versionen som vi hämtade är nyare
            if (metaPack.Version <= installedPack.Version)
            {
                progress.Report(packageName + " " + installedPack.Version + ": Package is already of latest version.");
                return CPackInstallationResult.NotNeeded;
            }

            //CPackVersion installingVersion = new CPackVersion(metaPack.Major, metaPack.Minor, metaPack.Build);

            // Kolla efter dependencies, och börja med att installera dem om de inte finns
            installedPack.DependenciesList.Clear();

            foreach (CPackDependency metaDependency in metaPack.Dependencies)
            {
                // Uppdatera beroendelistan lokalt
                installedPack.DependenciesList.Add(metaDependency);

                // Om dependenciet redan är installerat, uppdatera det.
                CPackInstallationResult result = CPackInstallationResult.NotNeeded;
                CPack localDependencyPack =
                    localCPackSettings.CPacks.FirstOrDefault(
                        p => String.Equals(p.Name, metaDependency.Name, StringComparison.InvariantCultureIgnoreCase));

                if (localDependencyPack != null)
                {
                    //Här är jag, CommonLib 3.0 beta installeras inte när man installerar Traffic Beta.

                    if (localDependencyPack.Version < metaDependency.Version)
                    {
                        if (!updateDependencies)
                        {
                            progress.Report(metaDependency.Name + " " + localDependencyPack.Version + ": Package needs to be updated. Use flag -updatedep to update dependencies automatically. Aborting...");
                            return CPackInstallationResult.Failure;
                        }

                        result = UpdatePackage(metaDependency.Name, localCPackSettings, projectRootDirectory,
                            metaDependency.Version, updateDependencies, overwrite, false, progress,
                            isSimulation, metaDependency.Version);
                    }
                }
                else
                {
                    result = InstallPackage(metaDependency.Name, localCPackSettings, projectRootDirectory,
                        metaDependency.Version, updateDependencies, overwrite, false, progress, isSimulation,
                        metaDependency.Version);
                }

                if (result == CPackInstallationResult.Failure)
                {
                    return result;
                }
            }

            // Alla dependencies installerade. Installera nu det här paketet.

            // Ladda ner filen
            string fileName;
            if (!DownloadPackageFile(packageName, allowBeta, metaPack.Version, out fileName))
            {
                progress.Report(packageName + " " + metaPack.Version + ": Failed to download file. Aborting...");
                return CPackInstallationResult.Failure;
            }

            using (ZipArchive zip = ZipFile.Open(Path.Combine(CurrentApplication.PackageDownloadDirectory, fileName), ZipArchiveMode.Read))
            {
                bool filesCollide = false;
                List<CPackFile> filesToIgnore = new List<CPackFile>();

                // Börja med att kolla om någon av filerna finns. I sådana fall kan inte paketet packas upp.
                // Filerna kolliderar om nya filer som packas upp inte ersätter gamla versioner
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    if (!entry.FullName.EndsWith("/")) // om det är en fil (och inte en mapp)
                    {
                        string filePath = Path.Combine(projectRootDirectory, entry.FullName);
                        bool installedInLastVersion = false;

                        CPackFile installedFile = installedPack.Files.FirstOrDefault(f => f.Name == entry.FullName);
                        if (installedFile != null)
                        {
                            installedInLastVersion = installedFile.Name == entry.FullName;
                        }

                        if (!installedInLastVersion)
                        {
                            if (File.Exists(filePath))
                            {
                                progress.Report(packageName + " " + metaPack.Version + ": File already exists (" +
                                                entry.FullName + ").");
                                filesCollide = true;
                            }
                        }
                        else
                        {
                            // Om filen är installerad i en tidigare version, kolla om den är en fil
                            // som användaren ändrat, men inte pakettillverkaren

                            if (installedFile != null)
                            {
                                string tempFileName = Path.GetTempFileName();
                                entry.ExtractToFile(tempFileName, true);
                                string checkSum = ToMd5String(tempFileName);
                                File.Delete(tempFileName);
                                if (checkSum == installedFile.CheckSum)
                                {
                                    filesToIgnore.Add(new CPackFile()
                                    {
                                        Name = entry.FullName,
                                        CheckSum = checkSum
                                    });
                                }
                            }
                        }
                    }
                }

                if (filesCollide && !overwrite)
                {
                    progress.Report("Files not belonging to package will not be overwritten. Use flag -overwrite to explicitly force files to be overwritten. Aborting...");
                    return CPackInstallationResult.Failure;
                }

                // Check if user have changed any files

                bool filesHaveChanged = false;
                foreach (var file in installedPack.Files)
                {
                    string filePath = Path.Combine(projectRootDirectory, file.Name);
                    if (File.Exists(filePath) && file.CheckSum != ToMd5String(filePath) && !filesToIgnore.Any(f => f.Name == file.Name))
                    {
                        filesHaveChanged = true;
                        if (isSimulation)
                        {
                            progress.Report(packageName + " " + metaPack.Version + ": File has changed (" + file.Name + ")");
                        }
                    }
                }

                if (filesHaveChanged && !overwrite)
                {
                    progress.Report("User changed files will not be overwritten. User flag -overwrite to explicitly force files to be overwritten. Aborting...");
                    return CPackInstallationResult.Failure;
                }

                if (isSimulation)
                {
                    if (!HandleInitFiles(projectRootDirectory, metaPack, installedPack, CrudAction.Create, progress, true))
                    {
                        return CPackInstallationResult.Failure;
                    }
                }

                if (!isSimulation)
                {

                    // Delete files from previous versions

                    foreach (var file in installedPack.Files)
                    {
                        if (!filesToIgnore.Any(f => f.Name == file.Name))
                        {
                            string filePath = Path.Combine(projectRootDirectory, file.Name);
                            if (File.Exists(filePath))
                            {
                                File.SetAttributes(filePath, FileAttributes.Normal);
                                File.Delete(filePath);
                                DeleteFolderIfEmpty(Path.GetDirectoryName(filePath), projectRootDirectory);
                            }
                        }
                    }

                    installedPack.FilesList.Clear();

                    // Do the actual decompression of files

                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRootDirectory, entry.FullName)));
                        if (!entry.FullName.EndsWith("/"))
                        {
                            string filePath = Path.Combine(projectRootDirectory, entry.FullName);
                            string checkSum;

                            CPackFile ignoreFile = filesToIgnore.FirstOrDefault(f => f.Name == entry.FullName);
                            if (ignoreFile != null)
                            {
                                checkSum = ignoreFile.CheckSum;
                            }
                            else
                            {
                                entry.ExtractToFile(Path.Combine(projectRootDirectory, entry.FullName), overwrite);
                                checkSum = ToMd5String(filePath);
                            }

                            installedPack.FilesList.Add(new CPackFile()
                            {
                                Name = entry.FullName,
                                CheckSum = checkSum
                            });
                        }
                    }

                    HandleInitFiles(projectRootDirectory, metaPack, installedPack, CrudAction.Update, progress, false);

                    if (overwrite)
                    {
                        progress.Report("Files overwritten.");
                    }

                    progress.Report(packageName + " " + metaPack.Version + ": Package updated successfully.");
                }

                // Ta bort den nedladdade filen
                try
                {
                    //File.Delete(fileName);
                }
                catch { }

                //if (!isSimulation) {
                installedPack.Major = metaPack.Major;
                installedPack.Minor = metaPack.Minor;
                installedPack.Build = metaPack.Build;
                //}

                return CPackInstallationResult.Success;
            }
        }

        private static bool UpdatePackage(string packageName, string projectRootDirectory, AppVersion targetVersion, bool updateDependencies, bool overwrite, bool allowBeta, IProgress<string> progress)
        {
            try
            {
                // Ladda in den lokala CPack.Config-filen (eller skapa en ny)
                var localCPackSettings = GetLocalCPackSettings(projectRootDirectory);

                // Kör uppdateraren

                CPackInstallationResult installationResult = CPackInstallationResult.Failure;
                CPackInstallationResult simulationResult = UpdatePackage(packageName, localCPackSettings,
                    projectRootDirectory, targetVersion, updateDependencies, overwrite, allowBeta, progress, true);

                if (simulationResult == CPackInstallationResult.Success)
                {
                    // Ladda in den lokala CPack.Config-filen (eller skapa en ny)
                    localCPackSettings = GetLocalCPackSettings(projectRootDirectory);

                    installationResult = UpdatePackage(packageName, localCPackSettings, projectRootDirectory,
                        targetVersion, updateDependencies, overwrite, allowBeta, progress,
                        false);
                }

                if (installationResult == CPackInstallationResult.Success)
                {
                    // Skriv den lokala inställningsfilen
                    WriteSettingsFileToDisk(localCPackSettings, projectRootDirectory);
                }

                return installationResult == CPackInstallationResult.Success;
            }
            catch (WebException)
            {
                progress.Report("Server was not found.");
                return false;
            }
#if !DEBUG
            catch (Exception ex)
            {
                progress.Report(ex.Message);
                return false;
            }
#endif
        }

        private static bool RemovePackage(string packageName, string projectRootDirectory, bool removeDependents, CPackSettings localPackSettings, bool isSimulation, List<string> removedPackages, bool overwrite, IProgress<string> progress)
        {
            CPack localPack = localPackSettings.CPacks.FirstOrDefault(p => String.Equals(p.Name, packageName, StringComparison.CurrentCultureIgnoreCase));

            // If package is not installed, exit with failure.
            if (localPack == null)
            {
                progress.Report(packageName + ": Package is not installed.");
                return false;
            }
            else
            {
                packageName = localPack.Name;
            }

            // Kolla om någon annat installerat paket har beroenden till detta paket
            List<CPack> dependentPacks = new List<CPack>();
            foreach (var pack in localPackSettings.CPacks)
            {
                if (pack.Dependencies.Any(d => String.Equals(d.Name, packageName, StringComparison.CurrentCultureIgnoreCase)))
                {
                    if (isSimulation)
                    {
                        progress.Report(packageName + " " + localPack.Version + ": Package " + pack.Name + " has dependency to this package.");
                    }

                    dependentPacks.Add(pack);
                }
            }

            if (dependentPacks.Count > 0)
            {
                if (!removeDependents)
                {
                    progress.Report("Package could not be removed since it has dependants. Use flag -removedep to automatically remove all dependants.");
                    return false;
                }

                foreach (CPack dependant in dependentPacks)
                {
                    if (removedPackages.Contains(dependant.Name))
                    {
                        continue;
                    }

                    if (RemovePackage(dependant.Name, projectRootDirectory, removeDependents, localPackSettings, isSimulation, removedPackages, overwrite, progress))
                    {
                        removedPackages.Add(dependant.Name);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            // Check if files can be deleted (may be changed)

            bool filesHaveChanged = false;
            foreach (var file in localPack.Files)
            {
                string filePath = Path.Combine(projectRootDirectory, file.Name);
                if (File.Exists(filePath))
                {
                    if (file.CheckSum != ToMd5String(filePath))
                    {
                        filesHaveChanged = true;
                        if (isSimulation)
                        {
                            progress.Report(packageName + " " + localPack.Version + ": File has changed (" + file.Name + ")");
                        }
                    }
                }
            }

            if (filesHaveChanged && !overwrite)
            {
                progress.Report("User changed files will not be removed. User flag -overwrite to explicitly force files to be removed. Aborting...");
                return false;
            }

            if (isSimulation)
            {
                if (!HandleInitFiles(projectRootDirectory, null, localPack, CrudAction.Delete, progress, true))
                {
                    return false;
                }
            }

            if (!isSimulation)
            {
                List<CPackFile> filesToRemove = new List<CPackFile>();

                // Remove files
                foreach (var file in localPack.Files)
                {
                    string filePath = Path.Combine(projectRootDirectory, file.Name);
                    if (File.Exists(filePath))
                    {
                        File.SetAttributes(filePath, FileAttributes.Normal);
                        File.Delete(filePath);
                    }

                    // Also remove the compiled file
                    if (filePath.ToLower().EndsWith(".sqx"))
                    {
                        string compiledFilePath = filePath + ".sqf";
                        if (File.Exists(compiledFilePath))
                        {
                            File.SetAttributes(compiledFilePath, FileAttributes.Normal);
                            File.Delete(compiledFilePath);
                        }
                    }

                    filesToRemove.Add(file);

                    string directoryPath = Path.GetDirectoryName(filePath);
                    if (Directory.Exists(directoryPath) && !Directory.EnumerateFileSystemEntries(directoryPath).Any())
                    {
                        //Directory.Delete(directoryPath);
                        DeleteFolderIfEmpty(directoryPath, projectRootDirectory);
                    }
                }

                foreach (var file in filesToRemove)
                {
                    localPack.FilesList.Remove(file);
                }

                HandleInitFiles(projectRootDirectory, null, localPack, CrudAction.Delete, progress, false);

                progress.Report(packageName + " " + localPack.Version + ": Package was removed.");
                localPackSettings.CPacksList.Remove(localPack);
            }

            return true;
        }

        private static bool RemovePackage(string packageName, string projectRootDirectory, bool removeDependents, bool overwrite, IProgress<string> progress)
        {
            try
            {
                // Ladda in den lokala CPack.Config-filen (eller skapa en ny)
                CPackSettings localCPackSettings = GetLocalCPackSettings(projectRootDirectory);

                // Ta bort paketet.
                var removedPackages = new List<string>();
                bool simulationSuccessful = RemovePackage(packageName, projectRootDirectory, removeDependents, localCPackSettings, true, removedPackages, overwrite, progress);

                removedPackages = new List<string>();
                bool removeSuccess = false;
                if (simulationSuccessful)
                {
                    removeSuccess = RemovePackage(packageName, projectRootDirectory, removeDependents, localCPackSettings, false, removedPackages, overwrite, progress);
                }

                if (removeSuccess)
                {

                    // Skriv den lokala inställningsfilen
                    WriteSettingsFileToDisk(localCPackSettings, projectRootDirectory);
                }

                return removeSuccess;
            }
#if !DEBUG
            catch (Exception ex)
            {
                progress.Report(ex.Message);
                return false;
            }
#endif
            finally { }
        }

        private static bool ListPackages(string projectRootDirectory, bool allowBeta, IProgress<string> progress)
        {
            try
            {
                // Ladda in den lokala CPack.Config-filen (eller skapa en ny)
                CPackSettings localCPackSettings = GetLocalCPackSettings(projectRootDirectory);
                bool serverAvailable = true;

                // Lista alla paket
                foreach (CPack pack in localCPackSettings.CPacks)
                {
                    string message = pack.Name + " " + pack.Version;

                    // Check for package of newer version
                    try
                    {
                        if (serverAvailable)
                        {
                            CPackSettings metaCPackSettings = GetMetaCPackSettingsFromInternet(pack.Name, allowBeta);

                            if (metaCPackSettings.CPacks.Length > 0)
                            {
                                CPack onlinePack = metaCPackSettings.CPacks[0];

                                if (metaCPackSettings.CPacks.Length > 0 && onlinePack.Version > pack.Version)
                                {
                                    message += " (version " + onlinePack.Version + " is available)";
                                }
                            }
                        }
                    }
                    catch (WebException)
                    {
                        serverAvailable = false;
                    }

                    progress.Report(message);
                }

                if (!serverAvailable)
                {
                    progress.Report("(Server was not found. Failed to check for new versions.)");
                }

                return true;
            }
#if !DEBUG
            catch (Exception ex)
            {
                progress.Report(ex.Message);
                return false;
            }
#endif
            finally { }
        }

        public async Task<bool> InstallPackageAsync(string packageName, string projectRootDirectory, AppVersion targetVersion, bool updateDependencies, bool overwrite, bool allowBeta, IProgress<string> progress)
        {
            bool packageInstalled = await Task.Run<bool>(() =>
                                                         {
                                                             bool result = InstallPackage(packageName, projectRootDirectory, targetVersion, updateDependencies, overwrite, allowBeta, progress);

                                                             if (result)
                                                             {
                                                                 progress.Report(">");
                                                             }
                                                             else
                                                             {
                                                                 progress.Report(">");
                                                             }

                                                             return result;
                                                         });

            return packageInstalled;
        }

        public async Task<bool> UpdatePackageAsync(string packageName, string projectRootDirectory, AppVersion targetVersion, bool updateDependencies, bool overwrite, bool allowBeta, IProgress<string> progress)
        {
            bool packageUpdated = await Task.Run<bool>(() =>
            {
                bool result = UpdatePackage(packageName, projectRootDirectory, targetVersion, updateDependencies, overwrite, allowBeta, progress);

                if (result)
                {
                    progress.Report(">");
                }
                else
                {
                    progress.Report(">");
                }

                return result;
            });

            return packageUpdated;
        }

        public async Task<bool> RemovePackageAsync(string packageName, string projectRootDirectory, bool removedependents, bool overwrite, IProgress<string> progress)
        {
            bool packageUpdated = await Task.Run<bool>(() =>
            {
                bool result = RemovePackage(packageName, projectRootDirectory, removedependents, overwrite, progress);

                if (result)
                {
                    progress.Report(">");
                }
                else
                {
                    progress.Report(">");
                }

                return result;
            });

            return packageUpdated;
        }

        public async Task<bool> ListPackagesAsync(string projectRootDirectory, bool allowBeta, IProgress<string> progress)
        {
            bool packageUpdated = await Task.Run<bool>(() =>
            {
                ListPackages(projectRootDirectory, allowBeta, progress);

                progress.Report(">");

                return true;
            });

            return packageUpdated;
        }
    }
}
