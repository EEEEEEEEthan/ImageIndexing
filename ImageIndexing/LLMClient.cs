using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
namespace ImageIndexing
{
	class LLMClient
	{
		static string GetMimeType(string filePath)
		{
			var ext = Path.GetExtension(filePath).ToLowerInvariant();
			switch (ext)
			{
				case ".jpg":
				case ".jpeg":
					return "image/jpeg";
				case ".png":
					return "image/png";
				case ".webp":
					return "image/webp";
				case ".bmp":
					return "image/bmp";
				case ".gif":
					return "image/gif";
				default:
					return "application/octet-stream";
			}
		}
		readonly string apiKey;
		readonly string model;
		readonly string url;
		public LLMClient(string url, string model, string apiKey)
		{
			this.url = url;
			this.apiKey = apiKey;
			this.model = model;
		}
		public async Task<(bool success, string result)> Request(string prompt, string imageFileFullPath)
		{
			if (!File.Exists(imageFileFullPath)) return (false, "Image file not found.");
			try
			{
				var imageBytes = File.ReadAllBytes(imageFileFullPath);
				var imageBase64 = Convert.ToBase64String(imageBytes);
				var mimeType = GetMimeType(imageFileFullPath);
				var volcRequest = new
				{
					model,
					messages = new[]
					{
						new
						{
							role = "user",
							content = new object[]
							{
								new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{imageBase64}", }, },
								new { type = "text", text = prompt, },
							},
						},
					},
				};
				using (var client = new HttpClient())
				{
					client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
					client.DefaultRequestHeaders.Add("Accept", "application/json");
					var content = new StringContent(JsonConvert.SerializeObject(volcRequest), Encoding.UTF8, "application/json");
					var response = await client.PostAsync(url, content);
					var resp = await response.Content.ReadAsStringAsync();
					try
					{
						var obj = JsonConvert.DeserializeObject<dynamic>(resp);
						var contentValue = obj.choices[0].message.content;
						return (true, contentValue.ToString());
					}
					catch
					{
						return (false, resp);
					}
				}
			}
			catch (Exception ex)
			{
				return (false, ex.Message);
			}
		}
		public async Task<(bool success, string result)> RequestText(string prompt)
		{
			try
			{
				var volcRequest = new
				{
					model,
					messages = new[]
					{
						new
						{
							role = "user",
							content = new object[]
							{
								new { type = "text", text = prompt, },
							},
						},
					},
				};
				using (var client = new HttpClient())
				{
					client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
					client.DefaultRequestHeaders.Add("Accept", "application/json");
					var content = new StringContent(JsonConvert.SerializeObject(volcRequest), Encoding.UTF8, "application/json");
					var response = await client.PostAsync(url, content);
					var resp = await response.Content.ReadAsStringAsync();
					try
					{
						var obj = JsonConvert.DeserializeObject<dynamic>(resp);
						var contentValue = obj.choices[0].message.content;
						return (true, contentValue.ToString());
					}
					catch
					{
						return (false, resp);
					}
				}
			}
			catch (Exception ex)
			{
				return (false, ex.Message);
			}
		}
	}
}
