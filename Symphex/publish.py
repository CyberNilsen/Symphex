import os
import platform
import subprocess

# CONFIGURATION
root_dir = os.path.abspath(os.path.dirname(__file__))
configuration = "Release"

# Platform-to-runtime mapping
runtime_map = {
    ("Windows", "AMD64"): "win-x64",
    ("Linux", "x86_64"): "linux-x64",
    ("Darwin", "x86_64"): "osx-x64",
    ("Darwin", "arm64"): "osx-arm64"
}

def find_csproj(start_dir):
    for root, dirs, files in os.walk(start_dir):
        for file in files:
            if file.endswith(".csproj"):
                return os.path.join(root, file)
    return None

def publish(runtime, csproj_path):
    print(f"\n📦 Publishing for runtime: {runtime}")
    output_dir = os.path.join(os.path.dirname(csproj_path), "publish", runtime)
    cmd = [
        "dotnet", "publish", csproj_path,
        "-c", configuration,
        "-r", runtime,
        "--self-contained", "true",
        "-o", output_dir
    ]
    result = subprocess.run(cmd)
    if result.returncode == 0:
        print(f"✅ Build complete: {output_dir}")
    else:
        print(f"❌ Build failed.")

def main():
    system = platform.system()
    arch = platform.machine()
    print(f"🖥️ Host OS: {system}, Architecture: {arch}")

    runtime = runtime_map.get((system, arch))
    if not runtime:
        print("❌ Unsupported platform.")
        return

    csproj_path = find_csproj(root_dir)
    if not csproj_path:
        print("❌ No .csproj file found.")
        return

    publish(runtime, csproj_path)

if __name__ == "__main__":
    main()
