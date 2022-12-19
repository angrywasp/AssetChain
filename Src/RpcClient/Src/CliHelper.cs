using System;
using AngryWasp.Cli;

namespace RpcClient
{
    public static class CliHelper
    {
        public static bool Begin()
        {
            Application.LogBufferPaused = true;
            Application.UserInputPaused = true;

            return true;
        }

        public static bool Complete(string exitError = null)
        {
            Application.LogBufferPaused = false;
            Application.UserInputPaused = false;
            if (exitError != null)
                WriteError(exitError);
            return true;
        }

        public static void Write(string value) =>
            ApplicationLogWriter.WriteImmediate(value);

        public static void Write(string value, ConsoleColor color) =>
            ApplicationLogWriter.WriteImmediate(value, color);

        public static void WriteWarning(string value) =>
            ApplicationLogWriter.WriteImmediate($"Warning: {value}{Environment.NewLine}", ConsoleColor.Yellow);

        public static void WriteError(string value) =>
            ApplicationLogWriter.WriteImmediate($"Error: {value}{Environment.NewLine}", ConsoleColor.Red);
    }
}