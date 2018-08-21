using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChattingServer.Network
{
	public class SocketConnection
	{
		private Socket _socket;

		private static List<SocketConnection> _connections = new List<SocketConnection>();
		public static IReadOnlyList<SocketConnection> Connections => _connections.AsReadOnly();

		public SocketConnection(Socket socket)
		{
			_socket = socket;
		}

		private byte[] GetPayload(string message)
		{
			var utf8 = System.Text.Encoding.UTF8;
			var bytes = utf8.GetBytes(message);
			var len = utf8.GetByteCount(message);
			var arr = new byte[len + 1];
			utf8.GetBytes(message, 0, message.Length, arr, 0);
			arr[arr.Length - 1] = (byte)'\n';
			return arr;
		}

		public void Send(string msg)
		{
			var bytes = GetPayload(msg);
			_socket.Send(bytes);
		}

		public override string ToString()
		{
			return $"[{_socket.RemoteEndPoint}]";
		}

		public void Disconnect()
		{
			_socket.Disconnect(false);
		}

		public static void OnAccept(Socket socket)
		{
			var client = new SocketConnection(socket);
			_ = client.DoReceiveTask();
		}

		

		private async Task DoReceiveTask()
		{
			var pipe = new Pipe();

			Task writing = FillPipeAsync(_socket, pipe.Writer);
			Task reading = ReadPipeAsync(_socket, pipe.Reader);

			Console.WriteLine($"[{_socket.RemoteEndPoint}] : connected.");
			lock (_connections) {
				_connections.Add(this);
			}

			await Task.WhenAll(reading, writing);

			Console.WriteLine($"[{_socket.RemoteEndPoint}] : disconnected.");
			lock (_connections) {
				_connections.Remove(this);
			}
		}

		private async Task FillPipeAsync(Socket socket, PipeWriter writer)
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

		private async Task ReadPipeAsync(Socket socket, PipeReader reader)
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

		private void ProcessLine(Socket socket, in ReadOnlySequence<byte> buffer)
		{
			Console.WriteLine($"[{socket.RemoteEndPoint}]: ");

			var msg = GetAsciiString(buffer);
			Console.WriteLine(msg);

			// TODO
			socket.Send(buffer.First.Span);
		}

		private string GetAsciiString(ReadOnlySequence<byte> buffer)
		{
			if (buffer.IsSingleSegment) {
				return Encoding.ASCII.GetString(buffer.First.Span);
			}

			return string.Create((int)buffer.Length, buffer, (span, sequence) =>
			{
				foreach (var segment in sequence) {
					Encoding.ASCII.GetChars(segment.Span, span);

					span = span.Slice(segment.Length);
				}
			});
		}
	}
}
