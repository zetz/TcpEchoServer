using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ChattingServer.Network
{
	public static class SocketListener
	{
		private static Socket _listenSocket;

		/// <summary>
		/// Start the specified port.
		/// </summary>
		/// <returns>listen task</returns>
		/// <param name="port">Port.</param>
		public static Task StartAsync(int port = 8087)
		{
			_listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
			_listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));

			try {
				_listenSocket.Listen(20);
			} catch (Exception e) {
				Console.WriteLine(e);
				return Task.CompletedTask;
			}

			Console.WriteLine($"Listening on port {port}");

			return Task.Factory.StartNew(ListenSocket, _listenSocket);
		}

		private static async Task ListenSocket(object obj)
		{
			var listenSocket = (Socket)obj;
			while (true) {
				var socket = await listenSocket.AcceptAsync();
				SocketConnection.OnAccept(socket);
			}
		}
	}
}