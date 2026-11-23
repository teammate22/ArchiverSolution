using System.IO;
using System.Text.Json;

namespace Archiver.Core
{
    /// <summary>
    /// Сохранение и загрузка состояния приложения в JSON-файл.
    /// </summary>
    public static class StateStorage
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static void Save(string filePath, AppState state)
        {
            string json = JsonSerializer.Serialize(state, Options);
            File.WriteAllText(filePath, json);
        }

        public static AppState Load(string filePath)
        {
            if (!File.Exists(filePath))
                return new AppState();

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<AppState>(json, Options) ?? new AppState();
            }
            catch
            {
                return new AppState();
            }
        }
    }
}
