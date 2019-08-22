using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace JsonArrayStream
{
	public class UnitTest1
	{
		private const string JsonUnicode = @"
[
	{ ""Value"": 1 },
	{ ""Value"": 2 }
]
";
		private static readonly byte[] JsonUtf8 = Encoding.UTF8.GetBytes(JsonUnicode);
		private static MemoryStream CreateStream() => new MemoryStream(JsonUtf8, writable: false);

		private class Typed
		{
			public int Value { get; set; }
		}

		[Fact]
		public async Task Test1() {
			using var stream = CreateStream();
			await foreach (var item in JsonArrayDeserializer.ReadArray<Typed>(stream)) {
				Debug.WriteLine($"Value = {item?.Value.ToString() ?? "(null)"}");
			}
		}
	}
}
