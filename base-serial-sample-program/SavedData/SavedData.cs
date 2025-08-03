using System.Text;
using Newtonsoft.Json;

namespace Hai.HView.Data;

public class SavedData
{
    private const string MainFilename = "user_config.json";
    private const string BackupFilename = "user_config.backup.json";
    private static string Main => Path.Combine(SaveUtil.GetUserDataFolder(), MainFilename);
    private static string Backup => Path.Combine(SaveUtil.GetUserDataFolder(), BackupFilename);

    public int offsetX = 9;
    public int offsetY = 656;
    [JsonIgnore] public bool manualControl;
    public float positionMultiplier = 4f;
    public float rotationMultiplier = 2f;
    public float positionMultiplierL0 = 1f;
    public float positionMultiplierL1 = 1f;
    public float positionMultiplierL2 = 1f;
    public float rotationMultiplierL0 = 1f;
    public float rotationMultiplierL1 = 1f;
    public float rotationMultiplierL2 = 1f;
    public string windowName = "VR";
    
    public int vrOffsetX = 0;
    public int vrOffsetY = 0;
    public bool vrUseRightEye = false;

    public static SavedData OpenConfig()
    {
        return OpenConfig(Main, Backup);
    }

    public static SavedData OpenConfig(string main, string backup)
    {
        if (File.Exists(main))
        {
            try
            {
                var serialized = File.ReadAllText(Main, Encoding.UTF8);
                var result = JsonConvert.DeserializeObject<SavedData>(serialized);
                if (result == null) throw new InvalidDataException();
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while reading main config {main}: {e.Message}");
                if (File.Exists(backup))
                {
                    try
                    {
                        Console.WriteLine($"Trying to read backup... {backup}");
                        var serialized = File.ReadAllText(backup, Encoding.UTF8);
                        var result = JsonConvert.DeserializeObject<SavedData>(serialized);
                        if (result == null) throw new InvalidDataException();
                        return result;
                    }
                    catch (Exception e2)
                    {
                        Console.WriteLine(
                            $"Error while reading backup config {backup}: {e2.Message}, will continue with default config");
                    }
                }
                else
                {
                    Console.WriteLine($"No backup config {backup}, will continue with default config");
                }
            }
        }

        return SavedData.DefaultConfig();
    }

    private static SavedData DefaultConfig()
    {
        return new SavedData();
    }

    public void SaveConfig()
    {
        SaveConfig(Main, Backup);
    }

    public void SaveConfig(string main, string backup)
    {
        new FileInfo(main).Directory?.Create();
        if (File.Exists(main))
        {
            File.Copy(main, backup, true);
        }
        File.WriteAllText(main, JsonConvert.SerializeObject(this));
    }
}