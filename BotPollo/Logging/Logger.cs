using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotPollo.Logging
{
    public enum LogLevel
    {
        Info,
        Error,
        Warning,
        Fatal,
        Trace,
        All,
        AudioManager,
        Database,
        WebAPI
    }
    public class Logger
    {
        public async static Task Client_LogAsync(Discord.LogMessage arg) //Logging discord async
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("[Logging][Discord] ");
            Console.Write(arg + "\n");
            Console.ForegroundColor = ConsoleColor.White;
            return;
        }

        public static void Console_Log(String arg, LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Info:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    break;
                case LogLevel.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogLevel.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogLevel.Fatal:
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    break;
                case LogLevel.Trace:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
                case LogLevel.All:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogLevel.Database:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                case LogLevel.AudioManager:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
                case LogLevel.WebAPI:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    break;
            }
            Console.Write($"[Logging][{level}] {DateTime.Now.ToLocalTime().ToString("hh:mm:ss")} ");
            Console.Write(arg + "\n");
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
