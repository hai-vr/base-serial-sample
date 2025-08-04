namespace Hai.PositionSystemToExternalProgram.Configuration;

public static class SaveUtil
{
    private const string AppSaveFolder = "PositionSystemToExternalProgram";

    public static string GetUserDataFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppSaveFolder);
    }
}