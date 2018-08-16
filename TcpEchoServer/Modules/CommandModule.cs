using System;
using System.Threading;
using System.Threading.Tasks;

namespace TcpEchoServer.Modules
{
	public class CommandModule
	{
		Task _inputTask;

		public CommandModule()
		{
			
		}

		public bool Init() {
			_inputTask = Task.Factory.StartNew(ReadInputTask);

			return true;
		}

		public void Update() {
			
		}

		public void Exit() {
			// _inputTask.Wait();
		}

		private void ReadInputTask()
		{
			Console.WriteLine($"ReadInputTask started.");
			while (true) {
				var line = Console.ReadLine();
				var tokens = line.Split(" ");
				if (tokens.Length > 0) {
					var command = tokens[0];
					Console.WriteLine($"[{command}]");

					if (string.CompareOrdinal(command, "exit") == 0) {
						break;
					}
				}
			}
			Console.WriteLine($"ReadInputTask finished.");
		}
	}
}
