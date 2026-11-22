# Third-Party Notices — Thetis

Thetis is distributed under the GNU General Public License, version 2
(root `LICENSE`). Richard Samphire's dual-licensing reservation for his own
contributions is reproduced in `LICENSE-DUAL-LICENSING`.

This file is the top-level third-party notice for the Thetis solution. It
covers the third-party native libraries in `Project Files\lib` and the NuGet
packages referenced by `Project Files\Source\Console\packages.config`.
Each package's own licence text and any `THIRD-PARTY-NOTICES.TXT` remain in
`Project Files\Source\packages\<id>.<version>\`.

## Native libraries

| Component | Version | Role | Licence | Licence text |
| --- | --- | --- | --- | --- |
| FFTW | 3.x Windows binaries | FFT | GPL-2.0-or-later | `Project Files\lib\fftw_x64\COPYING`, `fftw_x86\COPYING` |
| PortAudio | 19.7.0 | Audio input/output | MIT | `Project Files\lib\portaudio-19.7.0\LICENSE.txt` |
| WDSP | 1.29 | Signal-processing engine | GPL-2.0-or-later | `Project Files\Source\wdsp` (per-file headers) |
| libspecbleach | bundled source | NR4 noise reduction | LGPL-2.1-or-later | `Project Files\lib\NR_Algorithms_x64\src\libspecbleach\LICENSE` |
| rnnoise | bundled source | NR3 noise reduction | BSD-3-Clause | `Project Files\lib\NR_Algorithms_x64\src\rnnoise\COPYING` |
| SharpDX | 4.2.0 | Direct3D/Direct2D interop | MIT | `Project Files\lib\SharpDX`, `packages\SharpDX.4.2.0` |
| SkiaSharp | 3.119.2 | 2D graphics | MIT | `packages\SkiaSharp.3.119.2` |

## NuGet packages

The table records the licence declared by each package's `.nuspec`. All
packages are restored under `Project Files\Source\packages\<id>.<version>\`;
their licence files and any bundled notices are in those folders. Versions
are the ones pinned by `packages.config`.

| Package | Version | Licence |
| --- | --- | --- |
| Markdig | 1.2.0 | BSD-2-Clause |
| System.IO.Compression | 4.3.0 | MIT |
| System.IO.Compression.ZipFile | 4.3.0 | MIT |
| WindowsFirewallHelper | 2.2.0.86 | MIT |
| Microsoft.NETFramework.ReferenceAssemblies | 1.0.3 | MIT |
| Microsoft.NETFramework.ReferenceAssemblies.net35 | 1.0.3 | MIT |
| Microsoft.NETFramework.ReferenceAssemblies.net48 | 1.0.3 | MIT |
| Discord.Net | 3.18.0 | MIT |
| Discord.Net.Commands | 3.18.0 | MIT |
| Discord.Net.Core | 3.18.0 | MIT |
| Discord.Net.Interactions | 3.18.0 | MIT |
| Discord.Net.Rest | 3.18.0 | MIT |
| Discord.Net.Webhook | 3.18.0 | MIT |
| Discord.Net.WebSocket | 3.18.0 | MIT |
| ExCSS | 4.3.1 | MIT |
| HtmlAgilityPack | 1.12.4 | MIT |
| Microsoft.Bcl.AsyncInterfaces | 10.0.8 | MIT |
| Microsoft.Bcl.HashCode | 6.0.0 | MIT |
| Microsoft.Bcl.Memory | 10.0.8 | MIT |
| Microsoft.CodeAnalysis.Analyzers | 5.3.0 | MIT |
| Microsoft.CodeAnalysis.Common | 5.3.0 | MIT |
| Microsoft.CodeAnalysis.CSharp | 5.3.0 | MIT |
| Microsoft.CodeAnalysis.CSharp.Scripting | 5.3.0 | MIT |
| Microsoft.CodeAnalysis.Scripting.Common | 5.3.0 | MIT |
| Microsoft.CSharp | 4.7.0 | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.8 | MIT |
| Microsoft.NET.StringTools | 18.6.3 | MIT |
| Microsoft.Win32.Registry | 5.0.0 | MIT |
| NAudio | 2.3.0 | MIT |
| NAudio.Asio | 2.3.0 | MIT |
| NAudio.Core | 2.3.0 | MIT |
| NAudio.Midi | 2.3.0 | MIT |
| NAudio.Wasapi | 2.3.0 | MIT |
| NAudio.WinForms | 2.3.0 | MIT |
| NAudio.WinMM | 2.3.0 | MIT |
| Newtonsoft.Json | 13.0.4 | MIT |
| SkiaSharp | 3.119.2 | MIT |
| SkiaSharp.NativeAssets.macOS | 3.119.2 | MIT |
| SkiaSharp.NativeAssets.Win32 | 3.119.2 | MIT |
| System.Buffers | 4.6.1 | MIT |
| System.CodeDom | 10.0.8 | MIT |
| System.Collections.Immutable | 10.0.8 | MIT |
| System.Drawing.Common | 10.0.8 | MIT |
| System.Formats.Nrbf | 10.0.8 | MIT |
| System.Interactive.Async | 7.0.1 | MIT |
| System.Linq.Async | 7.0.1 | MIT |
| System.Linq.AsyncEnumerable | 10.0.8 | MIT |
| System.Memory | 4.6.3 | MIT |
| System.Numerics.Vectors | 4.6.1 | MIT |
| System.Reactive | 6.1.0 | MIT |
| System.Reflection.Metadata | 10.0.8 | MIT |
| System.Resources.Extensions | 10.0.8 | MIT |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 | MIT |
| System.Security.AccessControl | 6.0.1 | MIT |
| System.Security.Principal.Windows | 5.0.0 | MIT |
| System.Text.Encoding.CodePages | 10.0.8 | MIT |
| System.Threading.Tasks.Extensions | 4.6.3 | MIT |
| System.ValueTuple | 4.6.2 | MIT |
| Svg | 3.4.7 | MS-PL |
| FTD2XX.Net | 1.2.1 | FTDI proprietary (see `packages/FTD2XX.Net.1.2.1/License.txt`) |
| SharpDX | 4.2.0 | MIT |
| SharpDX.Desktop | 4.2.0 | MIT |
| SharpDX.Direct2D1 | 4.2.0 | MIT |
| SharpDX.Direct3D11 | 4.2.0 | MIT |
| SharpDX.DXGI | 4.2.0 | MIT |
| SharpDX.Mathematics | 4.2.0 | MIT |

### FTD2XX.Net

`FTD2XX.Net` is **not open source**. It is Future Technology Devices
International Limited's proprietary .NET wrapper, and its `License.txt`
restricts use to products based on FTDI parts. That condition is satisfied
by the FTDI-based hardware this project supports, but the FTDI licence text
must be retained and shipped with the package. It is not MIT and must not be
described as such.

## Licence and source compliance

- Thetis and WDSP-derived code are GPL-2.0-or-later; the root `LICENSE` is
  GPLv2. If you distribute Thetis binaries, provide the corresponding source
  as the GPL requires.
- FFTW is GPL-2.0-or-later; its `COPYING` file is in each FFTW library folder.
- libspecbleach is LGPL-2.1-or-later. It is shipped as a separate DLL
  (`specbleach.dll`); keep its licence notice and preserve the ability to
  replace or relink it as the LGPL requires.
- rnnoise is BSD-3-Clause; its `COPYING` file is in the rnnoise source folder.
- PortAudio is MIT; its `LICENSE.txt` is in the PortAudio source folder.
- SharpDX is MIT. It is an archived project but its licence remains MIT.
- SkiaSharp and its native assets are MIT.
- The Microsoft .NET support packages, NAudio, Discord.Net, Newtonsoft.Json,
  HtmlAgilityPack, ExCSS, WindowsFirewallHelper, System.* and Microsoft.* are
  MIT. Markdig is BSD-2-Clause. Svg is MS-PL.
- Package licence texts are not duplicated here; they remain in each
  package folder under `Project Files\Source\packages\`.

## No endorsement

Thetis is not affiliated with or endorsed by Apache Labs, FlexRadio Systems,
the OpenHPSDR project, or any of the authors and projects named above. The
names are recorded for attribution only.
