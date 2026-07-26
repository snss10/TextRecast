# Third-party notices

TextRecast uses the following third-party components. These components remain subject to their respective licenses; the TextRecast Apache-2.0 license does not replace those terms.

## NuGet dependencies

The following packages declare the MIT License in their NuGet metadata:

| Package | Version | Authors or project |
| --- | --- | --- |
| CommunityToolkit.HighPerformance | 8.4.2 | Microsoft / .NET Community Toolkit |
| LLamaSharp | 0.27.0 | LLamaSharp contributors |
| LLamaSharp.Backend.Cpu | 0.27.0 | LLamaSharp and llama.cpp contributors |
| MahApps.Metro.IconPacks.Core | 6.2.1 | Jan Karger / MahApps.Metro.IconPacks |
| MahApps.Metro.IconPacks.Lucide | 6.2.1 | Jan Karger / MahApps.Metro.IconPacks |
| Microsoft.Bcl.AsyncInterfaces | 10.0.5 | Microsoft |
| Microsoft.Bcl.Memory | 10.0.5 | Microsoft |
| Microsoft.Extensions.AI.Abstractions | 10.4.1 | Microsoft |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.5 | Microsoft |
| Microsoft.Extensions.Logging.Abstractions | 10.0.5 | Microsoft |
| System.Interactive.Async | 7.0.0 | .NET Foundation and contributors |
| System.Linq.Async | 7.0.0 | .NET Foundation and contributors |
| System.Numerics.Tensors | 10.0.5 | Microsoft |

### MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Default SLM model

TextRecast downloads or optionally redistributes `Qwen2.5-1.5B-Instruct-GGUF`, which is provided by the Qwen team under the Apache License 2.0.

- Model: `Qwen2.5-1.5B-Instruct-GGUF`
- Quantization: `Q4_K_M`
- Source: <https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF>
- License: Apache-2.0

Review the upstream model card and license before redistributing the model binary.
