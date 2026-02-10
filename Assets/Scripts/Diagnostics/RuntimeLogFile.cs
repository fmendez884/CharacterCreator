using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class RuntimeLogFile
{
    private static readonly object Sync = new();
    private static string logFilePath;
    private static bool initialized;

    public static string LogFilePath => logFilePath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (initialized)
            return;

        initialized = true;

        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string logDirectory = Path.Combine(projectRoot, "Logs");
            Directory.CreateDirectory(logDirectory);

            logFilePath = Path.Combine(logDirectory, "roo-runtime.log");
            File.WriteAllText(logFilePath, $"=== Runtime log started {DateTime.UtcNow:O} (UTC) ==={Environment.NewLine}");

            Application.logMessageReceivedThreaded += HandleLog;
            Debug.Log($"[RuntimeLogFile] Writing runtime logs to '{logFilePath}'.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RuntimeLogFile] Failed to initialize file logging: {ex}");
        }
    }

    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
            return;

        var timestamp = DateTime.UtcNow.ToString("O");
        var builder = new StringBuilder(256);
        builder.Append(timestamp).Append(" [").Append(type).Append("] ").Append(condition);

        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            builder.AppendLine();
            builder.Append(stackTrace);
        }

        WriteLine(builder.ToString());
    }

    private static void WriteLine(string message)
    {
        try
        {
            lock (Sync)
            {
                File.AppendAllText(logFilePath, message + Environment.NewLine);
            }
        }
        catch
        {
            // Ignore file I/O errors to avoid breaking play mode.
        }
    }
}
