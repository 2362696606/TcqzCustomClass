// ReSharper disable once CheckNamespace

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace Tcqz.Configuration;

public class ConfigPaths
{
    // ReSharper disable once InconsistentNaming
    private static readonly Lazy<ConfigPaths> _instance = new(()=> new ConfigPaths());
    private string _companyName = string.Empty;
    /// <summary>
    /// 单例实例
    /// </summary>
    public static ConfigPaths Instance => _instance.Value;

    /// <summary>
    /// 字符串最大长度
    /// </summary>
    private const int MaxLengthToUse = 25;
    /// <summary>
    /// 用户配置文件名
    /// </summary>
    private const string UserConfigFilename = "userSettings.db";


    private ConfigPaths()
    {
        #region InitPath
        var exeAssembly = Assembly.GetEntryAssembly();

        bool isSingleFile = false;
        if (exeAssembly?.Location.Length == 0)
        {
            isSingleFile = true;
            HasEntryAssembly = true;
        }

        if (exeAssembly != null && !isSingleFile)
        {
            HasEntryAssembly = true;
            ApplicationUri = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exeAssembly.ManifestModule.Name);
        }
        else
        {
            try
            {
                // An EntryAssembly may not be found when running from a custom host.
                // Try to find the native entry point.
                using Process currentProcess = Process.GetCurrentProcess();
                ApplicationUri = currentProcess.MainModule?.FileName ?? string.Empty;
            }
            catch (PlatformNotSupportedException)
            {
                ApplicationUri = string.Empty;
            }
        }

        string? externalConfigPath = AppDomain.CurrentDomain.GetData("APP_CONFIG_FILE") as string;
        if (!string.IsNullOrEmpty(externalConfigPath))
        {
            if (Uri.IsWellFormedUriString(externalConfigPath, UriKind.Absolute))
            {
                Uri externalConfigUri = new Uri(externalConfigPath, UriKind.Absolute);
                if (externalConfigUri.IsFile)
                {
                    ApplicationConfigUri = externalConfigUri.LocalPath;
                }
            }
            else
            {
                if (!Path.IsPathRooted(externalConfigPath))
                {
                    externalConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, externalConfigPath);
                }

                ApplicationConfigUri = Path.GetFullPath(externalConfigPath);
            }
        }
        else if (!string.IsNullOrEmpty(ApplicationUri))
        {
            string applicationPath = ApplicationUri;
            if (isSingleFile)
            {
                // on Unix, we want to first append '.dll' extension and on Windows change '.exe' to '.dll'
                // eventually, in ApplicationConfigUri we will get '{applicationName}.dll.config'
                applicationPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                    Path.ChangeExtension(ApplicationUri, ".dll") : ApplicationUri + ".dll";
            }

            ApplicationConfigUri = applicationPath + ConfigExtension;
        }

        bool isHttp = StartsWithOrdinalIgnoreCase(ApplicationConfigUri, HttpUri);
        SetNamesAndVersion(exeAssembly, isHttp);
        if (isHttp) return;

        // Create a directory suffix for local and roaming config of three parts:

        // (1) Company name
        string part1 = Validate(_companyName, limitSize: true);

        // (2) Domain or product name & an application uri hash
        string namePrefix = Validate(AppDomain.CurrentDomain.FriendlyName, limitSize: true);
        if (string.IsNullOrEmpty(namePrefix))
            namePrefix = Validate(ProductName, limitSize: true);
        string part2 = !string.IsNullOrEmpty(namePrefix) ? namePrefix : "UnNameApp";

        // (3) The product version
        string part3 = Validate(ProductVersion, limitSize: false);

        string dirSuffix = CombineIfValid(CombineIfValid(part1, part2), part3);

        string roamingFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (Path.IsPathRooted(roamingFolderPath))
        {
            RoamingConfigDirectory = CombineIfValid(roamingFolderPath, dirSuffix);
            RoamingConfigFilename = CombineIfValid(RoamingConfigDirectory, UserConfigFilename);
        }

        string localFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (Path.IsPathRooted(localFolderPath))
        {
            LocalConfigDirectory = CombineIfValid(localFolderPath, dirSuffix);
            LocalConfigFilename = CombineIfValid(LocalConfigDirectory, UserConfigFilename);
        }

        #endregion
    }

    public string HttpUri = "http://";

    public string ConfigExtension  = ".db";

    public string ApplicationConfigUri { get; set; } = string.Empty;

    public string ApplicationUri { get; private set; }

    public bool HasEntryAssembly { get; set; }

    /// <summary>
    /// Local配置文件完整路径
    /// </summary>
    public string LocalConfigFilename { get; private set; } = string.Empty;
    /// <summary>
    /// Local配置文件夹路径
    /// </summary>
    public string LocalConfigDirectory { get; private set; } = string.Empty;
    /// <summary>
    /// Roaming配置文件完整路径
    /// </summary>
    public string RoamingConfigFilename { get; private set; } = string.Empty;
    /// <summary>
    /// Roaming配置文件夹路径
    /// </summary>
    public string RoamingConfigDirectory { get; private set; } = string.Empty;
    /// <summary>
    /// 产品版本
    /// </summary>
    public string ProductVersion { get; private set; } = string.Empty;
    /// <summary>
    /// 产品名
    /// </summary>
    public string ProductName { get; private set; } = string.Empty;
    /// <summary>
    /// 公司名
    /// </summary>
    public string CompanyName => _companyName;
    /// <summary>
    /// Combines path2 with path1 if possible, else returns null.
    /// </summary>
    /// <param name="path1"></param>
    /// <param name="path2"></param>
    /// <returns></returns>
    private static string CombineIfValid(string path1, string path2)
    {
        if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2)) return string.Empty;
        if (string.IsNullOrEmpty(path1))
        {
            return path2;
        }
        if (string.IsNullOrEmpty(path2))
        {
            return path1;
        }

        try
        {
            return Path.Combine(path1, path2);
        }
        catch
        {
            return string.Empty;
        }
    }
    /// <summary>
    /// 获取路径与版本
    /// </summary>
    /// <param name="exeAssembly"></param>
    /// <param name="isHttp"></param>
    private void SetNamesAndVersion(Assembly? exeAssembly, bool isHttp)
    {
        Type? mainType = null;

        // Get CompanyName, ProductName, and ProductVersion
        // First try custom attributes on the assembly.
        if (exeAssembly != null)
        {
            var attrs = exeAssembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
            if ((attrs.Length > 0))
            {
                _companyName = ((AssemblyCompanyAttribute)attrs[0]).Company.Trim();
            }

            attrs = exeAssembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false);
            if ((attrs.Length > 0))
            {
                ProductName = ((AssemblyProductAttribute)attrs[0]).Product.Trim();
            }

            ProductVersion = exeAssembly.GetName().Version?.ToString().Trim()??string.Empty;
        }

        // If we couldn't get custom attributes, fall back on the entry type namespace
        if (!isHttp &&
            (string.IsNullOrEmpty(_companyName) || string.IsNullOrEmpty(ProductName) ||
            string.IsNullOrEmpty(ProductVersion)))
        {
            if (exeAssembly != null)
            {
                var entryPoint = exeAssembly.EntryPoint;
                if (entryPoint != null)
                {
                    mainType = entryPoint.ReflectedType;
                }
            }

            string? ns = null;
            if (mainType != null) ns = mainType.Namespace;

            if (string.IsNullOrEmpty(ProductName))
            {
                // Try the remainder of the namespace
                if (ns != null)
                {
                    var lastDot = ns.LastIndexOf('.');
                    if ((lastDot != -1) && (lastDot < ns.Length - 1)) ProductName = ns.Substring(lastDot + 1);
                    else ProductName = ns;

                    ProductName = ProductName.Trim();
                }

                // Try the type of the entry assembly
                if (string.IsNullOrEmpty(ProductName) && (mainType != null)) ProductName = mainType.Name.Trim();
            }

            if (string.IsNullOrEmpty(_companyName))
            {
                // Try the first part of the namespace
                if (ns != null)
                {
                    var firstDot = ns.IndexOf('.');
                    _companyName = firstDot != -1 ? ns.Substring(0, firstDot) : ns;

                    _companyName = _companyName.Trim();
                }

                // If that doesn't work, use the product name
                if (string.IsNullOrEmpty(_companyName)) _companyName = ProductName;
            }
        }

        // Desperate measures for product version - assume 1.0
        if (string.IsNullOrEmpty(ProductVersion)) ProductVersion = "1.0.0.0";
    }
    /// <summary>
    /// 验证长度
    /// </summary>
    /// <param name="str"></param>
    /// <param name="limitSize"></param>
    /// <returns></returns>
    private static string Validate(string str, bool limitSize)
    {
        var validated = str;

        if (string.IsNullOrEmpty(validated)) return validated;

        // First replace all illegal characters with underscores
        validated = Path.GetInvalidFileNameChars().Aggregate(validated, (current, c) => current.Replace(c, '_'));

        // Replace all spaces with underscores
        validated = validated.Replace(' ', '_');

        if (limitSize)
        {
            validated = validated.Length > MaxLengthToUse
                ? validated.Substring(0, MaxLengthToUse)
                : validated;
        }

        return validated;
    }

    public static bool StartsWithOrdinalIgnoreCase(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s2)) return false;
        return 0 == string.Compare(s1, 0, s2, 0, s2.Length, StringComparison.OrdinalIgnoreCase);
    }
}