# Model Peek

Model Peek is a small, fast, read-only STL viewer for 64-bit Windows. It opens
ASCII and binary STL files locally and keeps the interface focused on inspecting
a model without editing it.

## Download

Download `ModelPeek.exe` from the [latest GitHub release](../../releases/latest).
It is a portable, self-contained application: no separate .NET installation or
setup program is required.

The initial preview releases are not code-signed, so Windows may identify the
publisher as unknown. Download only from this repository, compare the file's
SHA-256 value with `SHA256SUMS.txt`, and see [RELEASING.md](RELEASING.md) for
GitHub attestation verification. The ZIP also contains the applicable license,
privacy, and .NET third-party notices. A signed installer may be added later.

## Use

- Drag an STL file onto the window, or select **Open STL**.
- Left-drag to rotate.
- Right-drag or middle-drag to pan.
- Use the mouse wheel to zoom.
- Press `F` to fit the model.
- Press `1`, `2`, `3`, or `4` for isometric, front, top, or right view.
- Press `Ctrl+O` to open an STL.

Model Peek does not repair, smooth, rescale, slice, upload, or alter STL files.
Very large models can require substantial memory; the current safety limit is
5,000,000 triangles.

## Privacy

Model Peek has no telemetry, advertising, analytics, account system, automatic
updater, or network feature. STL processing stays on the computer. Read the
plain-language [privacy statement](PRIVACY.md) for details.

## Build from source

Requirements:

- Windows 10 or Windows 11 (x64)
- .NET 10 SDK

```powershell
dotnet publish .\ModelPeek.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish
.\scripts\Test-Release.ps1 -ExecutablePath .\artifacts\publish\ModelPeek.exe
```

Pushes and pull requests run the same build and self-tests through GitHub
Actions. Version tags such as `v0.1.0` create a GitHub Release with the portable
EXE, a ZIP copy, SHA-256 checksums, and build-provenance attestations.

## License

Model Peek source is available under the [MIT License](LICENSE). Self-contained
releases include Microsoft .NET components under their applicable terms; see
[third-party notices](THIRD-PARTY-NOTICES.md).
