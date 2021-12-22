using System;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace ResGen
{
  internal sealed class SR1
  {
    internal const string DuplicateResourceKey = "DuplicateResourceKey";
    internal const string UnknownFileExtension = "UnknownFileExtension";
    internal const string FileNotFound = "FileNotFound";
    internal const string InvalidResX = "InvalidResX";
    internal const string WriteError = "WriteError";
    internal const string CorruptOutput = "CorruptOutput";
    internal const string DeleteOutputFileFailed = "DeleteOutputFileFailed";
    internal const string SpecificError = "SpecificError";
    internal const string GenericWriteError = "GenericWriteError";
    internal const string ErrorCount = "ErrorCount";
    internal const string WarningCount = "WarningCount";
    internal const string INFFileBracket = "INFFileBracket";
    internal const string NoEqualsWithNewLine = "NoEqualsWithNewLine";
    internal const string NoEquals = "NoEquals";
    internal const string BadFileExtensionOnWindows = "BadFileExtensionOnWindows";
    internal const string BadFileExtensionNotOnWindows = "BadFileExtensionNotOnWindows";
    internal const string MustProvideOutputDirectoryNotFilename = "MustProvideOutputDirectoryNotFilename";
    internal const string OutputDirectoryMustExist = "OutputDirectoryMustExist";
    internal const string BadCommandLineOption = "BadCommandLineOption";
    internal const string BadEscape = "BadEscape";
    internal const string NoName = "NoName";
    internal const string ReadIn = "ReadIn";
    internal const string BeginWriting = "BeginWriting";
    internal const string DoneDot = "DoneDot";
    internal const string BeginSTRClass = "BeginSTRClass";
    internal const string BeginSTRClassNamespace = "BeginSTRClassNamespace";
    internal const string MalformedCompileString = "MalformedCompileString";
    internal const string MalformedResponseFileName = "MalformedResponseFileName";
    internal const string MalformedResponseFileEntry = "MalformedResponseFileEntry";
    internal const string DuplicateOutputFilenames = "DuplicateOutputFilenames";
    internal const string MultipleResponseFiles = "MultipleResponseFiles";
    internal const string ResponseFileDoesntExist = "ResponseFileDoesntExist";
    internal const string StringsTagObsolete = "StringsTagObsolete";
    internal const string OnlyString = "OnlyString";
    internal const string UnmappableResource = "UnmappableResource";
    internal const string UsageOnWindows = "UsageOnWindows";
    internal const string UsageNotOnWindows = "UsageNotOnWindows";
    internal const string InvalidCommandLineSyntax = "InvalidCommandLineSyntax";
    internal const string CompileSwitchNotSupportedForAssemblies = "CompileSwitchNotSupportedForAssemblies";
    internal const string CompileAndSTRDontMix = "CompileAndSTRDontMix";
    internal const string STRSwitchNotSupportedForAssemblies = "STRSwitchNotSupportedForAssemblies";
    internal const string CantLoadAssembly = "CantLoadAssembly";
    internal const string ClassnameMustMatchBasename = "ClassnameMustMatchBasename";
    internal const string ValidLanguages = "ValidLanguages";
    internal const string InvalidIfdef = "InvalidIfdef";
    internal const string UnbalancedEndifs = "UnbalancedEndifs";
    internal const string UnbalancedIfdefs = "UnbalancedIfdefs";
    internal const string CannotWriteAssembly = "CannotWriteAssembly";
    internal const string CreatingCultureInfoFailed = "CreatingCultureInfoFailed";
    internal const string UnrecognizedUltimateResourceFallbackLocation = "UnrecognizedUltimateResourceFallbackLocation";
    internal const string NoResourcesFilesInAssembly = "NoResourcesFilesInAssembly";
    internal const string SatelliteOrMalformedAssembly = "SatelliteOrMalformedAssembly";
    internal const string SatelliteAssemblyContainsCode = "SatelliteAssemblyContainsCode";
    internal const string SatelliteAssemblyContainsNoResourcesFile = "SatelliteAssemblyContainsNoResourcesFile";
    internal const string AssemblyNotFullySigned = "AssemblyNotFullySigned";
    internal const string BadImageFormat = "BadImageFormat";
    internal const string ImproperlyBuiltMainAssembly = "ImproperlyBuiltMainAssembly";
    internal const string ImproperlyBuiltSatelliteAssembly = "ImproperlyBuiltSatelliteAssembly";
    internal const string NeutralityOfCultureNotPreserved = "NeutralityOfCultureNotPreserved";
    internal const string NoResourcesFileInAssembly = "NoResourcesFileInAssembly";
    internal const string MissingFileLocation = "MissingFileLocation";
    internal const string CannotLoadAssemblyLoadFromFailed = "CannotLoadAssemblyLoadFromFailed";
    private static SR1 loader;
    private ResourceManager resources;

    internal SR1() => this.resources = new ResourceManager(typeof(Resources));

    private static SR1 GetLoader()
    {
      if (SR1.loader == null)
      {
        SR1 sr1 = new SR1();
        Interlocked.CompareExchange<SR1>(ref SR1.loader, sr1, (SR1) null);
      }
      return SR1.loader;
    }

    private static CultureInfo Culture => (CultureInfo) null;

    public static ResourceManager Resources => SR1.GetLoader().resources;

    public static string GetString(string name, params object[] args)
    {
      SR1 loader = SR1.GetLoader();
      if (loader == null)
        return (string) null;
      string format = loader.resources.GetString(name, SR1.Culture);
      if (args == null || args.Length == 0)
        return format;
      for (int index = 0; index < args.Length; ++index)
      {
        if (args[index] is string str && str.Length > 1024)
          args[index] = (object) (str.Substring(0, 1021) + "...");
      }
      return string.Format((IFormatProvider) CultureInfo.CurrentCulture, format, args);
    }

    public static string GetString(string name) => SR1.GetLoader()?.resources.GetString(name, SR1.Culture);

    public static string GetString(string name, out bool usedFallback)
    {
      usedFallback = false;
      return SR1.GetString(name);
    }

    public static object GetObject(string name) => SR1.GetLoader()?.resources.GetObject(name, SR1.Culture);
  }
}
