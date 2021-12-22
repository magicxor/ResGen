using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;

namespace ResGen
{
    internal sealed class SR2
    {
        internal const string ClassDocComment = "ClassDocComment";
        internal const string ClassComments1 = "ClassComments1";
        internal const string ClassComments3 = "ClassComments3";
        internal const string StringPropertyComment = "StringPropertyComment";
        internal const string StringPropertyTruncatedComment = "StringPropertyTruncatedComment";
        internal const string NonStringPropertyComment = "NonStringPropertyComment";
        internal const string NonStringPropertyDetailedComment = "NonStringPropertyDetailedComment";
        internal const string CulturePropertyComment1 = "CulturePropertyComment1";
        internal const string CulturePropertyComment2 = "CulturePropertyComment2";
        internal const string ResMgrPropertyComment = "ResMgrPropertyComment";
        internal const string MismatchedResourceName = "MismatchedResourceName";
        internal const string InvalidIdentifier = "InvalidIdentifier";

        private static SR2 s_loader = null;
        private MainAssemblyFallbackResourceManager _resources;

        /// <summary>
        /// The containing assembly is set to lookup resources for the neutral language in satellite assemblies, not in the main assembly.
        /// System.Design resources are not meant to be translated, so the ResourceManager should not look for satellite assemblies.
        /// This ResourceManager forces resource lookup to be constrained to the current assembly and not look for satellites.
        /// </summary>
        private class MainAssemblyFallbackResourceManager : ResourceManager
        {
            public MainAssemblyFallbackResourceManager(string baseName, Assembly assembly) : base(baseName, assembly)
            {
                this.FallbackLocation = UltimateResourceFallbackLocation.MainAssembly;
            }
        }

        internal SR2()
        {
            _resources = new MainAssemblyFallbackResourceManager("System.Design", this.GetType().Assembly);
        }

        private static SR2 GetLoader()
        {
            if (s_loader == null)
            {
                SR2 sr2 = new SR2();
                Interlocked.CompareExchange(ref s_loader, sr2, null);
            }
            return s_loader;
        }

        private static CultureInfo Culture
        {
            get { return null/*use ResourceManager default, CultureInfo.CurrentUICulture*/; }
        }

        public static ResourceManager Resources
        {
            get
            {
                return GetLoader()._resources;
            }
        }

        public static string GetString(string name, params object[] args)
        {
            SR2 sys = GetLoader();
            if (sys == null)
                return null;
            string res = sys._resources.GetString(name, SR2.Culture);

            if (args?.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    String value = args[i] as String;
                    if (value?.Length > 1024)
                    {
                        args[i] = value.Substring(0, 1024 - 3) + "...";
                    }
                }
                return String.Format(CultureInfo.CurrentCulture, res, args);
            }
            else
            {
                return res;
            }
        }

        public static string GetString(string name)
        {
            SR2 sys = GetLoader();
            if (sys == null)
                return null;
            return sys._resources.GetString(name, SR2.Culture);
        }

        public static string GetString(string name, out bool usedFallback)
        {
            // always false for this version of gensr
            usedFallback = false;
            return GetString(name);
        }

        public static object GetObject(string name)
        {
            SR2 sys = GetLoader();
            if (sys == null)
                return null;
            return sys._resources.GetObject(name, SR2.Culture);
        }
    }
}