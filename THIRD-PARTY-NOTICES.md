# Third-party notices

TextRecast contains or uses the third-party components listed below. Each component remains subject to its own license; the Apache-2.0 license for TextRecast does not replace those terms.

## NuGet dependencies

The following production dependencies declare the MIT License in their NuGet package metadata:

| Package | Version | Authors or project | License |
| --- | --- | --- | --- |
| CommunityToolkit.HighPerformance | 8.4.2 | Microsoft / .NET Community Toolkit | MIT |
| LLamaSharp | 0.27.0 | SciSharp STACK and LLamaSharp contributors | MIT |
| LLamaSharp.Backend.Cpu | 0.27.0 | llama.cpp authors | MIT |
| MahApps.Metro.IconPacks.Core | 6.2.1 | Jan Karger / MahApps.Metro.IconPacks | MIT |
| MahApps.Metro.IconPacks.Lucide | 6.2.1 | Jan Karger / MahApps.Metro.IconPacks | MIT |
| Microsoft.Bcl.AsyncInterfaces | 10.0.5 | Microsoft | MIT |
| Microsoft.Bcl.Memory | 10.0.5 | Microsoft | MIT |
| Microsoft.Extensions.AI.Abstractions | 10.4.1 | Microsoft | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.5 | Microsoft | MIT |
| Microsoft.Extensions.Logging.Abstractions | 10.0.5 | Microsoft | MIT |
| System.Interactive.Async | 7.0.0 | .NET Foundation and contributors | MIT |
| System.Linq.Async | 7.0.0 | .NET Foundation and contributors | MIT |
| System.Numerics.Tensors | 10.0.5 | Microsoft | MIT |

Copyright and attribution information from the package metadata:

- CommunityToolkit.HighPerformance: Copyright (c) .NET Foundation and Contributors. All rights reserved.
- LLamaSharp: SciSharp STACK 2026 and the LLamaSharp contributors.
- LLamaSharp.Backend.Cpu: Copyright 2023 The llama.cpp Authors. All rights reserved.
- MahApps.Metro.IconPacks.Core and MahApps.Metro.IconPacks.Lucide: Copyright (c) 2016-2025 MahApps.Metro.
- Microsoft.Bcl.AsyncInterfaces, Microsoft.Bcl.Memory, Microsoft.Extensions.AI.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions, and System.Numerics.Tensors: Copyright (c) Microsoft Corporation. All rights reserved.
- System.Interactive.Async and System.Linq.Async: Copyright (c) .NET Foundation and Contributors.

### MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Lucide icon artwork

`MahApps.Metro.IconPacks.Lucide` incorporates icon data from the [Lucide project](https://github.com/lucide-icons/lucide). Lucide is licensed under the ISC License, and the Lucide icons derived from Feather retain the Feather MIT notice below.

### Lucide ISC License

Copyright (c) 2026 Lucide Icons and Contributors

Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby granted, provided that the above copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

The following Lucide icons are derived from the Feather project:

airplay, alert-circle, alert-octagon, alert-triangle, aperture, arrow-down-circle, arrow-down-left, arrow-down-right, arrow-down, arrow-left-circle, arrow-left, arrow-right-circle, arrow-right, arrow-up-circle, arrow-up-left, arrow-up-right, arrow-up, at-sign, calendar, cast, check, chevron-down, chevron-left, chevron-right, chevron-up, chevrons-down, chevrons-left, chevrons-right, chevrons-up, circle, clipboard, clock, code, columns, command, compass, corner-down-left, corner-down-right, corner-left-down, corner-left-up, corner-right-down, corner-right-up, corner-up-left, corner-up-right, crosshair, database, divide-circle, divide-square, dollar-sign, download, external-link, feather, frown, hash, headphones, help-circle, info, italic, key, layout, life-buoy, link-2, link, loader, lock, log-in, log-out, maximize, meh, minimize, minimize-2, minus-circle, minus-square, minus, monitor, moon, more-horizontal, more-vertical, move, music, navigation-2, navigation, octagon, pause-circle, percent, plus-circle, plus-square, plus, power, radio, rss, search, server, share, shopping-bag, sidebar, smartphone, smile, square, table-2, tablet, target, terminal, trash-2, trash, triangle, tv, type, upload, x-circle, x-octagon, x-square, x, zoom-in, zoom-out

### Feather MIT License

Copyright (c) 2013-present Cole Bemis

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Default SLM model

TextRecast downloads `Qwen2.5-1.5B-Instruct-GGUF` separately when a valid model is not already installed or packaged. The model repository declares the Apache License 2.0; it is not relicensed as part of TextRecast.

- Model: `Qwen2.5-1.5B-Instruct-GGUF`
- Quantization used by TextRecast: `Q4_K_M`
- Source and model card: <https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF>
- Upstream license: <https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/blob/main/LICENSE>
- SPDX identifier: `Apache-2.0`

Review the upstream model card and license before redistributing the model binary.
