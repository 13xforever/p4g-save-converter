using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Persona4GoldenSaveConverter
{
	internal static class Program
	{
		private const string DefaultInputDir = @".\";
		private static readonly byte[] SlotHeader = Encoding.ASCII.GetBytes("SAVE0001");
		private static readonly byte[] MetaSalt = Encoding.ASCII.GetBytes("P4GOLDEN");

		static async Task<int> Main(string[] args)
		{
			var importDir = args.Length == 1 ? args[0] : DefaultInputDir;
			Console.Title = "Persona 4 Golden Save Data Converter";
			Console.WriteLine("Looking for decrypted PS Vita save data...");
			var metaPath = Path.Combine(importDir, "sce_sys", "sdslot.dat");
			if (!File.Exists(metaPath))
			{
				Console.WriteLine("Slot metadata file is missing, aborting");
				return -1;
			}

			var slotFiles = Directory.GetFiles(importDir, "*.bin");
			Console.WriteLine($"Found {slotFiles.Length} save slots (including system data)");

			var buf = new byte[0x34c];
			using var metaStream = File.Open(metaPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			for (var i = 0; i < 17; i++)
			{
				var dataName = i == 0 ? "system.bin" : $"data{i:0000}.bin";
				var dataPath = Path.Combine(importDir, dataName);
				metaStream.Seek(0x200 + i, SeekOrigin.Begin);
				if (metaStream.ReadByte() == 1 && File.Exists(dataPath))
				{
					Console.Write($"Importing {dataName}... ");
					int read = 0;
					do
					{
						metaStream.Seek(0x400 * (i + 1), SeekOrigin.Begin);
						read = await metaStream.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false);
						if (read < buf.Length)
						{
							var (left, top) = (Console.CursorLeft, Console.CursorTop);
							Console.Write("failed");
							Console.SetCursorPosition(left, top);
						}
					} while (read < buf.Length);

					using var md5 = MD5.Create();
					md5.TransformBlock(buf, 0, buf.Length, null, 0);
					md5.TransformFinalBlock(MetaSalt, 0, MetaSalt.Length);
					var metaHash = md5.Hash;
					
					md5.Initialize();
					using var dataStream = File.Open(dataPath, FileMode.Open, FileAccess.Read, FileShare.Read);
					var dataHash = md5.ComputeHash(dataStream);

					var binslotPath = dataPath + "slot";
					using var binslotStream = File.Open(binslotPath, FileMode.Create, FileAccess.Write, FileShare.Read);
					await binslotStream.WriteAsync(SlotHeader).ConfigureAwait(false);
					await binslotStream.WriteAsync(metaHash).ConfigureAwait(false); // another md5 of _something_
					await binslotStream.WriteAsync(dataHash).ConfigureAwait(false);
					await binslotStream.WriteAsync(buf).ConfigureAwait(false);
					await binslotStream.FlushAsync().ConfigureAwait(false);
					Console.WriteLine("ok");
				}
				else
				{
					Console.WriteLine($"Skipping {dataName}");
				}
			}
			Console.WriteLine("Done");
			return 0;
		}
	}
}
