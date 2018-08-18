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
		private static List<SocketConnection> _clients = new List<SocketConnection>();

		static void Main(string[] args)
		{
			//
			SocketListener.OnConnected = (conn) => {
				lock (_clients) {
					_clients.Add(conn);
				}
			};

			SocketListener.OnDisconnected = (conn) => {
				lock (_clients) {
					_clients.Remove(conn);
				}
			};


			if (SocketListener.Start(8087) == false) {
				Console.WriteLine($"unable to open port 8087");
				return;

			}

			//
			CommandModule.AddCommand("clients", _ => {
				lock (_clients) {
					foreach (var s in _clients) {
						Console.WriteLine(s);
					}
				}
			});
			CommandModule.AddCommand("say", _ => {
				lock (_clients) {
					foreach (var s in _clients) {
						s.Send(string.Join(' ', _));
					}
				}
			});

			CommandModule.AddCommand("kill", _ => {
				if (_.Length > 1) {
					var target = _[1];
					if (target == "all") {
						lock (_clients) {
							foreach (var s in _clients){
								s.Disconnect();
							}
						}
					} else {
						lock (_clients) {
							foreach (var s in _clients) {
								if (s.ToString().Contains(target)) {
									s.Disconnect();
									break;
								}
							}
						}
					}
				}
			});
			CommandModule.Init();

			Task.WaitAll(CommandModule.InputTask, SocketListener.AcceptTask);

			Console.WriteLine("Shutdown ...");
		}
	}
}
