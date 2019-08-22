using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JsonArrayStream
{
	public static class JsonArrayDeserializer {
		public static async IAsyncEnumerable<T> ReadArray<T>(Stream stream, JsonSerializerOptions options = null, [EnumeratorCancellation] CancellationToken token = default) {
			options ??= new JsonSerializerOptions {
				AllowTrailingCommas = true,
				DictionaryKeyPolicy = null,
				PropertyNamingPolicy = null,
			};
			var pipe = new Pipe();
			var writing = FillPipeAsync(stream, pipe.Writer, token);
			await foreach (var item in ReadPipeAsync<T>(pipe.Reader, options, token)) {
				yield return item;
			}
			await writing;
		}
			
		private static async Task FillPipeAsync(Stream stream, PipeWriter writer, CancellationToken token = default) {
			const int MinimumBufferSize = 512;
			Exception exception = null;
			while (true) {
				try {
					var memory = writer.GetMemory(MinimumBufferSize);
					var read = await stream.ReadAsync(memory, token);
					if (read == 0) break;
					writer.Advance(read);

					var result = await writer.FlushAsync(token);
					if (result.IsCompleted) break;
				} catch (Exception ex) {
					exception = ex;
				}
			}
			writer.Complete(exception);
		}
		static async IAsyncEnumerable<T> ReadPipeAsync<T>(PipeReader reader, JsonSerializerOptions options = null, [EnumeratorCancellation] CancellationToken token = default) {			
			while (true) {
			NextObject:
				var result = await reader.ReadAsync(token);
				var buffer = result.Buffer;
				// Should check for [ here and verify all characters except [ before { are whitespace
				var array = buffer.PositionOf((byte)']');
				var open = buffer.PositionOf((byte)'{');
				if (open is null) {
					if (!(array is null)) {
						// need more checks here
						reader.AdvanceTo(array.Value);
						if (result.IsCompleted)
							yield break;
					}
					reader.AdvanceTo(buffer.Start, buffer.End);
					continue;
				}
				// start searching for } right after {
				var close = open;
				while (true) {
					close = buffer.Slice(close.Value).PositionOf((byte)'}');
					if (close is null) {
						reader.AdvanceTo(buffer.Start, buffer.End);
						continue;
					}
					var end = buffer.GetPosition(1, close.Value);
					var slice = buffer.Slice(open.Value, end);
					if (TryDeserialize(slice, out T value, options)) {
						yield return value;
						buffer = buffer.Slice(buffer.GetPosition(1, close.Value));
						reader.AdvanceTo(buffer.Start, buffer.Start);
						goto NextObject;
					} else {
						// skip this } and search the next }
						close = buffer.GetPosition(1, close.Value);
					}
				}
			}
		}
		static bool TryDeserialize<T>(ReadOnlySequence<byte> bytes, out T result, JsonSerializerOptions options) {
			var reader = new Utf8JsonReader(bytes, new JsonReaderOptions {
				AllowTrailingCommas = options.AllowTrailingCommas,
				CommentHandling = options.ReadCommentHandling,
				MaxDepth = options.MaxDepth
			});			
#if DEBUG
			var temp = Encoding.UTF8.GetString(bytes.ToArray());
#endif
			return /*JsonSerializer.*/TryDeserialize<T>(ref reader, out result, options);
		}
		static bool TryDeserialize<T>(ref Utf8JsonReader reader, out T result, JsonSerializerOptions options) {
			try {
				result = JsonSerializer.Deserialize<T>(ref reader, options);
				return true;
			} catch {
				result = default;
				return false;
			}
		}
    }
}
