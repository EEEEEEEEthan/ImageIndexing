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
			string path;
			// 如果args里有path,就用path,否则用当前目录
			// path转换为绝对路径
			Start(() => finished = true);
			while (!finished) Thread.Sleep(100);
		}
		static async void Start(string rootPath, Action callback)
		{
			try
			{
				Console.WriteLine("Starting image indexing...");
				var (success, result) = await Summary(@"D:\Projects\Kingdom\Kingdom\Assets\Game\Resources\Textures\Icon16.png");
				Console.WriteLine(result);
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
