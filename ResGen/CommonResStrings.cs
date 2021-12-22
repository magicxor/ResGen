using System.Reflection;
using System.Resources;

namespace ResGen
{
  internal static class CommonResStrings
  {
    private static ResourceManager resmgr = new ResourceManager(typeof(Resources));

    internal static string GetString(string id) => CommonResStrings.resmgr.GetString(id);

    internal static string CopyrightForCmdLine => CommonResStrings.GetString("Microsoft_Copyright_CommandLine_Logo");
  }
}
