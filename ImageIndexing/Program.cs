using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace ImageIndexing
{
	struct ImageSummary
	{
		public static Dictionary<string, ImageSummary> GetSummaries(string dataFilePath)
		{
			var dict = new Dictionary<string, ImageSummary>(StringComparer.OrdinalIgnoreCase);
			try
			{
				if (!File.Exists(dataFilePath)) return dict;
				foreach (var line in File.ReadAllLines(dataFilePath))
				{
					var parts = line.Split('\t');
					if (parts.Length < 3) continue;
					var path = parts[0].Trim();
					var summary = parts[1].Trim();
					var md5 = parts[2].Trim();
					if (string.IsNullOrEmpty(md5) || string.IsNullOrEmpty(summary)) continue;
					if (dict.ContainsKey(md5)) continue;
					dict[md5] = new ImageSummary
					{
						filePath = path,
						md5 = md5,
						summary = summary,
					};
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error reading summaries from {dataFilePath}: {ex.Message}");
			}
			return dict;
		}
		public static void SaveSummaries(string dataFilePath, Dictionary<string, ImageSummary> summaries)
		{
			var builder = new StringBuilder();
			foreach (var kvp in summaries)
			{
				var summary = kvp.Value;
				if (string.IsNullOrEmpty(summary.md5) || string.IsNullOrEmpty(summary.summary)) continue;
				builder.AppendLine($"{summary.filePath}\t{summary.summary}\t{summary.md5}");
			}
			try
			{
				File.WriteAllText(dataFilePath, builder.ToString());
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error saving summaries to {dataFilePath}: {ex.Message}");
			}
		}
		public string filePath;
		public string md5;
		public string summary;
	}
	class Program
	{
		const string defaultDataFile = ".imageIndex";
		const int maxRequests = 1000;
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
			//Console.WriteLine(string.Join(" ", args));
			if (args != null && args.Length > 0)
			{
				var cmd = args[0].ToLowerInvariant();
				switch (cmd)
				{
					case "update":
					{
						var root = Directory.GetCurrentDirectory();
						var dataFile = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + defaultDataFile;
						var maxRequests = Program.maxRequests;
						for (var i = 1; i < args.Length; i++)
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
							else
							{
								root = Path.GetFullPath(a);
							}
						}
						UpdateIndex(root, dataFile, maxRequests, () => finished = true);
						break;
					}
					case "search":
					{
						string prompt = null;
						var dataFile = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + defaultDataFile;
						for (var i = 1; i < args.Length; i++)
						{
							var a = args[i];
							if (a == "--prompt" && i + 1 < args.Length)
							{
								prompt = args[++i];
							}
							else if (a == "--data" && i + 1 < args.Length)
							{
								dataFile = Path.GetFullPath(args[++i]);
							}
							else if (a == "--help" || a == "-h")
							{
								PrintUsage();
								finished = true;
								break;
							}
							else
							{
								prompt = a;
							}
						}
						if (string.IsNullOrWhiteSpace(prompt))
						{
							Console.Write("Enter prompt: ");
							prompt = Console.ReadLine();
						}
						Search(prompt, dataFile, () => finished = true);
						break;
					}
					case "help":
					case "-h":
						PrintUsage();
						finished = true;
						break;
					default:
						throw new ArgumentException($"Unknown command: {cmd}");
				}
			}
			else
			{
				throw new ArgumentException("No command provided. Use 'help' to see usage.");
			}
			while (!finished) Thread.Sleep(100);
			//Console.ReadKey();
		}
		static void PrintUsage()
		{
			Console.WriteLine("Usage:");
			Console.WriteLine("  update [--root <path>] [--data <file>] [--max <n>]    建立或更新索引");
			Console.WriteLine("    --root: 根目录 (可选, 默认当前目录)");
			Console.WriteLine("    --data: 数据文件路径 (可选, 默认: 根目录下的 .imageIndex)");
			Console.WriteLine("    --max:  最多请求次数 (可选, 默认: 1000)");
			Console.WriteLine();
			Console.WriteLine("  search [--data <file>] [--prompt <提示词>] <prompt?>    查询");
			Console.WriteLine("    --data:  数据文件路径 (可选, 默认: 当前目录下的 .imageIndex)");
			Console.WriteLine("    --prompt: 提示词 (可选, 若未提供将交互输入; 也可直接作为位置参数)");
			Console.WriteLine();
			Console.WriteLine("  help|-h                                            显示此帮助");
			Console.WriteLine();
			Console.WriteLine("留出扩展: 可在命令行中加入 --concurrency, --verbose 等选项");
		}
		static async void Search(string prompts, string dataFilePath, Action callback)
		{
			try
			{
				// First try local search using summaries file
				if (File.Exists(dataFilePath))
				{
					var summaries = ImageSummary.GetSummaries(dataFilePath);
					var results = new List<string>();
					var q = prompts?.Trim();
					if (!string.IsNullOrEmpty(q))
					{
						foreach (var kv in summaries)
						{
							if (kv.Value.summary != null && kv.Value.summary.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
							{
								results.Add(kv.Value.filePath);
							}
						}
					}
					if (results.Count > 0)
					{
						foreach (var r in results) Console.WriteLine(r);
						callback.Invoke();
						return;
					}
				}
				// Fallback to LLM query when no local matches
				var (success, result) = await client.RequestText("请根据以下提示词查询相关图片,结果每行一个路径,不要说多余的话: " + prompts);
				Console.WriteLine(result);
				callback.Invoke();
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				throw e;
			}
		}
		static async void UpdateIndex(string rootPath, string dataFilePath, int maxRequests, Action callback)
		{
			try
			{
				var summaries = ImageSummary.GetSummaries(dataFilePath);
				var newSummaries = new Dictionary<string, ImageSummary>(StringComparer.OrdinalIgnoreCase);
				Console.WriteLine("Starting image indexing...");
				var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
				{
					".jpg",
					".jpeg",
					".png",
					".bmp",
					".gif",
					".webp",
					".tif",
					".tiff",
				};
				var list = new List<string>();
				foreach (var file in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
				{
					if (!exts.Contains(Path.GetExtension(file))) continue;
					list.Add(file);
				}
				for (var i = 0; i < list.Count; ++i)
				{
					var file = list[i];
					if (!exts.Contains(Path.GetExtension(file))) continue;
					try
					{
						var currentMd5 = System.Security.Cryptography.MD5.Create().ComputeHash(File.ReadAllBytes(file));
						var md5String = BitConverter.ToString(currentMd5).Replace("-", "").ToLower();
						Console.Write($"{i + 1}/{list.Count} {file} ");
						if (summaries.TryGetValue(md5String, out var existingSummary))
						{
							newSummaries[md5String] = existingSummary;
							Console.WriteLine($"Skipped, {existingSummary.summary}");
							continue;
						}
						var (success, result) = await Summary(file);
						if (success)
						{
							result = result.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
							newSummaries[md5String] = new ImageSummary
							{
								filePath = GetRelativePath(rootPath, file),
								summary = result,
								md5 = md5String,
							};
							ImageSummary.SaveSummaries(dataFilePath, newSummaries);
							Console.WriteLine($"Success, {result}");
						}
						else
						{
							Console.WriteLine($"Failed: {result}.");
							Console.WriteLine("Aborting further requests.");
							break;
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Error processing {file}: {ex.Message}");
					}
					if (i >= maxRequests)
					{
						Console.WriteLine($"Reached max requests limit of {maxRequests}. Stopping.");
						break;
					}
				}
				Console.WriteLine("Indexing complete. Saving summaries...");
				ImageSummary.SaveSummaries(dataFilePath, newSummaries);
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
				(success, result) = await client.RequestImage(prompts, filePath);
				if (success) break;
				await Task.Delay(1000);
			}
			return (success, result);
		}

		// 兼容旧框架的相对路径实现
		static string GetRelativePath(string relativeTo, string path)
		{
			try
			{
				if (string.IsNullOrEmpty(relativeTo)) return path;
				var fromPath = AppendDirectorySeparatorChar(Path.GetFullPath(relativeTo));
				var toPath = Path.GetFullPath(path);
				var fromUri = new Uri(fromPath);
				var toUri = new Uri(toPath);
				var relativeUri = fromUri.MakeRelativeUri(toUri);
				var relativePath = Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
				return relativePath;
			}
			catch
			{
				return path;
			}
		}
		static string AppendDirectorySeparatorChar(string path)
		{
			if (string.IsNullOrEmpty(path)) return Path.DirectorySeparatorChar.ToString();
			if (path.EndsWith(Path.DirectorySeparatorChar.ToString()) || path.EndsWith(Path.AltDirectorySeparatorChar.ToString())) return path;
			return path + Path.DirectorySeparatorChar;
		}
	}
}
