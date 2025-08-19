using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace ImageIndexing
{
	class Program
	{
		static readonly string prompts;
		static readonly LLMClient client;
		static Program()
		{
			const string iniPath = "config.ini";
			if (!File.Exists(iniPath))
				File.WriteAllText(iniPath,
					@"[llm]
		url=https://ark.cn-beijing.volces.com/api/v3/chat/completions
		model=doubao-1-5-thinking-vision-pro-250428
		apikey=YOUR_API_KEY
		prompts=这张图里的内容,你抽象成120个以内的汉字,简述其中内容.可以是短句,也可以是逗号隔开的词或者短语.不要出现换行.不要说多余的话,也包括'图中是xxx'的开头都不要说.直接描述
		");
			var ini = ReadIni(iniPath);
			var url = ini.TryGetValue("url", out var value) ? value : "";
			var model = ini.TryGetValue("model", out var value1) ? value1 : "";
			var apikey = ini.TryGetValue("apikey", out var value2) ? value2 : "";
			prompts = ini.TryGetValue("prompts", out var value3) ? value3 : "";
			client = new LLMClient(url, model, apikey);
		}
		static Dictionary<string, string> ReadIni(string path)
		{
			var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (!File.Exists(path)) return dict;
			foreach (var line in File.ReadAllLines(path))
			{
				var l = line.Trim();
				if (string.IsNullOrEmpty(l) || l.StartsWith("#") || l.StartsWith("[")) continue;
				var idx = l.IndexOf('=');
				if (idx > 0)
				{
					var key = l.Substring(0, idx).Trim();
					var val = l.Substring(idx + 1).Trim();
					dict[key] = val;
				}
			}
			return dict;
		}
		static void Main(string[] args)
		{
			var finished = false;
			Console.OutputEncoding = Encoding.UTF8;

			// 参数表（设计）
			// A 功能: 建立索引 (子命令: build 或 a)
			//   参数:
			//     --root <根目录>            (可选, 默认: 当前目录)
			//     --data <数据文件路径>      (可选, 默认: 根目录下的 .imageIndex 文件)
			//     --max <最多请求次数>      (可选, 默认: 1000)
			// B 功能: 查询 (子命令: search 或 b)
			//   参数:
			//     --prompt <提示词>          (必需或可通过交互输入)
			// 留出扩展: 未来可增加 --concurrency, --verbose 等通用选项

			if (args != null && args.Length > 0)
			{
				var cmd = args[0].ToLowerInvariant();
				if (cmd == "build" || cmd == "a")
				{
					string root = Directory.GetCurrentDirectory();
					string dataFile = null;
					int maxRequests = 1000;
					for (int i = 1; i < args.Length; i++)
					{
						var a = args[i];
						if (a == "--root" && i + 1 < args.Length)
						{
							root = Path.GetFullPath(args[++i]);
						}
						else if (a == "--data" && i + 1 < args.Length)
						{
							dataFile = Path.GetFullPath(args[++i]);
						}
						else if (a == "--max" && i + 1 < args.Length && int.TryParse(args[++i], out var m))
						{
							maxRequests = m;
						}
						else if (a == "--help" || a == "-h")
						{
							PrintUsage();
							finished = true;
							break;
						}
					}
					if (dataFile == null) dataFile = Path.Combine(root, ".imageIndex");
					BuildIndex(root, dataFile, maxRequests, () => finished = true);
				}
				else if (cmd == "search" || cmd == "b")
				{
					string prompt = null;
					for (int i = 1; i < args.Length; i++)
					{
						var a = args[i];
						if (a == "--prompt" && i + 1 < args.Length)
						{
							prompt = args[++i];
						}
						else if (a == "--help" || a == "-h")
						{
							PrintUsage();
							finished = true;
							break;
						}
					}
					if (string.IsNullOrWhiteSpace(prompt))
					{
						Console.Write("Enter prompt: ");
						prompt = Console.ReadLine();
					}
					Search(prompt, () => finished = true);
				}
				else if (cmd == "help" || cmd == "-h")
				{
					PrintUsage();
					finished = true;
				}
				else
				{
					// 兼容旧行为: 把第一个参数当作路径
					var path = !string.IsNullOrWhiteSpace(args[0]) ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
					Start(path, () => finished = true);
				}
			}
			else
			{
				// 无参数: 使用当前目录作为根路径进行 Start
				var path = Directory.GetCurrentDirectory();
				Start(path, () => finished = true);
			}

			while (!finished) Thread.Sleep(100);
		}

		static void PrintUsage()
		{
			Console.WriteLine("Usage:");
			Console.WriteLine("  build|a [--root <path>] [--data <file>] [--max <n>]    建立索引");
			Console.WriteLine("    --root: 根目录 (可选, 默认当前目录)");
			Console.WriteLine("    --data: 数据文件路径 (可选, 默认: 根目录下的 .imageIndex)");
			Console.WriteLine("    --max:  最多请求次数 (可选, 默认: 1000)");
			Console.WriteLine();
			Console.WriteLine("  search|b [--prompt <提示词>]                        查询");
			Console.WriteLine("    --prompt: 提示词 (可选, 若未提供将交互输入)");
			Console.WriteLine();
			Console.WriteLine("  help|-h                                            显示此帮助");
			Console.WriteLine();
			Console.WriteLine("留出扩展: 可在命令行中加入 --concurrency, --verbose 等选项");
		}
		static async void Search(string prompts, Action callback)
		{

		}
		static async void BuildIndex(string rootPath, string dataFilePath, int maxRequests, Action callback)
		{
			try
			{
				Console.WriteLine("Starting image indexing...");
				var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
				{
					".jpg",".jpeg",".png",".bmp",".gif",".webp",".tif",".tiff"
				};
				int total = 0, successCount = 0, failCount = 0;
				foreach (var file in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
				{
					if (!exts.Contains(Path.GetExtension(file))) continue;
					total++;
					Console.WriteLine($"Processing: {file}");
					try
					{
						var (success, result) = await Summary(file);
						if (success)
						{
							successCount++;
							Console.WriteLine($"OK: {file} => {result}");
						}
						else
						{
							failCount++;
							Console.WriteLine($"Failed: {file} => {result}");
						}
					}
					catch (Exception ex)
					{
						failCount++;
						Console.WriteLine($"Error processing {file}: {ex.Message}");
					}
				}
				Console.WriteLine($"Done. Total: {total}, Success: {successCount}, Fail: {failCount}");
				Console.ReadKey();
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error: {e.Message}");
			}
			finally
			{
				callback?.Invoke();
			}
		}
		static async Task<(bool success, string result)> Summary(string filePath)
		{
			var success = false;
			string result = null;
			for (var i = 0; i < 3; i++)
			{
				(success, result) = await client.Request(prompts, filePath);
				if (success) break;
				await Task.Delay(1000);
			}
			return (success, result);
		}
	}
}
