using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static SnakeGame.Config;

namespace SnakeGame
{
    public static class ConfigLoader
    {
        public static AppConfig Load(string relativePath = "Config/config.json")
        {
            string baseDir = AppContext.BaseDirectory;
            string fullPath = Path.Combine(baseDir, relativePath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Config file not found: {fullPath}");

            string json = File.ReadAllText(fullPath);
            return JsonSerializer.Deserialize<AppConfig>(json)
                   ?? throw new Exception("Failed to parse config.json");
        }
    }
}
