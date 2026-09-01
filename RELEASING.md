# Releasing Model Peek

GitHub Actions is the release builder. Release binaries should not be committed
to the repository or copied from a developer workstation.

## Before tagging

1. Update `<Version>` in `ModelPeek.csproj`.
2. Run `scripts/Test-Privacy.ps1`.
3. Publish and test locally when the .NET 10 SDK is available:

   ```powershell
   dotnet publish .\ModelPeek.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish
   .\scripts\Test-Release.ps1 -ExecutablePath .\artifacts\publish\ModelPeek.exe
   ```

4. Confirm the GitHub Actions CI workflow passes on the exact commit to tag.
5. Review the commit and repository for personal information, credentials,
   proprietary assets, and license compatibility.

## Create a release

Create and push a signed or annotated version tag that exactly matches the
project version:

```powershell
git tag -a v0.1.0 -m "Model Peek v0.1.0"
git push origin v0.1.0
```

The release workflow builds on a fresh GitHub-hosted Windows runner, runs the
valid and malformed STL self-tests, produces `ModelPeek.exe`, creates a ZIP and
`SHA256SUMS.txt`, adds GitHub build-provenance attestations, and publishes the
release assets.

## Verify an artifact

Compare the SHA-256 digest, then use GitHub CLI to verify build provenance:

```powershell
Get-FileHash .\ModelPeek.exe -Algorithm SHA256
gh attestation verify .\ModelPeek.exe --repo OWNER/REPOSITORY
```

Replace `OWNER/REPOSITORY` with the public repository name.

## Code signing

Unsigned releases can trigger Microsoft Defender SmartScreen and may be blocked
by stricter Windows policies. GitHub attestations and checksums prove build
origin and integrity, but they do not replace Authenticode signing. Add a
protected code-signing step before calling a release broadly production-ready.
