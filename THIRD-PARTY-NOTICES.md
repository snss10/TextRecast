# Third-party notices

TextRecast contains or uses the third-party components listed below. Each component remains subject to its own license; the Apache-2.0 license for TextRecast does not replace those terms.

## NuGet dependencies

The following production dependencies declare the MIT License in their NuGet package metadata:

- Direct production package references: `LLamaSharp`, `LLamaSharp.Backend.Cpu`, and `MahApps.Metro.IconPacks.Lucide`.
- The remaining packages in the table are production transitive dependencies resolved by those packages.
- Test-only dependencies are not included because they are not distributed with the application or installer.

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

## Bundled .NET runtime packs

The self-contained Windows release includes Microsoft .NET runtime and Windows Desktop/WPF binaries. Their accompanying legal files are copied verbatim from the exact runtime packs selected during restore and are distributed beside the application as:

- `DOTNET-LICENSE.txt`
- `DOTNET-THIRD-PARTY-NOTICES.txt`
- `WPF-LICENSE.txt`

## Windows installer engine

The model-free Windows setup executable is built with NSIS 3.12. The NSIS
installer code and zlib compression module used by TextRecast are available
under the zlib/libpng license. The generated installer is build output; the
NSIS compiler and source distribution are not redistributed with TextRecast.

Copyright (C) 1999-2026 Contributors

This software is provided 'as-is', without any express or implied warranty.
In no event will the authors be held liable for any damages arising from the
use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not claim
   that you wrote the original software. If you use this software in a
   product, an acknowledgment in the product documentation would be
   appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.

Upstream project: <https://nsis.sourceforge.io/>

Upstream license: <https://github.com/kichik/nsis/blob/v3.12/COPYING>

## Lucide icon artwork

`MahApps.Metro.IconPacks.Lucide` incorporates icon data from the [Lucide project](https://github.com/lucide-icons/lucide). Lucide is licensed under the ISC License, and the Lucide icons derived from Feather retain the Feather MIT notice below.

TextRecast uses only the glyphs needed for launcher commands, rewrite operations, status, copy, and regeneration. The complete Lucide and Feather notices remain here because the distributed MahApps icon-pack assembly contains the broader icon data, not only the glyphs referenced by TextRecast.

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

## Optional local SLM models

TextRecast does not include a model in its normal release archive. After explicit user confirmation, it may separately download one of the following GGUF files. Each repository declares the Apache License 2.0; the model binary remains subject to its upstream terms and is not relicensed as part of TextRecast.

| Model and quantization | Pinned source | SPDX license |
| --- | --- | --- |
| Qwen 2.5 1.5B Instruct `Q4_K_M` | <https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/tree/dd26da440ef0330c47919d1ecae0966d24022222> | `Apache-2.0` |
| Qwen 3.5 2B `Q5_K_M` | <https://huggingface.co/unsloth/Qwen3.5-2B-GGUF/tree/f6d5376be1edb4d416d56da11e5397a961aca8ae> | `Apache-2.0` |
| Qwen 3.5 4B `Q5_K_M` | <https://huggingface.co/unsloth/Qwen3.5-4B-GGUF/tree/e87f176479d0855a907a41277aca2f8ee7a09523> | `Apache-2.0` |
| Granite 4.1 3B `Q5_K_M` | <https://huggingface.co/ibm-granite/granite-4.1-3b-GGUF/tree/ab4701481089b58a082ef63cc1cee738887293ff> | `Apache-2.0` |

Review the relevant upstream model card and license before redistributing a model binary. Exact filenames, sizes, and SHA-256 checksums are documented in `Models/README.md` and enforced by the application catalog.
