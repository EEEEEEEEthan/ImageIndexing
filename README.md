# ImageIndexing 图片索引工具

一个基于AI的图片内容索引和搜索工具，使用大语言模型（LLM）自动生成图片描述，支持智能搜索。

## 功能特性

- **自动图片索引**: 扫描指定目录下的图片文件，使用AI生成内容描述
- **智能搜索**: 基于图片描述进行关键词搜索
- **多格式支持**: 支持 JPG、JPEG、PNG、BMP、GIF、WebP、TIF、TIFF 等常见图片格式
- **增量更新**: 仅处理新增或修改的图片文件（基于MD5校验）
- **本地缓存**: 生成的描述本地存储，提高搜索效率
- **可配置AI模型**: 支持自定义LLM API配置

## 系统要求

- .NET Framework 4.7.2 或更高版本
- Windows操作系统
- 网络连接（用于访问LLM API）

## 安装与配置

### 1. 编译项目

```bash
# 使用 Visual Studio 或 MSBuild 编译
msbuild ImageIndexing.sln /p:Configuration=Release
```

### 2. 配置API密钥

首次运行程序时会自动创建 `config.ini` 配置文件，请修改其中的API密钥：

```ini
[llm]
url=https://ark.cn-beijing.volces.com/api/v3/chat/completions
model=doubao-1-5-thinking-vision-pro-250428
apikey=YOUR_API_KEY
prompts=根据这张图里的内容,结合文件路径({path}),猜测其用途,然后抽象成120个以内的汉字,简述其中内容.可以是短句,也可以是逗号隔开的词或者短语.不要出现换行.不要说多余的话,也包括'图中是xxx'的开头都不要说.直接描述
```

**配置说明:**
- `url`: LLM API接口地址
- `model`: 使用的AI模型名称
- `apikey`: 替换为您的API密钥
- `prompts`: AI生成描述时使用的提示词模板

## 使用方法

### 建立图片索引

扫描目录并为图片生成AI描述：

```bash
# 扫描当前目录
ImageIndexing.exe update

# 指定根目录
ImageIndexing.exe update --root "C:\Pictures"

# 自定义数据文件位置
ImageIndexing.exe update --root "C:\Pictures" --data "C:\my_index.txt"

# 限制最大请求次数
ImageIndexing.exe update --root "C:\Pictures" --max 500
```

**参数说明:**
- `--root`: 要扫描的根目录（默认：当前目录）
- `--data`: 索引数据文件路径（默认：根目录下的 `.imageIndex`）
- `--max`: 最大API请求次数（默认：1000）

### 搜索图片

根据关键词搜索已索引的图片：

```bash
# 交互式搜索（会提示输入关键词）
ImageIndexing.exe search

# 直接指定搜索关键词
ImageIndexing.exe search "风景"
ImageIndexing.exe search --prompt "人物照片"

# 指定数据文件
ImageIndexing.exe search --data "C:\my_index.txt" "建筑"
```

**参数说明:**
- `--data`: 索引数据文件路径（默认：当前目录下的 `.imageIndex`）
- `--prompt`: 搜索关键词（也可作为位置参数传入）

### 查看帮助

```bash
ImageIndexing.exe help
# 或
ImageIndexing.exe -h
```

## 数据格式

索引数据以Tab分隔的文本格式存储（`.imageIndex`文件）：

```
文件路径[TAB]AI生成的描述[TAB]MD5校验值
```

示例：
```
photos\sunset.jpg	海边日落，金色阳光洒在海面上，远山轮廓，宁静美丽的自然风光	a1b2c3d4e5f6...
photos\family.jpg	家庭聚餐，餐桌上摆满美食，温馨的室内环境	f6e5d4c3b2a1...
```

## 工作原理

1. **索引阶段**: 
   - 递归扫描指定目录下的图片文件
   - 计算每个图片的MD5值，跳过已处理的文件
   - 将图片发送到LLM API生成中文描述
   - 将结果保存到本地索引文件

2. **搜索阶段**:
   - 优先在本地索引文件中进行关键词匹配
   - 如果本地没有匹配结果，则调用LLM API进行智能搜索
   - 返回匹配的图片文件路径

## 项目结构

```
ImageIndexing/
├── ImageIndexing/
│   ├── Program.cs          # 主程序逻辑
│   ├── LLMClient.cs        # LLM API客户端
│   ├── ImageIndexing.csproj # 项目文件
│   ├── config.ini          # 配置文件
│   └── Properties/
│       └── AssemblyInfo.cs
├── ImageIndexing.sln       # 解决方案文件
└── README.md              # 本文档
```

## 依赖库

- **Newtonsoft.Json 13.0.3**: JSON序列化/反序列化
- **System.Net.Http**: HTTP客户端
- **.NET Framework 4.7.2**: 运行时框架

## 注意事项

1. **API费用**: 每次图片分析都会调用LLM API，请注意控制使用量以避免产生过高费用
2. **网络要求**: 需要稳定的网络连接访问LLM API服务
3. **图片格式**: 确保图片文件格式受支持且文件完整
4. **存储空间**: 大量图片的索引文件可能占用较多磁盘空间
5. **处理时间**: 初次建立索引需要较长时间，建议分批处理大量图片

## 故障排除

### 常见问题

**Q: 提示 "API密钥无效" 或请求失败**
A: 请检查 `config.ini` 中的 `apikey` 是否正确配置

**Q: 某些图片处理失败**
A: 检查图片文件是否损坏，或格式是否受支持

**Q: 搜索没有结果**
A: 确认索引文件存在且包含数据，尝试使用不同的关键词

**Q: 程序运行缓慢**
A: 可以通过 `--max` 参数限制单次处理的图片数量

## 许可证

本项目版权所有 © 2025

## 更新日志

- **v1.0.0**: 初始版本，支持图片索引和搜索功能