using System;
using System.IO;
using System.Text.Json;
using MDPythonPatternMaker.Core.Config;

namespace MDPythonPatternMaker.Core.IO
{
    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch
            {
                // 読み込み失敗時はデフォルト設定を返す
            }
            return new AppConfig();
        }

        public static void Save(AppConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                throw new IOException("設定ファイルの保存中にエラーが発生しました。", ex);
            }
        }
    }
}
