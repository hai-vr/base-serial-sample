using System.Reflection;

namespace Hai.PositionSystemToExternalProgram.ImGuiProgram;

public class PAssets
{
    private static readonly string _directoryName;
    
    public static readonly PAsset TraditionalChineseFont = new("PAssets/fonts/NotoSansTC-VariableFont_wght.ttf");

    static PAssets()
    {
        // https://stackoverflow.com/questions/837488/how-can-i-get-the-applications-path-in-a-net-console-application
        _directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }
    
    public static string MakeAbsoluteToApplicationPath(string relativeAssetPath)
    {
        return Path.Combine(_directoryName, relativeAssetPath);
    }
}

public class PAsset
{
    private readonly string _relativePath;

    public PAsset(string relativePath)
    {
        _relativePath = relativePath;
    }

    public string Absolute()
    {
        return PAssets.MakeAbsoluteToApplicationPath(_relativePath);
    }

    public string Relative()
    {
        return _relativePath;
    }
}