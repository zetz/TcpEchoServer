using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TcpEchoServer
{
	public static class CommandModule
	{
		public static Task InputTask;
		private static Dictionary<string, Action<string[]>> _commands = new Dictionary<string, Action<string[]>>();


		public static bool Init() {
			AddCommand("help", Command_Help);
			InputTask = Task.Factory.StartNew(ReadInputTask);
			return true;
		}

		private static void ReadInputTask()
		{
			//System.Console.WriteLine($"ReadInputTask started.");
			while (true) {
				var line = System.Console.ReadLine();
				var tokens = line.Split(" ");
				if (tokens.Length > 0) {
					var command = tokens[0].ToLower();
					if (command == "exit") {
						break;
					}

					if (_commands.TryGetValue(command, out var action)) {
						action(tokens);
					}
				}
			}
			//System.Console.WriteLine($"ReadInputTask finished.");
		}

		public static void AddCommand(string cmd, Action<string[]> callback) {
			_commands.Add(cmd.ToLower(), callback);	
		}

		private static void Command_Help(string[] args) {
			System.Console.WriteLine($"prease command input [{string.Join(',', _commands.Keys)}]");
		}
	}
}
