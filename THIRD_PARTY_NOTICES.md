# Third-Party Notices

## Sound Cue Assets

This project bundles sound cue assets adapted from a GPLv3-licensed project by Beingpax:

- `src/LafazFlow.Windows/Resources/Sounds/recstart.mp3`
- `src/LafazFlow.Windows/Resources/Sounds/recstop.mp3`
- `src/LafazFlow.Windows/Resources/Sounds/pastess.mp3`
- `src/LafazFlow.Windows/Resources/Sounds/esc.wav`

Source owner: https://github.com/Beingpax

The upstream project is licensed under the GNU General Public License version 3. The GPLv3 license text is included in this repository as `LICENSE`.

## whisper.cpp

This project bundles or builds against [whisper.cpp](https://github.com/ggml-org/whisper.cpp), licensed under the MIT License:

- The persistent Whisper engine worker (`lafazflow-whisper-worker.exe`) is built from whisper.cpp revision `968eebe77225d25e57a3f981da7c696310f0e881` (the same source revision as the owner-local CUDA `whisper-cli.exe` below; M3 adopted it deliberately so the worker and the in-use CLI share identical source).
- Release packages include exactly one `whisper-cli.exe`, selected at packaging time:
  - **Local CUDA CLI** — when packaged with the owner-local CUDA build (`-WhisperCliLocalPath`), the package redistributes that binary, which is built from whisper.cpp revision `968eebe77225d25e57a3f981da7c696310f0e881` (May 2026).
  - **Official CPU CLI** — otherwise, the package includes the official whisper.cpp Windows binary release (`whisper-bin-x64.zip`) from the GitHub release; its release identity is recorded at packaging time.
- Every package embeds `LafazFlow-artifact-manifest.json`, which records which CLI source was selected, the source revision or release identity, and the SHA-256 of each shipped binary (`whisper-cli.exe`, `lafazflow-whisper-worker.exe`, `LafazFlow.Windows.exe`). That manifest is authoritative for the binaries in the package.

MIT License

Copyright (c) 2022-2025 Georgi Gerganov

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
