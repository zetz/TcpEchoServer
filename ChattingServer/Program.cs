using System;

namespace ChattingServer
{
	using System.Threading.Tasks;
	using Network;


	class Program
	{
		static void Main(string[] args)
		{
			var listenTask = SocketListener.StartAsync(8087);
			if (listenTask == Task.CompletedTask) {
				Console.WriteLine($"unable to open port 8087");
				return;
			}

			//
			CommandModule.AddCommand("clients", tokens => {
				var clients = SocketConnection.Connections;
				lock (clients) {
					foreach (var s in clients) {
						Console.WriteLine(s);
					}
				}
			});
			CommandModule.AddCommand("say", tokens => {
				var clients = SocketConnection.Connections;
				lock (clients) {
					foreach (var s in clients) {
						s.Send(string.Join(' ', tokens));
					}
				}
			});

			CommandModule.AddCommand("kill", tokens => {
				if (tokens.Length > 1) {
					var clients = SocketConnection.Connections;
					var target = tokens[1];
					if (target == "all") {
						lock (clients) {
							foreach (var s in clients) {
								s.Disconnect();
							}
						}
					} else {
						lock (clients) {
							foreach (var s in clients) {
								if (s.ToString().Contains(target)) {
									s.Disconnect();
									break;
								}
							}
						}
					}
				}
			});

			var inputTask = CommandModule.StartAsync();

			Task.WaitAll(inputTask, listenTask);

			Console.WriteLine("Shutdown ...");
		}
	}
}
