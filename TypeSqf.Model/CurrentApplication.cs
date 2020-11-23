using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TypeSqf.WebService;

namespace TypeSqf.Model
{
    public static class CurrentApplication
    {
        private static bool _domainNameUpdated = false;
        private static string _typeSqfWebDomain = "http://typesqf.com";

        public static string Name { get { return "TypeSqf"; } }

        public static string AppDataFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name); } }

        public static string OpenFileFilter { get { return "TypeSqf File (*.sqf, *.sqx, *.sqs, *.ext, *.hpp, *.cpp, *.tproj)|*.sqf;*.sqx;*.sqs;*.ext;*.hpp;*.cpp;*.tproj"; } }

        public static string SaveFileFilter { get { return "TypeSqf File (*.sqf, *.sqx, *.sqs, *.ext, *.hpp, *.cpp)|*.sqf;*.sqx;*.sqs;*.ext;*.hpp;*.cpp"; } }

        public static string SaveProjectFilter { get { return "TypeSqf Project File (*.tproj)|*.tproj"; } }

        public static string OpenProjectFilter { get { return "TypeSqf Project File (*.tproj)|*.tproj"; } }

        public static string OpenMissionFilter { get { return "Arma Mission File|mission.sqm"; } }

        public static string SettingsFileName { get { return @"Settings.xml"; } }

        public static AppVersion Version
        {
            get
            {
                return new AppVersion(1, 07, 11);
            }
        }

        public static async Task<AppVersion> CheckNewVersionAsync()
        {
            AppVersion metaVersion = await Task.Run<AppVersion>(() =>
                                                                    {
                                                                        AppVersion version = null;

                                                                        try {
                                                                            WebClient webClient = new WebClient();
                                                                            string sVersion =
                                                                                webClient.DownloadString(TypeSqfDomain +
                                                                                                         "/download/typesqfversion");

                                                                            AppVersion.TryParse(sVersion, out version);
                                                                        }
                                                                        catch (WebException) {
                                                                        }

                                                                        return version;
                                                                    });

            return metaVersion;
        }

        public static async Task<int> CheckNewWebServiceVersionAsync()
        {
            int webServiceVersion = await Task.Run<int>(() =>
            {
                int version = 0;

                try
                {
                    WebClient webClient = new WebClient();
                    string sVersion =
                        webClient.DownloadString(TypeSqfDomain + "/download/webserviceversion");

                    int.TryParse(sVersion, out version);
                }
                catch (WebException)
                {
                }

                return version;
            });

            return webServiceVersion;
        }

        public static bool IsRelease
        {
            get
            {
#if DEBUG
                return false;
#else
                return true;
#endif
            }
        }

#if DEBUG
        public static string TypeSqfDomain { get { return "http://localhost:1286"; } }
#else
        public static string TypeSqfDomain
        {
            get
            {
                if (!_domainNameUpdated)
                {
                    MyWebClient webClient = new MyWebClient();
                    webClient.DownloadString(_typeSqfWebDomain);
                    _typeSqfWebDomain = webClient.ResponseUri.ToString();
                    _domainNameUpdated = true;
                }

                return _typeSqfWebDomain;
            }
        }
#endif

        public static string PackageDownloadDirectory { get { return "DownloadedPackages"; } }
    }
}
