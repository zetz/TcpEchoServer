using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;



namespace TcpEchoServer
{
	using Network;
	/*
	 * Engine
	 *  - Modules
	 *   : NetworkModule
	 *   : CommandModule
	 *   : SessionModule
	 *  | RequestJob(IJob job)
	*/

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
