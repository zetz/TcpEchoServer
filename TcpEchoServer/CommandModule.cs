using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TcpEchoServer
{
	public static class CommandModule
	{
		private static Dictionary<string, Action<string[]>> _commands = new Dictionary<string, Action<string[]>>();

		public static Task StartAsync() {
			AddCommand("help", Command_Help);
			return Task.Factory.StartNew(ReadInputTask);
		}

		private static void ReadInputTask()
		{
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
		}

		public static void AddCommand(string cmd, Action<string[]> callback) {
			_commands.Add(cmd.ToLower(), callback);	
		}

		private static void Command_Help(string[] args) {
			System.Console.WriteLine($"prease command input [{string.Join(", ", _commands.Keys)}]");
		}
	}
}
