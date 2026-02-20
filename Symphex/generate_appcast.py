#!/usr/bin/env python3
"""
Generate App Cast for NetSparkle

Creates the appcast.xml file that tells your app about available updates.
Run this AFTER you've uploaded your release to GitHub.
"""

import subprocess
import shutil
import sys
from pathlib import Path

def find_netsparkle_tool():
    """Find the netsparkle-generate-appcast tool"""
    tool = shutil.which("netsparkle-generate-appcast")
    if tool:
        return tool
    
    windows_tool = Path.home() / ".dotnet" / "tools" / "netsparkle-generate-appcast.exe"
    if windows_tool.exists():
        return str(windows_tool)
    
    print("❌ netsparkle-generate-appcast not found!")
    print("📦 Install it with: dotnet tool install --global NetSparkleUpdater.Tools.AppCastGenerator")
    sys.exit(1)

def main():
    import argparse
    
    parser = argparse.ArgumentParser(
        description="Generate app cast for NetSparkle updates",
        epilog="Example: python generate_appcast.py --version 1.3.0 --url https://github.com/CyberNilsen/Symphex/releases/download/v1.3.0"
    )
    
    parser.add_argument("--version", required=True, help="Version number (e.g., 1.3.0)")
    parser.add_argument("--url", required=True, help="Base download URL where your files are hosted")
    parser.add_argument("--installers-dir", default="release", help="Directory with your installers (default: release)")
    
    args = parser.parse_args()
    
    installers_dir = Path(args.installers_dir)
    
    if not installers_dir.exists():
        print(f"❌ Directory not found: {installers_dir}")
        sys.exit(1)
    
    tool = find_netsparkle_tool()
    
    print(f"📦 Generating app cast...")
    print(f"   Version: {args.version}")
    print(f"   URL: {args.url}")
    print(f"   Installers: {installers_dir}")
    
    cmd = [
        tool,
        "-b", str(installers_dir),
        "-o", str(installers_dir),
        "-u", args.url,
        "-n", "Symphex",
        "--file-extract-version",
    ]
    
    result = subprocess.run(cmd, capture_output=True, text=True)
    
    if result.returncode == 0:
        appcast_file = installers_dir / "appcast.xml"
        sig_file = installers_dir / "appcast.xml.signature"
        
        print(f"\n✅ App cast generated!")
        print(f"   📄 {appcast_file}")
        print(f"   🔐 {sig_file}")
        
        print(f"\n📤 Next steps:")
        print(f"   1. Upload these files to GitHub:")
        print(f"      • {appcast_file.name}")
        print(f"      • {sig_file.name}")
        print(f"      • All your installer files")
        print(f"   2. Make sure appcast.xml is accessible at:")
        print(f"      {args.url}/appcast.xml")
        print(f"   3. Your app will automatically check for updates!")
    else:
        print(f"\n❌ Failed to generate app cast")
        print(result.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
