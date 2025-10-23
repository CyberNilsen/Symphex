import os
import platform
import subprocess

# Detect current platform
system = platform.system()
arch = platform.machine()

# Map to .NET runtime
runtime_map = {
    ("Windows", "AMD64"): "win-x64",
    ("Linux", "x86_64"): "linux-x64",
    ("Darwin", "x86_64"): "osx-x64",
    ("Darwin", "arm64"): "osx-arm64"
}

runtime = runtime_map.get((system, arch))
if not runtime:
    print(f"❌ Unsupported platform: {system} {arch}")
    exit(1)

# Find .csproj file
def find_csproj():
    for root, dirs, files in os.walk("."):
        for file in files:
            if file.endswith(".csproj"):
                return os.path.join(root, file)
    return None

csproj_path = find_csproj()
if not csproj_path:
    print("❌ No .csproj file found.")
    exit(1)

# Build command
output_dir = os.path.join(os.path.dirname(csproj_path), "publish", runtime)
cmd = [
    "dotnet", "publish", csproj_path,
    "-c", "Release",
    "-r", runtime,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-o", output_dir
]

print(f"📦 Publishing for {runtime}...")
result = subprocess.run(cmd)
if result.returncode == 0:
    print(f"✅ Build complete: {output_dir}")
else:
    print(f"❌ Build failed with code {result.returncode}")
