#!/usr/bin/env python3
"""
Symphex Velopack Builder

Builds Velopack releases for Windows, macOS, and Linux.

Usage:
    python build_velopack.py --version 1.3.0
    python build_velopack.py --version 1.3.0 --all-platforms
"""

import os
import sys
import subprocess
import shutil
import platform
import argparse
from pathlib import Path

PROJECT_NAME = "Symphex"
GITHUB_REPO = "CyberNilsen/Symphex"

class Colors:
    GREEN = '\033[92m'
    RED = '\033[91m'
    BLUE = '\033[94m'
    CYAN = '\033[96m'
    YELLOW = '\033[93m'
    END = '\033[0m'
    BOLD = '\033[1m'

def print_header(msg):
    print(f"\n{Colors.BOLD}{Colors.CYAN}{'='*60}{Colors.END}")
    print(f"{Colors.BOLD}{Colors.CYAN}{msg}{Colors.END}")
    print(f"{Colors.BOLD}{Colors.CYAN}{'='*60}{Colors.END}\n")

def print_success(msg):
    print(f"{Colors.GREEN}✅ {msg}{Colors.END}")

def print_error(msg):
    print(f"{Colors.RED}❌ {msg}{Colors.END}")

def print_info(msg):
    print(f"{Colors.BLUE}ℹ️  {msg}{Colors.END}")

def print_warning(msg):
    print(f"{Colors.YELLOW}⚠️  {msg}{Colors.END}")

def run_command(cmd, cwd=None, check=True):
    """Run a command"""
    print_info(f"Running: {' '.join(str(c) for c in cmd)}")
    result = subprocess.run(cmd, cwd=cwd, capture_output=False, text=True)
    
    if result.returncode != 0 and check:
        print_error(f"Command failed with exit code {result.returncode}")
        sys.exit(1)
    
    return result

def find_csproj(start_dir):
    """Find .csproj file"""
    for file in Path(start_dir).rglob("*.csproj"):
        return str(file)
    return None

def get_vpk_command():
    """Get the vpk command path"""
    # Try direct command first
    try:
        result = subprocess.run(["vpk", "--help"], capture_output=True, text=True)
        if result.returncode == 0:
            return "vpk"
    except FileNotFoundError:
        pass
    
    # Try user dotnet tools path
    user_tools = Path.home() / ".dotnet" / "tools" / "vpk.exe"
    if user_tools.exists():
        return str(user_tools)
    
    print_error("Velopack CLI (vpk) not found!")
    print_info("Install it with: dotnet tool install -g vpk")
    print_info("After installation, restart your terminal and try again")
    sys.exit(1)

def check_vpk_installed(vpk_cmd):
    """Check if Velopack CLI (vpk) is installed"""
    result = subprocess.run([vpk_cmd, "--help"], capture_output=True, text=True)
    if result.returncode == 0:
        # Extract version from help output
        for line in result.stdout.split('\n'):
            if 'Velopack CLI' in line:
                print_success(f"Found: {line.strip()}")
                return
        print_success("Velopack CLI found")
    else:
        print_error("Velopack CLI check failed!")
        sys.exit(1)

def publish_app(csproj_path, runtime, version, output_dir):
    """Publish the app for a specific runtime"""
    print_info(f"Publishing for {runtime}...")
    
    cmd = [
        "dotnet", "publish", csproj_path,
        "-c", "Release",
        "-r", runtime,
        "--self-contained", "true",
        "-p:PublishSingleFile=false",  # Velopack needs unpacked files
        "-p:Version=" + version,
        "-o", str(output_dir)
    ]
    
    run_command(cmd)
    print_success(f"Published to {output_dir}")
    return output_dir

def pack_with_velopack(vpk_cmd, publish_dir, runtime, version, output_dir, icon_path=None):
    """Pack the published app with Velopack"""
    print_info(f"Packing with Velopack for {runtime}...")
    
    # Determine platform-specific settings
    if runtime.startswith("win"):
        platform_name = "win"
        # Use absolute path for icon
        if icon_path and icon_path.exists():
            icon_arg = ["-i", str(icon_path.absolute())]
        else:
            icon_arg = []
    elif runtime.startswith("osx"):
        platform_name = "osx"
        icon_arg = []  # macOS uses .icns
    else:
        platform_name = "linux"
        icon_arg = []
    
    cmd = [
        vpk_cmd, "pack",
        "-u", PROJECT_NAME,
        "-v", version,
        "-p", str(publish_dir),
        "-e", f"{PROJECT_NAME}.exe" if platform_name == "win" else PROJECT_NAME,
        "-o", str(output_dir),
    ] + icon_arg
    
    result = run_command(cmd, check=False)
    
    if result.returncode == 0:
        # Velopack creates files like: Symphex-win-Setup.exe
        setup_file = list(Path(output_dir).glob(f"{PROJECT_NAME}-*-Setup.exe"))
        if not setup_file:
            setup_file = list(Path(output_dir).glob(f"{PROJECT_NAME}-*Setup.exe"))
        if setup_file:
            print_success(f"Velopack package created: {setup_file[0].name}")
            return setup_file[0]
        else:
            print_warning("Setup file not found, but packaging completed")
            return Path(output_dir) / f"{PROJECT_NAME}-{runtime}-Setup.exe"
    
    print_error(f"Velopack packing failed with exit code {result.returncode}")
    return None

def main():
    parser = argparse.ArgumentParser(
        description="Build Symphex with Velopack for auto-updates",
        epilog="Example: python build_velopack.py --version 1.3.0"
    )
    
    parser.add_argument("--version", required=True, help="Version number (e.g., 1.3.0)")
    parser.add_argument("--output", default="releases", help="Output directory (default: releases)")
    parser.add_argument("--all-platforms", action="store_true", help="Build for all platforms (default: current OS only)")
    
    args = parser.parse_args()
    
    # Get vpk command
    vpk_cmd = get_vpk_command()
    check_vpk_installed(vpk_cmd)
    
    # Setup
    script_dir = Path(__file__).parent
    csproj_path = find_csproj(script_dir)
    
    if not csproj_path:
        print_error("Could not find .csproj file!")
        sys.exit(1)
    
    current_os = platform.system()
    print_info(f"Current OS: {current_os}")
    print_info(f"Project: {csproj_path}")
    print_info(f"Version: {args.version}")
    
    if args.all_platforms:
        print_warning("Building for ALL platforms (--all-platforms flag set)")
    else:
        print_info("Building for current OS only (use --all-platforms to build for all)")
    
    output_dir = Path(args.output)
    output_dir.mkdir(exist_ok=True)
    
    publish_base = Path("publish")
    packages = []
    
    # Find icon
    icon_path = Path("Assets") / "SymphexLogo.ico"
    if icon_path.exists():
        print_success(f"Found icon: {icon_path.absolute()}")
    else:
        print_warning(f"Icon not found at: {icon_path.absolute()}")
    
    # Determine which platforms to build
    build_windows = args.all_platforms or current_os == "Windows"
    build_macos = args.all_platforms or current_os == "Darwin"
    build_linux = args.all_platforms or current_os == "Linux"
    
    # Build Windows
    if build_windows:
        print_header("Building Windows with Velopack")
        win_publish = publish_app(csproj_path, "win-x64", args.version, publish_base / "win-x64")
        win_package = pack_with_velopack(vpk_cmd, win_publish, "win-x64", args.version, output_dir, icon_path)
        if win_package:
            packages.append(win_package)
    
    # Build macOS
    if build_macos:
        print_header("Building macOS Intel with Velopack")
        mac_x64_publish = publish_app(csproj_path, "osx-x64", args.version, publish_base / "osx-x64")
        mac_x64_package = pack_with_velopack(vpk_cmd, mac_x64_publish, "osx-x64", args.version, output_dir)
        if mac_x64_package:
            packages.append(mac_x64_package)
        
        print_header("Building macOS Apple Silicon with Velopack")
        mac_arm_publish = publish_app(csproj_path, "osx-arm64", args.version, publish_base / "osx-arm64")
        mac_arm_package = pack_with_velopack(vpk_cmd, mac_arm_publish, "osx-arm64", args.version, output_dir)
        if mac_arm_package:
            packages.append(mac_arm_package)
    
    # Build Linux
    if build_linux:
        print_header("Building Linux with Velopack")
        linux_publish = publish_app(csproj_path, "linux-x64", args.version, publish_base / "linux-x64")
        linux_package = pack_with_velopack(vpk_cmd, linux_publish, "linux-x64", args.version, output_dir)
        if linux_package:
            packages.append(linux_package)
    
    # Summary
    print_header("✨ Build Complete! ✨")
    
    print(f"\n{Colors.BOLD}Velopack packages created:{Colors.END}")
    for package in packages:
        if package.exists():
            size_mb = package.stat().st_size / (1024 * 1024)
            print(f"  ✓ {package.name} ({size_mb:.2f} MB)")
    
    print(f"\n{Colors.BOLD}Output directory:{Colors.END} {output_dir.absolute()}")
    
    print(f"\n{Colors.BOLD}Next steps:{Colors.END}")
    print(f"  1. Test the installer on your platform")
    print(f"  2. Upload ALL files in '{output_dir}' to GitHub Releases")
    print(f"  3. Velopack will automatically handle updates!")
    
    print(f"\n{Colors.BOLD}Important:{Colors.END}")
    print(f"  • Upload the entire '{output_dir}' folder contents to GitHub")
    print(f"  • Velopack creates delta updates automatically")
    print(f"  • No need for appcast.xml - Velopack handles it")
    print(f"  • Update URL in app: https://github.com/{GITHUB_REPO}/releases/latest/download")

if __name__ == "__main__":
    main()
