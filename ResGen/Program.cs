using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Resources.NetStandard;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace ResGen
{
  public static class ResGen
    {
    private const int errorCode = -1;
    private static int errors = 0;
    private static int warnings = 0;
    private static List<AssemblyName> assemblyList;
    private static List<string> definesList = new List<string>();
    private static readonly object consoleOutputLock = new object();
    private static string BadFileExtensionResourceString;

    private static void Error(string message) => ResGen.Error(message, 0);

    private static void Error(string message, int errorNumber)
    {
      Console.Error.WriteLine("ResGen : error RG{1:0000}: {0}", (object) message, (object) errorNumber);
      ++ResGen.errors;
    }

    private static void Error(string message, string fileName) => ResGen.Error(message, fileName, 0);

    private static void Error(string message, string fileName, int errorNumber)
    {
      Console.Error.WriteLine("{0} : error RG{1:0000}: {2}", (object) fileName, (object) errorNumber, (object) message);
      ++ResGen.errors;
    }

    private static void Error(string message, string fileName, int line, int column) => ResGen.Error(message, fileName, line, column, 0);

    private static void Error(
      string message,
      string fileName,
      int line,
      int column,
      int errorNumber)
    {
      Console.Error.WriteLine("{0}({1},{2}): error RG{3:0000}: {4}", (object) fileName, (object) line, (object) column, (object) errorNumber, (object) message);
      ++ResGen.errors;
    }

    private static void Warning(string message)
    {
      Console.Error.WriteLine("ResGen : warning RG0000 : {0}", (object) message);
      ++ResGen.warnings;
    }

    private static void Warning(string message, string fileName) => ResGen.Warning(message, fileName, 0);

    private static void Warning(string message, string fileName, int warningNumber)
    {
      Console.Error.WriteLine("{0} : warning RG{1:0000}: {2}", (object) fileName, (object) warningNumber, (object) message);
      ++ResGen.warnings;
    }

    private static void Warning(string message, string fileName, int line, int column) => ResGen.Warning(message, fileName, line, column, 0);

    private static void Warning(
      string message,
      string fileName,
      int line,
      int column,
      int warningNumber)
    {
      Console.Error.WriteLine("{0}({1},{2}): warning RG{3:0000}: {4}", (object) fileName, (object) line, (object) column, (object) warningNumber, (object) message);
      ++ResGen.warnings;
    }

    private static ResGen.Format GetFormat(string filename)
    {
      string extension = Path.GetExtension(filename);
      if (string.Compare(extension, ".txt", StringComparison.OrdinalIgnoreCase) == 0 || string.Compare(extension, ".restext", StringComparison.OrdinalIgnoreCase) == 0)
        return ResGen.Format.Text;
      if (string.Compare(extension, ".resx", StringComparison.OrdinalIgnoreCase) == 0 || string.Compare(extension, ".resw", StringComparison.OrdinalIgnoreCase) == 0)
        return ResGen.Format.XML;
      if (string.Compare(extension, ".resources.dll", StringComparison.OrdinalIgnoreCase) == 0 || string.Compare(extension, ".dll", StringComparison.OrdinalIgnoreCase) == 0 || string.Compare(extension, ".exe", StringComparison.OrdinalIgnoreCase) == 0)
        return ResGen.Format.Assembly;
      if (string.Compare(extension, ".resources", StringComparison.OrdinalIgnoreCase) == 0)
        return ResGen.Format.Binary;
      ResGen.Error(SR1.GetString("UnknownFileExtension", (object) extension, (object) filename));
      Environment.Exit(-1);
      return ResGen.Format.Text;
    }

    private static void RemoveCorruptedFile(string filename)
    {
      ResGen.Error(SR1.GetString("CorruptOutput", (object) filename));
      try
      {
        File.Delete(filename);
      }
      catch (Exception)
      {
        ResGen.Error(SR1.GetString("DeleteOutputFileFailed", (object) filename));
      }
    }

    private static void SetConsoleUICulture()
    {
      Thread currentThread = Thread.CurrentThread;
      currentThread.CurrentUICulture = CultureInfo.CurrentUICulture.GetConsoleFallbackUICulture();
      if (Console.OutputEncoding.CodePage == Encoding.UTF8.CodePage || Console.OutputEncoding.CodePage == currentThread.CurrentUICulture.TextInfo.OEMCodePage || Console.OutputEncoding.CodePage == currentThread.CurrentUICulture.TextInfo.ANSICodePage)
        return;
      currentThread.CurrentUICulture = new CultureInfo("en-US");
    }

    public static void Main(string[] args)
    {
      Environment.ExitCode = -1;
      ResGen.SetConsoleUICulture();
      ResGen.BadFileExtensionResourceString = "BadFileExtensionOnWindows";
      if (args.Length < 1 || args[0].Equals("-h", StringComparison.OrdinalIgnoreCase) || (args[0].Equals("-?", StringComparison.OrdinalIgnoreCase) || args[0].Equals("/h", StringComparison.OrdinalIgnoreCase)) || args[0].Equals("/?", StringComparison.OrdinalIgnoreCase))
      {
        ResGen.Usage();
      }
      else
      {
        bool flag1 = false;
        List<string> stringList = new List<string>();
        foreach (string str1 in args)
        {
          if (str1.StartsWith("@", StringComparison.OrdinalIgnoreCase))
          {
            if (flag1)
            {
              ResGen.Error(SR1.GetString("MultipleResponseFiles"));
              break;
            }
            if (str1.Length == 1)
            {
              ResGen.Error(SR1.GetString("MalformedResponseFileName", (object) str1));
              break;
            }
            string str2 = str1.Substring(1);
            if (!ResGen.ValidResponseFileName(str2))
            {
              ResGen.Error(SR1.GetString(ResGen.BadFileExtensionResourceString, (object) str2));
              break;
            }
            if (!File.Exists(str2))
            {
              ResGen.Error(SR1.GetString("ResponseFileDoesntExist", (object) str2));
              break;
            }
            flag1 = true;
            try
            {
              foreach (string readAllLine in File.ReadAllLines(str2))
              {
                string str3 = readAllLine.Trim();
                if (str3.Length != 0 && !str3.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                {
                  if (str3.StartsWith("/compile", StringComparison.OrdinalIgnoreCase) && str3.Length > 8)
                  {
                    ResGen.Error(SR1.GetString("MalformedResponseFileEntry", (object) str2, (object) str3));
                    break;
                  }
                  stringList.Add(str3);
                }
              }
            }
            catch (Exception ex)
            {
              ResGen.Error(ex.Message, str2);
            }
          }
          else
            stringList.Add(str1);
        }
        string[] inFiles = (string[]) null;
        string[] outFilesOrDirs = (string[]) null;
        ResGen.ResourceClassOptions resourceClassOptions = (ResGen.ResourceClassOptions) null;
        int index1 = 0;
        bool flag2 = false;
        bool flag3 = false;
        bool useSourcePath = false;
        bool isClassInternal = true;
        bool simulateVS = false;
        for (; index1 < stringList.Count && ResGen.errors == 0; ++index1)
        {
          if (stringList[index1].Equals("/compile", StringComparison.OrdinalIgnoreCase))
          {
            SortedSet<string> sortedSet = new SortedSet<string>((IComparer<string>) StringComparer.OrdinalIgnoreCase);
            inFiles = new string[stringList.Count - index1 - 1];
            outFilesOrDirs = new string[stringList.Count - index1 - 1];
            for (int index2 = 0; index2 < inFiles.Length; ++index2)
            {
              inFiles[index2] = stringList[index1 + 1];
              int length = inFiles[index2].IndexOf(',');
              if (length != -1)
              {
                string str = inFiles[index2];
                inFiles[index2] = str.Substring(0, length);
                if (!ResGen.ValidResourceFileName(inFiles[index2]))
                {
                  ResGen.Error(SR1.GetString(ResGen.BadFileExtensionResourceString, (object) inFiles[index2]));
                  break;
                }
                if (length == str.Length - 1)
                {
                  ResGen.Error(SR1.GetString("MalformedCompileString", (object) str));
                  inFiles = new string[0];
                  break;
                }
                outFilesOrDirs[index2] = str.Substring(length + 1);
                if (ResGen.GetFormat(inFiles[index2]) == ResGen.Format.Assembly)
                {
                  ResGen.Error(SR1.GetString("CompileSwitchNotSupportedForAssemblies"));
                  break;
                }
                if (!ResGen.ValidResourceFileName(outFilesOrDirs[index2]))
                {
                  ResGen.Error(SR1.GetString(ResGen.BadFileExtensionResourceString, (object) outFilesOrDirs[index2]));
                  break;
                }
              }
              else
              {
                if (!ResGen.ValidResourceFileName(inFiles[index2]))
                {
                  if (inFiles[index2][0] == '/' || inFiles[index2][0] == '-')
                  {
                    ResGen.Error(SR1.GetString("InvalidCommandLineSyntax", (object) "/compile", (object) inFiles[index2]));
                    break;
                  }
                  ResGen.Error(SR1.GetString(ResGen.BadFileExtensionResourceString, (object) inFiles[index2]));
                  break;
                }
                string resourceFileName = ResGen.GetResourceFileName(inFiles[index2]);
                outFilesOrDirs[index2] = resourceFileName;
              }
              string fullPath = Path.GetFullPath(outFilesOrDirs[index2]);
              if (sortedSet.Contains(fullPath))
              {
                ResGen.Error(SR1.GetString("DuplicateOutputFilenames", (object) fullPath));
                break;
              }
              sortedSet.Add(fullPath);
              ++index1;
            }
          }
          else if (stringList[index1].StartsWith("/str:", StringComparison.OrdinalIgnoreCase))
          {
            string str = stringList[index1];
            int num = str.IndexOf(',', 5);
            if (num == -1)
              num = str.Length;
            string language = str.Substring(5, num - 5);
            string nameSpace = (string) null;
            string className = (string) null;
            string outputFileName = (string) null;
            int startIndex1 = num + 1;
            if (num < str.Length)
            {
              num = str.IndexOf(',', startIndex1);
              if (num == -1)
                num = str.Length;
            }
            if (startIndex1 <= num)
            {
              nameSpace = str.Substring(startIndex1, num - startIndex1);
              if (num < str.Length)
              {
                int startIndex2 = num + 1;
                num = str.IndexOf(',', startIndex2);
                if (num == -1)
                  num = str.Length;
                className = str.Substring(startIndex2, num - startIndex2);
              }
              int startIndex3 = num + 1;
              if (startIndex3 < str.Length)
                outputFileName = str.Substring(startIndex3, str.Length - startIndex3);
            }
            resourceClassOptions = new ResGen.ResourceClassOptions(language, nameSpace, className, outputFileName, isClassInternal, simulateVS);
          }
          else if (stringList[index1].StartsWith("/define:", StringComparison.OrdinalIgnoreCase) || stringList[index1].StartsWith("-define:", StringComparison.OrdinalIgnoreCase) || (stringList[index1].StartsWith("/D:", StringComparison.OrdinalIgnoreCase) || stringList[index1].StartsWith("-D:", StringComparison.OrdinalIgnoreCase)) || (stringList[index1].StartsWith("/d:", StringComparison.OrdinalIgnoreCase) || stringList[index1].StartsWith("-d:", StringComparison.OrdinalIgnoreCase)))
          {
            string str1 = stringList[index1].StartsWith("/D:", StringComparison.OrdinalIgnoreCase) || stringList[index1].StartsWith("-D:", StringComparison.OrdinalIgnoreCase) || (stringList[index1].StartsWith("/d:", StringComparison.OrdinalIgnoreCase) || stringList[index1].StartsWith("-d:", StringComparison.OrdinalIgnoreCase)) ? stringList[index1].Substring(3) : stringList[index1].Substring(8);
            char[] chArray = new char[1]{ ',' };
            foreach (string str2 in str1.Split(chArray))
            {
              if (str2.Length == 0 || str2.Contains("&") || (str2.Contains("|") || str2.Contains("(")))
                ResGen.Error(SR1.GetString("InvalidIfdef", (object) str2));
              ResGen.definesList.Add(str2);
            }
          }
          else if (stringList[index1].StartsWith("/r:", StringComparison.OrdinalIgnoreCase) || stringList[index1].StartsWith("-r:", StringComparison.OrdinalIgnoreCase))
          {
            string assemblyFile = stringList[index1].Substring(3);
            if (ResGen.assemblyList == null)
              ResGen.assemblyList = new List<AssemblyName>();
            try
            {
              ResGen.assemblyList.Add(AssemblyName.GetAssemblyName(assemblyFile));
            }
            catch (Exception ex)
            {
              ResGen.Error(SR1.GetString("CantLoadAssembly", (object) assemblyFile, (object) ex.GetType().Name, (object) ex.Message));
            }
          }
          else if (stringList[index1].Equals("/usesourcepath", StringComparison.OrdinalIgnoreCase) || stringList[index1].Equals("-usesourcepath", StringComparison.OrdinalIgnoreCase))
            useSourcePath = true;
          else if (stringList[index1].Equals("/publicclass", StringComparison.OrdinalIgnoreCase) || stringList[index1].Equals("-publicclass", StringComparison.OrdinalIgnoreCase))
            isClassInternal = false;
          else if (ResGen.ValidResourceFileName(stringList[index1]))
          {
            if (!flag2)
            {
              inFiles = new string[1]{ stringList[index1] };
              outFilesOrDirs = new string[1]
              {
                ResGen.GetFormat(inFiles[0]) != ResGen.Format.Assembly ? ResGen.GetResourceFileName(inFiles[0]) : (string) null
              };
              flag2 = true;
            }
            else if (!flag3)
            {
              outFilesOrDirs[0] = stringList[index1];
              if (ResGen.GetFormat(inFiles[0]) == ResGen.Format.Assembly)
              {
                if (ResGen.ValidResourceFileName(outFilesOrDirs[0]))
                  ResGen.Warning(SR1.GetString("MustProvideOutputDirectoryNotFilename", (object) outFilesOrDirs[0]));
                if (!Directory.Exists(outFilesOrDirs[0]))
                  ResGen.Error(SR1.GetString("OutputDirectoryMustExist", (object) outFilesOrDirs[0]));
              }
              flag3 = true;
            }
            else
            {
              ResGen.Error(SR1.GetString("InvalidCommandLineSyntax", (object) "<none>", (object) stringList[index1]));
              break;
            }
          }
          else if (flag2 && !flag3 && ResGen.GetFormat(inFiles[0]) == ResGen.Format.Assembly)
          {
            outFilesOrDirs[0] = stringList[index1];
            if (!Directory.Exists(outFilesOrDirs[0]))
              ResGen.Error(SR1.GetString("OutputDirectoryMustExist", (object) outFilesOrDirs[0]));
            flag3 = true;
          }
          else
          {
            if (stringList[index1][0] == '/' || stringList[index1][0] == '-')
            {
              ResGen.Error(SR1.GetString("BadCommandLineOption", (object) stringList[index1]));
              return;
            }
            ResGen.Error(SR1.GetString(ResGen.BadFileExtensionResourceString, (object) stringList[index1]));
            return;
          }
        }
        if ((inFiles == null || inFiles.Length == 0) && ResGen.errors == 0)
        {
          ResGen.Usage();
        }
        else
        {
          if (resourceClassOptions != null)
          {
            resourceClassOptions.InternalClass = isClassInternal;
            if (inFiles.Length > 1 && (resourceClassOptions.ClassName != null || resourceClassOptions.OutputFileName != null))
              ResGen.Error(SR1.GetString("CompileAndSTRDontMix"));
            if (ResGen.GetFormat(inFiles[0]) == ResGen.Format.Assembly)
              ResGen.Error(SR1.GetString("STRSwitchNotSupportedForAssemblies"));
          }
          if (ResGen.errors == 0)
            Parallel.For(0, inFiles.Length, (Action<int>) (i => new ResGen.ResGenRunner().ProcessFile(inFiles[i], outFilesOrDirs[i], resourceClassOptions, useSourcePath)));
          if (ResGen.warnings != 0)
            Console.Error.WriteLine(SR1.GetString("WarningCount", (object) ResGen.warnings));
          if (ResGen.errors != 0)
            Console.Error.WriteLine(SR1.GetString("ErrorCount", (object) ResGen.errors));
          else
            Environment.ExitCode = 0;
        }
      }
    }

    private static string GetResourceFileName(string inFile)
    {
      if (inFile == null)
        return (string) null;
      int length = inFile.LastIndexOf('.');
      return length == -1 ? (string) null : inFile.Substring(0, length) + ".resources";
    }

    private static bool ValidResourceFileName(string inFile) => inFile != null && (inFile.EndsWith(".resx", StringComparison.OrdinalIgnoreCase) || inFile.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || (inFile.EndsWith(".restext", StringComparison.OrdinalIgnoreCase) || inFile.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase)) || (inFile.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || inFile.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || inFile.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)));

    private static bool ValidResponseFileName(string inFile) => inFile != null && inFile.EndsWith(".rsp", StringComparison.OrdinalIgnoreCase);

    private static bool IfdefsAreActive(IEnumerable<string> searchForAll, IList<string> defines)
    {
      foreach (string str in searchForAll)
      {
        if (str[0] == '!')
        {
          if (defines.Contains(str.Substring(1)))
            return false;
        }
        else if (!defines.Contains(str))
          return false;
      }
      return true;
    }

    private static void Usage()
    {
      Console.WriteLine(SR1.GetString("UsageOnWindows", (object) "4.6.1590.0", (object) CommonResStrings.CopyrightForCmdLine));
      Console.WriteLine(SR1.GetString("ValidLanguages"));
      CompilerInfo[] allCompilerInfo = CodeDomProvider.GetAllCompilerInfo();
      for (int index1 = 0; index1 < allCompilerInfo.Length; ++index1)
      {
        string[] languages = allCompilerInfo[index1].GetLanguages();
        if (index1 != 0)
          Console.Write(", ");
        for (int index2 = 0; index2 < languages.Length; ++index2)
        {
          if (index2 != 0)
            Console.Write(", ");
          Console.Write(languages[index2]);
        }
      }
      Console.WriteLine();
    }

    internal sealed class ResourceClassOptions
    {
      private string _language;
      private string _nameSpace;
      private string _className;
      private string _outputFileName;
      private bool _internalClass;
      private bool _simulateVS;

      internal ResourceClassOptions(
        string language,
        string nameSpace,
        string className,
        string outputFileName,
        bool isClassInternal,
        bool simulateVS)
      {
        this._language = language;
        this._nameSpace = nameSpace;
        this._className = className;
        this._outputFileName = outputFileName;
        this._internalClass = isClassInternal;
        this._simulateVS = simulateVS;
      }

      internal string Language => this._language;

      internal string NameSpace => this._nameSpace;

      internal string ClassName => this._className;

      internal string OutputFileName => this._outputFileName;

      internal bool InternalClass
      {
        get => this._internalClass;
        set => this._internalClass = value;
      }

      internal bool SimulateVS
      {
        get => this._simulateVS;
        set => this._simulateVS = value;
      }
    }

    internal sealed class LineNumberStreamReader : StreamReader
    {
      private int _lineNumber;
      private int _col;

      internal LineNumberStreamReader(string fileName, Encoding encoding, bool detectEncoding)
        : base(fileName, encoding, detectEncoding)
      {
        this._lineNumber = 1;
        this._col = 0;
      }

      internal LineNumberStreamReader(Stream stream)
        : base(stream)
      {
        this._lineNumber = 1;
        this._col = 0;
      }

      public override int Read()
      {
        int num = base.Read();
        if (num != -1)
        {
          ++this._col;
          if (num == 10)
          {
            ++this._lineNumber;
            this._col = 0;
          }
        }
        return num;
      }

      public override int Read([In, Out] char[] chars, int index, int count)
      {
        int num = base.Read(chars, index, count);
        for (int index1 = 0; index1 < num; ++index1)
        {
          if (chars[index1 + index] == '\n')
          {
            ++this._lineNumber;
            this._col = 0;
          }
          else
            ++this._col;
        }
        return num;
      }

      public override string ReadLine()
      {
        string str = base.ReadLine();
        if (str != null)
        {
          ++this._lineNumber;
          this._col = 0;
        }
        return str;
      }

      public override string ReadToEnd() => throw new NotImplementedException("NYI");

      internal int LineNumber => this._lineNumber;

      internal int LinePosition => this._col;
    }

    internal sealed class TextFileException : Exception
    {
      private string _fileName;
      private int _lineNumber;
      private int _column;

      internal TextFileException(
        string message,
        string fileName,
        int lineNumber,
        int linePosition)
        : base(message)
      {
        this._fileName = fileName;
        this._lineNumber = lineNumber;
        this._column = linePosition;
      }

      internal string FileName => this._fileName;

      internal int LineNumber => this._lineNumber;

      internal int LinePosition => this._column;
    }

    private class ResGenRunner
    {
      private List<Action> bufferedOutput = new List<Action>(2);
      private List<ResGen.ResGenRunner.ReaderInfo> readers = new List<ResGen.ResGenRunner.ReaderInfo>();
      private bool hadErrors;

      private void AddResource(
        ResGen.ResGenRunner.ReaderInfo reader,
        string name,
        object value,
        string inputFileName,
        int lineNumber,
        int linePosition)
      {
        ResGen.Entry entry = new ResGen.Entry(name, value);
        if (reader.resourcesHashTable.ContainsKey((object) name))
        {
          this.Warning(SR1.GetString("DuplicateResourceKey", (object) name), inputFileName, lineNumber, linePosition);
        }
        else
        {
          reader.resources.Add((object) entry);
          reader.resourcesHashTable.Add((object) name, value);
        }
      }

      private void AddResource(
        ResGen.ResGenRunner.ReaderInfo reader,
        string name,
        object value,
        string inputFileName)
      {
        ResGen.Entry entry = new ResGen.Entry(name, value);
        if (reader.resourcesHashTable.ContainsKey((object) name))
        {
          this.Warning(SR1.GetString("DuplicateResourceKey", (object) name), inputFileName);
        }
        else
        {
          reader.resources.Add((object) entry);
          reader.resourcesHashTable.Add((object) name, value);
        }
      }

      private void Error(string message) => this.Error(message, 0);

      private void Error(string message, int errorNumber)
      {
        this.BufferErrorLine("ResGen : error RG{1:0000}: {0}", (object) message, (object) errorNumber);
        Interlocked.Increment(ref ResGen.errors);
        this.hadErrors = true;
      }

      private void Error(string message, string fileName) => this.Error(message, fileName, 0);

      private void Error(string message, string fileName, int errorNumber)
      {
        this.BufferErrorLine("{0} : error RG{1:0000}: {2}", (object) fileName, (object) errorNumber, (object) message);
        Interlocked.Increment(ref ResGen.errors);
        this.hadErrors = true;
      }

      private void Error(string message, string fileName, int line, int column) => this.Error(message, fileName, line, column, 0);

      private void Error(string message, string fileName, int line, int column, int errorNumber)
      {
        this.BufferErrorLine("{0}({1},{2}): error RG{3:0000}: {4}", (object) fileName, (object) line, (object) column, (object) errorNumber, (object) message);
        Interlocked.Increment(ref ResGen.errors);
        this.hadErrors = true;
      }

      private void Warning(string message)
      {
        this.BufferErrorLine("ResGen : warning RG0000 : {0}", (object) message);
        Interlocked.Increment(ref ResGen.warnings);
      }

      private void Warning(string message, string fileName) => this.Warning(message, fileName, 0);

      private void Warning(string message, string fileName, int warningNumber)
      {
        this.BufferErrorLine("{0} : warning RG{1:0000}: {2}", (object) fileName, (object) warningNumber, (object) message);
        Interlocked.Increment(ref ResGen.warnings);
      }

      private void Warning(string message, string fileName, int line, int column) => this.Warning(message, fileName, line, column, 0);

      private void Warning(
        string message,
        string fileName,
        int line,
        int column,
        int warningNumber)
      {
        this.BufferErrorLine("{0}({1},{2}): warning RG{3:0000}: {4}", (object) fileName, (object) line, (object) column, (object) warningNumber, (object) message);
        Interlocked.Increment(ref ResGen.warnings);
      }

      private void BufferErrorLine(string formatString, params object[] args) => this.bufferedOutput.Add((Action) (() => Console.Error.WriteLine(formatString, args)));

      private void BufferWriteLine() => this.BufferWriteLine("");

      private void BufferWriteLine(string formatString, params object[] args) => this.bufferedOutput.Add((Action) (() => Console.WriteLine(formatString, args)));

      private void BufferWrite(string formatString, params object[] args) => this.bufferedOutput.Add((Action) (() => Console.Write(formatString, args)));

      public void ProcessFile(
        string inFile,
        string outFileOrDir,
        ResGen.ResourceClassOptions resourceClassOptions,
        bool useSourcePath)
      {
        this.ProcessFileWorker(inFile, outFileOrDir, resourceClassOptions, useSourcePath);
        lock (ResGen.consoleOutputLock)
        {
          foreach (Action action in this.bufferedOutput)
            action();
        }
        if (!this.hadErrors || outFileOrDir == null || (!File.Exists(outFileOrDir) || ResGen.GetFormat(inFile) == ResGen.Format.Assembly) || ResGen.GetFormat(outFileOrDir) == ResGen.Format.Assembly)
          return;
        GC.Collect(2);
        GC.WaitForPendingFinalizers();
        try
        {
          File.Delete(outFileOrDir);
        }
        catch
        {
        }
      }

      public void ProcessFileWorker(
        string inFile,
        string outFileOrDir,
        ResGen.ResourceClassOptions resourceClassOptions,
        bool useSourcePath)
      {
        try
        {
          if (!File.Exists(inFile))
          {
            this.Error(SR1.GetString("FileNotFound", (object) inFile));
            return;
          }
          if (ResGen.GetFormat(inFile) != ResGen.Format.Assembly && ResGen.GetFormat(outFileOrDir) == ResGen.Format.Assembly)
          {
            this.Error(SR1.GetString("CannotWriteAssembly", (object) outFileOrDir));
            return;
          }
          this.ReadResources(inFile, useSourcePath);
        }
        catch (ArgumentException ex)
        {
          if (ex.InnerException is XmlException)
          {
            XmlException innerException = (XmlException) ex.InnerException;
            this.Error(innerException.Message, inFile, innerException.LineNumber, innerException.LinePosition);
            return;
          }
          this.Error(ex.Message, inFile);
          return;
        }
        catch (ResGen.TextFileException ex)
        {
          this.Error(ex.Message, ex.FileName, ex.LineNumber, ex.LinePosition);
          return;
        }
        catch (XmlException ex)
        {
          this.Error(ex.Message, inFile, ex.LineNumber, ex.LinePosition);
          return;
        }
        catch (Exception ex)
        {
          this.Error(ex.Message, inFile);
          if (ex.InnerException == null)
            return;
          Exception innerException = ex.InnerException;
          StringBuilder stringBuilder = new StringBuilder(200);
          stringBuilder.Append(ex.Message);
          for (; innerException != null; innerException = innerException.InnerException)
          {
            stringBuilder.Append(" ---> ");
            stringBuilder.Append(innerException.GetType().Name);
            stringBuilder.Append(": ");
            stringBuilder.Append(innerException.Message);
          }
          this.Error(SR1.GetString("SpecificError", (object) ex.InnerException.GetType().Name, (object) stringBuilder.ToString()), inFile);
          return;
        }
        string str1 = (string) null;
        string str2 = (string) null;
        string sourceFile = (string) null;
        bool flag = true;
        try
        {
          if (ResGen.GetFormat(inFile) == ResGen.Format.Assembly)
          {
            foreach (ResGen.ResGenRunner.ReaderInfo reader in this.readers)
            {
              string path2 = reader.outputFileName + ".resw";
              str1 = (string) null;
              flag = true;
              str2 = Path.Combine(outFileOrDir ?? string.Empty, reader.cultureName ?? string.Empty);
              if (str2.Length == 0)
              {
                str1 = path2;
              }
              else
              {
                if (!Directory.Exists(str2))
                {
                  flag = false;
                  Directory.CreateDirectory(str2);
                }
                str1 = Path.Combine(str2, path2);
              }
              this.WriteResources(reader, str1);
            }
          }
          else
          {
            str1 = outFileOrDir;
            this.WriteResources(this.readers[0], outFileOrDir);
            if (resourceClassOptions == null)
              return;
            this.CreateStronglyTypedResources(this.readers[0], outFileOrDir, resourceClassOptions, inFile, out sourceFile);
          }
        }
        catch (IOException ex1)
        {
          if (str1 != null)
          {
            this.Error(SR1.GetString("WriteError", (object) str1), str1);
            if (ex1.Message != null)
              this.Error(SR1.GetString("SpecificError", (object) ex1.GetType().Name, (object) ex1.Message), str1);
            if (File.Exists(str1) && ResGen.GetFormat(str1) != ResGen.Format.Assembly)
            {
              ResGen.RemoveCorruptedFile(str1);
              if (sourceFile != null)
                ResGen.RemoveCorruptedFile(sourceFile);
            }
          }
          if (str2 == null)
            return;
          if (flag)
            return;
          try
          {
            Directory.Delete(str2);
          }
          catch (Exception)
          {
          }
        }
        catch (Exception ex)
        {
          if (str1 != null)
            this.Error(SR1.GetString("GenericWriteError", (object) str1));
          if (ex.Message == null)
            return;
          this.Error(SR1.GetString("SpecificError", (object) ex.GetType().Name, (object) ex.Message));
        }
      }

      private void CreateStronglyTypedResources(
        ResGen.ResGenRunner.ReaderInfo reader,
        string outFile,
        ResGen.ResourceClassOptions options,
        string inputFileName,
        out string sourceFile)
      {
        CodeDomProvider provider = CodeDomProvider.CreateProvider(options.Language);
        string str1 = outFile.Substring(0, outFile.LastIndexOf('.'));
        int num = str1.LastIndexOfAny(new char[3]
        {
          Path.VolumeSeparatorChar,
          Path.DirectorySeparatorChar,
          Path.AltDirectorySeparatorChar
        });
        if (num != -1)
          str1 = str1.Substring(num + 1);
        string generatedCodeNamespace = options.NameSpace;
        string str2 = options.ClassName;
        if (string.IsNullOrEmpty(str2))
          str2 = str1;
        sourceFile = options.OutputFileName;
        if (string.IsNullOrEmpty(sourceFile))
        {
          string str3 = outFile.Substring(0, outFile.LastIndexOf('.'));
          sourceFile = str3 + "." + provider.FileExtension;
        }
        string[] unmatchable = (string[]) null;
        string str4 = StronglyTypedResourceBuilder.VerifyResourceName(str2, provider);
        if (str4 != null)
          str2 = str4;
        string str5;
        if (string.IsNullOrEmpty(generatedCodeNamespace))
        {
          this.BufferWrite(SR1.GetString("BeginSTRClass"), (object) str2);
          str5 = str2;
        }
        else
        {
          this.BufferWrite(SR1.GetString("BeginSTRClassNamespace"), (object) generatedCodeNamespace, (object) str2);
          str5 = generatedCodeNamespace + "." + str2;
        }
        if (!str1.Equals(str5, StringComparison.OrdinalIgnoreCase) && outFile.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
        {
          this.BufferWriteLine();
          this.Warning(SR1.GetString("ClassnameMustMatchBasename", (object) str1, (object) str5), inputFileName);
        }
        CodeCompileUnit compileUnit = StronglyTypedResourceBuilder.Create((IDictionary) reader.resourcesHashTable, str2, generatedCodeNamespace, provider, options.InternalClass, out unmatchable);
        compileUnit.ReferencedAssemblies.Add("System.dll");
        CodeGeneratorOptions options1 = new CodeGeneratorOptions();
        UTF8Encoding utF8Encoding = new UTF8Encoding(true, true);
        using (TextWriter writer = (TextWriter) new StreamWriter(sourceFile, false, (Encoding) utF8Encoding))
          provider.GenerateCodeFromCompileUnit(compileUnit, writer, options1);
        if (unmatchable.Length != 0)
        {
          this.BufferWriteLine();
          foreach (string str3 in unmatchable)
            this.Error(SR1.GetString("UnmappableResource", (object) str3), inputFileName);
        }
        else
          this.BufferWriteLine(SR1.GetString("DoneDot"));
      }

      private void ReadResources(string filename, bool useSourcePath)
      {
        ResGen.Format format = ResGen.GetFormat(filename);
        if (format == ResGen.Format.Assembly)
        {
          this.ReadAssemblyResources(filename);
        }
        else
        {
          ResGen.ResGenRunner.ReaderInfo readerInfo = new ResGen.ResGenRunner.ReaderInfo();
          this.readers.Add(readerInfo);
          switch (format)
          {
            case ResGen.Format.Text:
              this.ReadTextResources(readerInfo, filename);
              break;
            case ResGen.Format.XML:
              ResXResourceReader resXresourceReader = ResGen.assemblyList == null ? new ResXResourceReader(filename) : new ResXResourceReader(filename, ResGen.assemblyList.ToArray());
              if (useSourcePath)
              {
                string fullPath = Path.GetFullPath(filename);
                resXresourceReader.BasePath = Path.GetDirectoryName(fullPath);
              }
              this.ReadResources(readerInfo, (IResourceReader) resXresourceReader, filename);
              break;
            case ResGen.Format.Binary:
              this.ReadResources(readerInfo, (IResourceReader) new ResourceReader(filename), filename);
              break;
          }
          this.BufferWriteLine(SR1.GetString("ReadIn", (object) readerInfo.resources.Count, (object) filename));
        }
      }

      private void ReadResources(
        ResGen.ResGenRunner.ReaderInfo readerInfo,
        IResourceReader reader,
        string fileName)
      {
        using (reader)
        {
          IDictionaryEnumerator enumerator = reader.GetEnumerator();
          while (enumerator.MoveNext())
          {
            string key = (string) enumerator.Key;
            object obj = enumerator.Value;
            this.AddResource(readerInfo, key, obj, fileName);
          }
        }
      }

      private void ReadTextResources(ResGen.ResGenRunner.ReaderInfo reader, string fileName)
      {
        Stack<string> stringStack = new Stack<string>();
        bool flag1 = false;
        using (ResGen.LineNumberStreamReader numberStreamReader = new ResGen.LineNumberStreamReader(fileName, (Encoding) new UTF8Encoding(true), true))
        {
          StringBuilder stringBuilder1 = new StringBuilder(40);
          StringBuilder stringBuilder2 = new StringBuilder(120);
          int num1 = numberStreamReader.Read();
          string str1;
          string str2;
          while (true)
          {
            switch (num1)
            {
              case -1:
                goto label_68;
              case 10:
              case 13:
                num1 = numberStreamReader.Read();
                continue;
              case 35:
                string str3 = numberStreamReader.ReadLine();
                if (string.IsNullOrEmpty(str3))
                {
                  num1 = numberStreamReader.Read();
                  continue;
                }
                if (str3.StartsWith("ifdef ", StringComparison.InvariantCulture) || str3.StartsWith("ifndef ", StringComparison.InvariantCulture) || (str3.StartsWith("if ", StringComparison.InvariantCulture) || str3.StartsWith("If ", StringComparison.InvariantCulture)))
                {
                  str1 = str3.Substring(str3.IndexOf(' ') + 1).Trim();
                  for (int index = 0; index < str1.Length; ++index)
                  {
                    if (str1[index] == '#' || str1[index] == ';')
                    {
                      str1 = str1.Substring(0, index).Trim();
                      break;
                    }
                  }
                  if (str3[0] == 'I' && str1.EndsWith(" Then", StringComparison.InvariantCulture))
                    str1 = str1.Substring(0, str1.Length - 5);
                  if (str1.Length != 0 && !str1.Contains("&") && (!str1.Contains("|") && !str1.Contains("(")))
                  {
                    if (str3.StartsWith("ifndef", StringComparison.InvariantCulture))
                      str1 = "!" + str1;
                    stringStack.Push(str1);
                    flag1 = !ResGen.IfdefsAreActive((IEnumerable<string>) stringStack, (IList<string>) ResGen.definesList);
                  }
                  else
                    goto label_14;
                }
                else if (str3.StartsWith("endif", StringComparison.InvariantCulture) || str3.StartsWith("End If", StringComparison.InvariantCulture))
                {
                  if (stringStack.Count != 0)
                  {
                    stringStack.Pop();
                    flag1 = !ResGen.IfdefsAreActive((IEnumerable<string>) stringStack, (IList<string>) ResGen.definesList);
                  }
                  else
                    goto label_20;
                }
                num1 = numberStreamReader.Read();
                continue;
              default:
                if (!flag1)
                {
                  switch (num1)
                  {
                    case 9:
                    case 32:
                    case 59:
                      break;
                    case 91:
                      str2 = numberStreamReader.ReadLine();
                      if (str2.Equals("strings]", StringComparison.OrdinalIgnoreCase))
                      {
                        this.Warning(SR1.GetString("StringsTagObsolete"), fileName, numberStreamReader.LineNumber - 1, 1);
                        num1 = numberStreamReader.Read();
                        continue;
                      }
                      goto label_28;
                    default:
                      stringBuilder1.Length = 0;
                      while (num1 != 61)
                      {
                        if (num1 == 13 || num1 == 10)
                          throw new ResGen.TextFileException(SR1.GetString("NoEqualsWithNewLine", (object) stringBuilder1.Length, (object) stringBuilder1), fileName, numberStreamReader.LineNumber, numberStreamReader.LinePosition);
                        stringBuilder1.Append((char) num1);
                        num1 = numberStreamReader.Read();
                        if (num1 == -1)
                          break;
                      }
                      if (stringBuilder1.Length != 0)
                      {
                        if (stringBuilder1[stringBuilder1.Length - 1] == ' ')
                          --stringBuilder1.Length;
                        num1 = numberStreamReader.Read();
                        if (num1 == 32)
                          num1 = numberStreamReader.Read();
                        stringBuilder2.Length = 0;
                        for (; num1 != -1; num1 = numberStreamReader.Read())
                        {
                          bool flag2 = false;
                          if (num1 == 92)
                          {
                            num1 = numberStreamReader.Read();
                            if (num1 <= 92)
                            {
                              if (num1 != 34)
                              {
                                if (num1 == 92)
                                  goto label_58;
                              }
                              else
                              {
                                num1 = 34;
                                goto label_58;
                              }
                            }
                            else if (num1 != 110)
                            {
                              switch (num1 - 114)
                              {
                                case 0:
                                  num1 = 13;
                                  flag2 = true;
                                  goto label_58;
                                case 2:
                                  num1 = 9;
                                  goto label_58;
                                case 3:
                                  char[] buffer = new char[4];
                                  int count = 4;
                                  int index = 0;
                                  int num2;
                                  for (; count > 0; count -= num2)
                                  {
                                    num2 = numberStreamReader.Read(buffer, index, count);
                                    if (num2 == 0)
                                      throw new ResGen.TextFileException(SR1.GetString("BadEscape", (object) (char) num1, (object) stringBuilder1.ToString()), fileName, numberStreamReader.LineNumber, numberStreamReader.LinePosition);
                                    index += num2;
                                  }
                                  num1 = (int) ushort.Parse(new string(buffer), NumberStyles.HexNumber, (IFormatProvider) CultureInfo.InvariantCulture);
                                  flag2 = num1 == 10 || num1 == 13;
                                  goto label_58;
                              }
                            }
                            else
                            {
                              num1 = 10;
                              flag2 = true;
                              goto label_58;
                            }
                            throw new ResGen.TextFileException(SR1.GetString("BadEscape", (object) (char) num1, (object) stringBuilder1.ToString()), fileName, numberStreamReader.LineNumber, numberStreamReader.LinePosition);
                          }
label_58:
                          if (!flag2)
                          {
                            switch (num1)
                            {
                              case 10:
                                num1 = numberStreamReader.Read();
                                goto label_66;
                              case 13:
                                num1 = numberStreamReader.Read();
                                if (num1 != -1)
                                {
                                  if (num1 == 10)
                                  {
                                    num1 = numberStreamReader.Read();
                                    goto label_66;
                                  }
                                  else
                                    break;
                                }
                                else
                                  goto label_66;
                            }
                          }
                          stringBuilder2.Append((char) num1);
                        }
label_66:
                        this.AddResource(reader, stringBuilder1.ToString(), (object) stringBuilder2.ToString(), fileName, numberStreamReader.LineNumber, numberStreamReader.LinePosition);
                        continue;
                      }
                      goto label_35;
                  }
                }
                numberStreamReader.ReadLine();
                num1 = numberStreamReader.Read();
                continue;
            }
          }
label_14:
          throw new ResGen.TextFileException(SR1.GetString("InvalidIfdef", (object) str1), fileName, numberStreamReader.LineNumber - 1, 7);
label_20:
          throw new ResGen.TextFileException(SR1.GetString("UnbalancedEndifs"), fileName, numberStreamReader.LineNumber - 1, 1);
label_28:
          throw new ResGen.TextFileException(SR1.GetString("INFFileBracket", (object) str2), fileName, numberStreamReader.LineNumber - 1, 1);
label_35:
          throw new ResGen.TextFileException(SR1.GetString("NoEquals"), fileName, numberStreamReader.LineNumber, numberStreamReader.LinePosition);
label_68:
          if (stringStack.Count > 0)
            throw new ResGen.TextFileException(SR1.GetString("UnbalancedIfdefs", (object) stringStack.Pop()), fileName, numberStreamReader.LineNumber - 1, 1);
        }
      }

      private void WriteResources(ResGen.ResGenRunner.ReaderInfo reader, string filename)
      {
        switch (ResGen.GetFormat(filename))
        {
          case ResGen.Format.Text:
            this.WriteTextResources(reader, filename);
            break;
          case ResGen.Format.XML:
            this.WriteResources(reader, (IResourceWriter) new ResXResourceWriter(filename));
            break;
          case ResGen.Format.Assembly:
            this.Error(SR1.GetString("CannotWriteAssembly", (object) filename));
            break;
          case ResGen.Format.Binary:
            this.WriteResources(reader, (IResourceWriter) new ResourceWriter(filename));
            break;
        }
      }

      private void WriteResources(ResGen.ResGenRunner.ReaderInfo reader, IResourceWriter writer)
      {
        Exception exception = (Exception) null;
        try
        {
          foreach (ResGen.Entry resource in reader.resources)
          {
            string name = resource.name;
            object obj = resource.value;
            writer.AddResource(name, obj);
          }
          this.BufferWrite(SR1.GetString("BeginWriting"));
        }
        catch (Exception ex)
        {
          exception = ex;
        }
        finally
        {
          if (exception == null)
          {
            writer.Close();
          }
          else
          {
            try
            {
              writer.Close();
            }
            catch (Exception)
            {
            }
            try
            {
              writer.Close();
            }
            catch (Exception)
            {
            }
            throw exception;
          }
        }
        this.BufferWriteLine(SR1.GetString("DoneDot"));
      }

      private void WriteTextResources(ResGen.ResGenRunner.ReaderInfo reader, string fileName)
      {
        using (StreamWriter streamWriter = new StreamWriter(fileName, false, Encoding.UTF8))
        {
          foreach (ResGen.Entry resource in reader.resources)
          {
            string name = resource.name;
            object obj = resource.value;
            if (!(obj is string str))
            {
                this.Error(SR1.GetString("OnlyString", (object)name, (object)obj.GetType().FullName), fileName);
            }
            else
            {
                string str1 = str.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
                streamWriter.WriteLine("{0}={1}", (object)name, (object)str1);
            }
          }
        }
      }

      internal void ReadAssemblyResources(string name)
      {
        Assembly a = (Assembly) null;
        bool mainAssembly = false;
        bool flag = false;
        NeutralResourcesLanguageAttribute languageAttribute = (NeutralResourcesLanguageAttribute) null;
        AssemblyName assemblyName = (AssemblyName) null;
        try
        {
          a = Assembly.UnsafeLoadFrom(name);
          assemblyName = a.GetName();
          CultureInfo culture = (CultureInfo) null;
          try
          {
            culture = assemblyName.CultureInfo;
          }
          catch (ArgumentException ex)
          {
            this.Warning(SR1.GetString("CreatingCultureInfoFailed", (object) ex.GetType().Name, (object) ex.Message, (object) assemblyName.ToString()));
            flag = true;
          }
          if (!flag)
          {
            mainAssembly = culture.Equals((object) CultureInfo.InvariantCulture);
            languageAttribute = this.CheckAssemblyCultureInfo(name, assemblyName, culture, a, mainAssembly);
          }
        }
        catch (BadImageFormatException)
        {
          this.Error(SR1.GetString("BadImageFormat", (object) name));
        }
        catch (Exception ex)
        {
          this.Error(SR1.GetString("CannotLoadAssemblyLoadFromFailed", (object) name, (object) ex));
        }
        if (!(a != (Assembly) null))
          return;
        string[] manifestResourceNames = a.GetManifestResourceNames();
        CultureInfo cultureInfo = (CultureInfo) null;
        string suffix = (string) null;
        if (!flag)
        {
          cultureInfo = assemblyName.CultureInfo;
          if (!cultureInfo.Equals((object) CultureInfo.InvariantCulture))
            suffix = "." + cultureInfo.Name + ".resources";
        }
        foreach (string str in manifestResourceNames)
        {
          if (str.EndsWith(".resources", StringComparison.InvariantCultureIgnoreCase))
          {
            if (mainAssembly)
            {
              if (CultureInfo.InvariantCulture.CompareInfo.IsSuffix(str, ".en-US.resources"))
              {
                this.Error(SR1.GetString("ImproperlyBuiltMainAssembly", (object) str, (object) name));
                continue;
              }
            }
            else if (!flag && !CultureInfo.InvariantCulture.CompareInfo.IsSuffix(str, suffix))
            {
              this.Error(SR1.GetString("ImproperlyBuiltSatelliteAssembly", (object) str, (object) suffix, (object) name));
              continue;
            }
            try
            {
              using (IResourceReader resourceReader = (IResourceReader) new ResourceReader(a.GetManifestResourceStream(str)))
              {
                ResGen.ResGenRunner.ReaderInfo reader = new ResGen.ResGenRunner.ReaderInfo();
                reader.outputFileName = str.Remove(str.Length - 10);
                if (cultureInfo != null && !string.IsNullOrEmpty(cultureInfo.Name))
                  reader.cultureName = cultureInfo.Name;
                else if (languageAttribute != null && !string.IsNullOrEmpty(languageAttribute.CultureName))
                {
                  reader.cultureName = languageAttribute.CultureName;
                  this.Warning(SR1.GetString("NeutralityOfCultureNotPreserved", (object) reader.cultureName));
                }
                if (reader.cultureName != null && reader.outputFileName.EndsWith("." + reader.cultureName, StringComparison.OrdinalIgnoreCase))
                  reader.outputFileName = reader.outputFileName.Remove(reader.outputFileName.Length - (reader.cultureName.Length + 1));
                this.readers.Add(reader);
                foreach (DictionaryEntry dictionaryEntry in resourceReader)
                  this.AddResource(reader, (string) dictionaryEntry.Key, dictionaryEntry.Value, str);
                this.BufferWriteLine(SR1.GetString("ReadIn", (object) reader.resources.Count, (object) str));
              }
            }
            catch (FileNotFoundException)
            {
              this.Error(SR1.GetString("NoResourcesFileInAssembly", (object) str));
            }
          }
        }
      }

      private NeutralResourcesLanguageAttribute CheckAssemblyCultureInfo(
        string name,
        AssemblyName assemblyName,
        CultureInfo culture,
        Assembly a,
        bool mainAssembly)
      {
        NeutralResourcesLanguageAttribute languageAttribute = (NeutralResourcesLanguageAttribute) null;
        if (mainAssembly)
        {
          object[] customAttributes = a.GetCustomAttributes(typeof (NeutralResourcesLanguageAttribute), false);
          if (customAttributes.Length != 0)
          {
            languageAttribute = (NeutralResourcesLanguageAttribute) customAttributes[0];
            if (languageAttribute.Location != UltimateResourceFallbackLocation.Satellite && languageAttribute.Location != UltimateResourceFallbackLocation.MainAssembly)
              this.Warning(SR1.GetString("UnrecognizedUltimateResourceFallbackLocation", (object) languageAttribute.Location, (object) name));
            if (!ResGen.ResGenRunner.ContainsProperlyNamedResourcesFiles(a, true))
              this.Error(SR1.GetString("NoResourcesFilesInAssembly"));
          }
        }
        else
        {
          if (!assemblyName.Name.EndsWith(".resources", StringComparison.InvariantCultureIgnoreCase))
          {
            this.Error(SR1.GetString("SatelliteOrMalformedAssembly", (object) name, (object) culture.Name, (object) assemblyName.Name));
            return (NeutralResourcesLanguageAttribute) null;
          }
          if (a.GetTypes().Length != 0)
            this.Warning(SR1.GetString("SatelliteAssemblyContainsCode", (object) name));
          if (!ResGen.ResGenRunner.ContainsProperlyNamedResourcesFiles(a, false))
            this.Warning(SR1.GetString("SatelliteAssemblyContainsNoResourcesFile", (object) assemblyName.CultureInfo.Name));
        }
        return languageAttribute;
      }

      private static bool ContainsProperlyNamedResourcesFiles(Assembly a, bool mainAssembly)
      {
        string str = mainAssembly ? ".resources" : a.GetName().CultureInfo.Name + ".resources";
        foreach (string manifestResourceName in a.GetManifestResourceNames())
        {
          if (manifestResourceName.EndsWith(str, StringComparison.InvariantCultureIgnoreCase))
            return true;
        }
        return false;
      }

      internal sealed class ReaderInfo
      {
        public string outputFileName;
        public string cultureName;
        public ArrayList resources;
        public Hashtable resourcesHashTable;

        public ReaderInfo()
        {
          this.resources = new ArrayList();
          this.resourcesHashTable = new Hashtable((IEqualityComparer) StringComparer.InvariantCultureIgnoreCase);
        }
      }
    }

    private enum Format
    {
      Text,
      XML,
      Assembly,
      Binary,
    }

    private class Entry
    {
      public string name;
      public object value;

      public Entry(string name, object value)
      {
        this.name = name;
        this.value = value;
      }
    }
  }
}
