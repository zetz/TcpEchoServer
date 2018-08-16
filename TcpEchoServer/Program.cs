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
	/*
	 * Engine
	 *  - Modules
	 *   : NetworkModule
	 *   : CommandModule
	 *   : SessionModule
	 *   : ConsoleInputModule
	 *  | RequestJob(IJob job)
	*/

	public class Client {
		public Socket _socket;
		public Pipe _pipe;

		public static byte[] GetPayload(string message)
		{
			var utf8 = Encoding.UTF8;
			var bytes = utf8.GetBytes(message);
			var len = utf8.GetByteCount(message);
			var arr = new byte[len + 1];
			utf8.GetBytes(message, 0, message.Length, arr, 0);
			arr[arr.Length - 1] = (byte)'\n';
			return arr;
		}

		public void Send(string msg) {
			var bytes = GetPayload(msg);
			_socket.Send(bytes);
		}

		public override string ToString()
		{
			return $"[{_socket.RemoteEndPoint}]";
		}
	}


	class Program
	{
		private static List<TcpClient> _clients = new List<TcpClient>();
		
		static void Main(string[] args)
		{
			var tcpListener = new TcpListener(IPAddress.Loopback, 8087);

			Console.WriteLine("Listening on port 8087");
			try {
				tcpListener.Start();
				
			} catch (Exception e) {
				Console.WriteLine(e);
				return;
			}

			var inputTask = Task.Factory.StartNew(ReadInputs);
			var acceptTask = Task.Factory.StartNew(ListenSocket, tcpListener);

			Task.WaitAll(inputTask, acceptTask);

			Console.WriteLine("Shutdown ...");
		}

		private static void ReadInputs() {
			while (true) {
				var line = Console.ReadLine();
				var tokens = line.Split(" ");
				if (tokens.Length > 0) {
					var command = tokens[0];
					Console.WriteLine($"[{command}]");

					if (string.CompareOrdinal(command, "exit")== 0) {
						break;
					} else if (string.CompareOrdinal(command, "help")== 0) {
						Console.WriteLine($"press input command. [help, exit, sessions]");	
					} else if (string.CompareOrdinal(command, "clients") == 0) {
						lock (_clients) {
							foreach (var s in _clients) {
								Console.WriteLine(s);
							}
						}
					} else {
						lock (_clients) {
							foreach (var s in _clients) {
								//s.Send(line);
								var bytes = Client.GetPayload(line);
								s.GetStream().Write(bytes);
							}
						}
					}
				}
			}
		}

		private static async Task ListenSocket(object obj) {
			var listenSocket = (TcpListener)obj;
			while (true) {
				var socket = await listenSocket.AcceptTcpClientAsync();
				_ = ProcessLineAsync(socket);
			}
		}

		private static async Task ProcessLineAsync(TcpClient tcpClient)
		{
			Console.WriteLine($"[{tcpClient.Client.RemoteEndPoint}] : connected.");

			var pipe = new Pipe();

			Task writing = FillPipeAsync(tcpClient, pipe.Writer);
			Task reading = ReadPipeAsync(tcpClient, pipe.Reader);

			lock (_clients) {
				_clients.Add(tcpClient);
			}

			await Task.WhenAll(reading, writing);

			lock (_clients) {
				_clients.Remove(tcpClient);
			}
			Console.WriteLine($"[{tcpClient.Client.RemoteEndPoint}] : disconnected.");
		}

		private static async Task FillPipeAsync(TcpClient tcpClient, PipeWriter writer)
		{
			const int minimumBufferSize = 512;

			while (true) {
				try {
					// Request a minimum of 512 bytes from the PipeWriter
					Memory<byte> memory = writer.GetMemory(minimumBufferSize);

					int bytesRead = await tcpClient.Client.ReceiveAsync(memory, SocketFlags.None);
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

		private static async Task ReadPipeAsync(TcpClient tcpClient, PipeReader reader)
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
						ProcessLine(tcpClient, line);


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

		private static void ProcessLine(TcpClient tcpClient, in ReadOnlySequence<byte> buffer)
		{
			Socket socket = tcpClient.Client;

			Console.WriteLine($"[{socket.RemoteEndPoint}]: ");
			foreach (var segment in buffer) {
				var msg = Encoding.UTF8.GetString(segment.Span);
				Console.Write(msg);

				tcpClient.GetStream().Write(segment.Span);
			}
			Console.WriteLine();
		}
	}
}
