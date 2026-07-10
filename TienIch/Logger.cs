using System;
using System.IO;
using System.Collections.Generic;

namespace Vetheprevit.TienIch
{
    public static class Logger
    {
        private static List<string> _warnings = new List<string>();

        public static void ClearWarnings()
        {
            _warnings.Clear();
        }

        public static void AddWarning(string message)
        {
            if (!_warnings.Contains(message))
            {
                _warnings.Add(message);
            }
        }

        public static List<string> GetWarnings()
        {
            return new List<string>(_warnings);
        }

        public static void LogError(string message, Exception ex = null, string familyInfo = "")
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vetheprevit", "Logs");
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string filePath = Path.Combine(folder, $"ErrorLog_{DateTime.Now:yyyyMMdd}.txt");
                using (StreamWriter sw = new StreamWriter(filePath, true))
                {
                    sw.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}");
                    if (!string.IsNullOrEmpty(familyInfo))
                    {
                        sw.WriteLine($"   Context/Family: {familyInfo}");
                    }
                    if (ex != null)
                    {
                        sw.WriteLine($"   Exception: {ex.Message}");
                        sw.WriteLine($"   StackTrace: {ex.StackTrace}");
                    }
                    sw.WriteLine(new string('-', 50));
                }
            }
            catch
            {
                // Fallback nếu ghi log thất bại (ví dụ lỗi quyền) thì đành chịu
            }
        }
    }
}
