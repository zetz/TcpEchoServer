using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TcpEchoServer.Network
{
	public static class SocketListener
	{
		public static Action<SocketConnection> OnConnected;
		public static Action<SocketConnection> OnDisconnected;
		public static Action<Socket> OnAccepted;

		public static Task AcceptTask;

		private static Socket _listenSocket;

		public static bool Start(int port = 8087)
		{
			_listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
			_listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));

			try {
				_listenSocket.Listen(120);
			} catch (Exception e) {
				Console.WriteLine(e);
				return false;
			}

			Console.WriteLine("Listening on port 8087");

			AcceptTask = Task.Factory.StartNew(ListenSocket, _listenSocket);

			return true;
		}


		private static async Task ListenSocket(object obj)
		{
			var listenSocket = (Socket)obj;
			while (true) {
				var socket = await listenSocket.AcceptAsync();
				OnAccepted?.Invoke(socket);
				_ = ProcessLineAsync(socket);
			}
		}

		private static async Task ProcessLineAsync(Socket socket)
		{
			Console.WriteLine($"[{socket.RemoteEndPoint}] : connected.");

			var pipe = new Pipe();

			Task writing = FillPipeAsync(socket, pipe.Writer);
			Task reading = ReadPipeAsync(socket, pipe.Reader);

			var client = new SocketConnection(socket);

			OnConnected?.Invoke(client);

			await Task.WhenAll(reading, writing);

			OnDisconnected?.Invoke(client);

			Console.WriteLine($"[{socket.RemoteEndPoint}] : disconnected.");
		}

		private static async Task FillPipeAsync(Socket socket, PipeWriter writer)
		{
			const int minimumBufferSize = 512;

			while (true) {
				try {
					// Request a minimum of 512 bytes from the PipeWriter
					Memory<byte> memory = writer.GetMemory(minimumBufferSize);

					int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None);
					if (bytesRead == 0) {
						break;
					}

					// Tell the PipeWriter how much was read
					writer.Advance(bytesRead);
				} catch {
					break;
				}

				// Make the data available to the PipeReader
				FlushResult result = await writer.FlushAsync();

				if (result.IsCompleted) {
					break;
				}
			}

			// Signal to the reader that we're done writing
			writer.Complete();
		}

		private static async Task ReadPipeAsync(Socket socket, PipeReader reader)
		{
			while (true) {
				ReadResult result = await reader.ReadAsync();

				ReadOnlySequence<byte> buffer = result.Buffer;
				SequencePosition? position = null;

				do {
					// Find the EOL
					position = buffer.PositionOf((byte)'\n');

					if (position != null) {

						var line = buffer.Slice(0, position.Value);
						ProcessLine(socket, line);


						// This is equivalent to position + 1
						var next = buffer.GetPosition(1, position.Value);

						// Skip what we're already processed including \n
						buffer = buffer.Slice(next);

					}

				} while (position != null);

				// We sliced the buffer until no more data could be processed
				// Tell the PipeReader how much we consumed and how much we left to process
				reader.AdvanceTo(buffer.Start, buffer.End);

				if (result.IsCompleted) {
					break;
				}
			}

			reader.Complete();
		}

		private static void ProcessLine(Socket socket, in ReadOnlySequence<byte> buffer)
		{
			Console.WriteLine($"[{socket.RemoteEndPoint}]: ");
			foreach (var segment in buffer) {
				var msg = Encoding.UTF8.GetString(segment.Span);
				Console.Write(msg);
				socket.Send(segment.Span);
			}
			Console.WriteLine();
		}
	}
}
