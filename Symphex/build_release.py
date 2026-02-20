#!/usr/bin/env python3
"""
Symphex Installer Builder

Builds installer for your current platform (Windows/macOS/Linux).
No uploading, no changelogs - just builds the installer locally.

Usage:
    python build_release.py --version 1.3.0
    python build_release.py --version 1.3.0 --all-platforms
"""

import os
import sys
import subprocess
import shutil
import platform
import argparse
from pathlib import Path

# CONFIGURATION
PROJECT_NAME = "Symphex"
BUNDLE_ID = "com.cybernilsen.symphex"

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
    result = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True)
    
    if result.returncode != 0 and check:
        print_error(f"Command failed: {result.stderr}")
        sys.exit(1)
    
    return result

def find_csproj(start_dir):
    """Find .csproj file"""
    for file in Path(start_dir).rglob("*.csproj"):
        return str(file)
    return None

def publish_app(csproj_path, runtime, version, output_dir):
    """Publish the app for a specific runtime"""
    print_info(f"Publishing for {runtime}...")
    
    cmd = [
        "dotnet", "publish", csproj_path,
        "-c", "Release",
        "-r", runtime,
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:Version=" + version,
        "-o", str(output_dir)
    ]
    
    run_command(cmd)
    print_success(f"Published to {output_dir}")
    return output_dir

def create_windows_msi(publish_dir, version, output_dir, package_type="both"):
    """Create Windows installer package
    
    Args:
        package_type: "zip", "installer", or "both"
    """
    print_header("Creating Windows Package")
    
    created_files = []
    
    # Create ZIP if requested
    if package_type in ["zip", "both"]:
        zip_file = Path(output_dir) / f"{PROJECT_NAME}-{version}-win-x64.zip"
        shutil.make_archive(str(zip_file.with_suffix('')), 'zip', publish_dir)
        print_success(f"ZIP created: {zip_file}")
        created_files.append(zip_file)
    
    # Create installer if requested
    if package_type in ["installer", "both"]:
        # Check for Inno Setup
        inno_paths = [
            r"C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
            r"C:\Program Files\Inno Setup 6\ISCC.exe",
        ]
        
        inno_cmd = None
        for path in inno_paths:
            if Path(path).exists():
                inno_cmd = path
                break
        
        if not inno_cmd:
            print_warning("Inno Setup not found - skipping installer creation")
            print_info("Download from: https://jrsoftware.org/isdl.php")
            if package_type == "installer":
                print_error("Cannot create installer without Inno Setup")
                sys.exit(1)
        else:
            # Create installer scripts directory
            installer_scripts_dir = Path("installer_scripts")
            installer_scripts_dir.mkdir(exist_ok=True)
            
            # Find the icon file
            icon_path = Path("Assets") / "SymphexLogo.ico"
            if not icon_path.exists():
                icon_path = ""
            else:
                icon_path = icon_path.absolute()
            
            # Create Inno Setup script with absolute paths
            icon_line = f"SetupIconFile={icon_path}\nUninstallDisplayIcon={{app}}\\Symphex.exe" if icon_path else "UninstallDisplayIcon={app}\\Symphex.exe"
            
            iss_content = f"""[Setup]
AppName={PROJECT_NAME}
AppVersion={version}
AppPublisher=CyberNilsen
AppPublisherURL=https://github.com/CyberNilsen/Symphex
DefaultDirName={{autopf}}\\{PROJECT_NAME}
DefaultGroupName={PROJECT_NAME}
OutputDir={Path(output_dir).absolute()}
OutputBaseFilename={PROJECT_NAME}-{version}-win-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
{icon_line}
WizardStyle=modern
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{{cm:CreateDesktopIcon}}"; GroupDescription: "{{cm:AdditionalIcons}}"

[Files]
Source: "{Path(publish_dir).absolute()}\\*"; DestDir: "{{app}}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{{group}}\\{PROJECT_NAME}"; Filename: "{{app}}\\{PROJECT_NAME}.exe"
Name: "{{autodesktop}}\\{PROJECT_NAME}"; Filename: "{{app}}\\{PROJECT_NAME}.exe"; Tasks: desktopicon

[Run]
Filename: "{{app}}\\{PROJECT_NAME}.exe"; Description: "{{cm:LaunchProgram,{PROJECT_NAME}}}"; Flags: nowait postinstall skipifsilent
"""
            
            iss_file = installer_scripts_dir / f"{PROJECT_NAME}-{version}.iss"
            iss_file.write_text(iss_content)
            print_info(f"Inno Setup script saved to: {iss_file}")
            
            # Build installer
            cmd = [inno_cmd, str(iss_file.absolute())]
            result = run_command(cmd, check=False)
            
            if result.returncode == 0:
                installer_file = Path(output_dir) / f"{PROJECT_NAME}-{version}-win-x64-setup.exe"
                print_success(f"Installer created: {installer_file}")
                created_files.append(installer_file)
            else:
                print_error(f"Installer creation failed:\n{result.stderr}")
                print_info(f"ISS script kept at: {iss_file} for manual compilation")
                if package_type == "installer":
                    sys.exit(1)
    
    return created_files if created_files else None
    """Create macOS PKG installer"""
    print_header(f"Creating macOS PKG ({arch})")
    
    if platform.system() != "Darwin":
        print_warning("Not on macOS - creating ZIP instead")
        zip_file = Path(output_dir) / f"{PROJECT_NAME}-{version}-osx-{arch}.zip"
        shutil.make_archive(str(zip_file.with_suffix('')), 'zip', publish_dir)
        print_success(f"ZIP created: {zip_file}")
        return zip_file
    
    # Create app bundle
    app_bundle = Path(output_dir) / f"{PROJECT_NAME}.app"
    contents_dir = app_bundle / "Contents"
    macos_dir = contents_dir / "MacOS"
    resources_dir = contents_dir / "Resources"
    
    macos_dir.mkdir(parents=True, exist_ok=True)
    resources_dir.mkdir(parents=True, exist_ok=True)
    
    # Copy executable
    exe_src = Path(publish_dir) / PROJECT_NAME
    exe_dst = macos_dir / PROJECT_NAME
    shutil.copy2(exe_src, exe_dst)
    os.chmod(exe_dst, 0o755)
    
    # Create Info.plist
    info_plist = f"""<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>{PROJECT_NAME}</string>
    <key>CFBundleIdentifier</key>
    <string>{BUNDLE_ID}</string>
    <key>CFBundleName</key>
    <string>{PROJECT_NAME}</string>
    <key>CFBundleVersion</key>
    <string>{version}</string>
    <key>CFBundleShortVersionString</key>
    <string>{version}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
"""
    (contents_dir / "Info.plist").write_text(info_plist)
    
    # Create PKG
    pkg_file = Path(output_dir) / f"{PROJECT_NAME}-{version}-osx-{arch}.pkg"
    
    cmd = [
        "pkgbuild",
        "--root", str(app_bundle.parent),
        "--identifier", BUNDLE_ID,
        "--version", version,
        "--install-location", f"/Applications/{PROJECT_NAME}.app",
        str(pkg_file)
    ]
    
    result = run_command(cmd, check=False)
    
    # Cleanup temp app bundle
    if app_bundle.exists():
        shutil.rmtree(app_bundle)
    
    if result.returncode == 0:
        print_success(f"PKG created: {pkg_file}")
        return pkg_file
    else:
        print_warning("PKG creation failed, creating ZIP instead")
        zip_file = Path(output_dir) / f"{PROJECT_NAME}-{version}-osx-{arch}.zip"
        shutil.make_archive(str(zip_file.with_suffix('')), 'zip', publish_dir)
        print_success(f"ZIP created: {zip_file}")
        return zip_file

def create_linux_deb(publish_dir, version, output_dir):
    """Create Linux DEB package"""
    print_header("Creating Linux DEB")
    
    # Create DEB structure
    deb_dir = Path(output_dir) / "deb_build"
    deb_dir.mkdir(exist_ok=True)
    
    usr_bin = deb_dir / "usr" / "bin"
    usr_share = deb_dir / "usr" / "share" / "applications"
    debian_dir = deb_dir / "DEBIAN"
    
    usr_bin.mkdir(parents=True, exist_ok=True)
    usr_share.mkdir(parents=True, exist_ok=True)
    debian_dir.mkdir(parents=True, exist_ok=True)
    
    # Copy executable
    exe_src = Path(publish_dir) / PROJECT_NAME
    exe_dst = usr_bin / PROJECT_NAME.lower()
    shutil.copy2(exe_src, exe_dst)
    os.chmod(exe_dst, 0o755)
    
    # Create control file
    control_content = f"""Package: {PROJECT_NAME.lower()}
Version: {version}
Section: sound
Priority: optional
Architecture: amd64
Maintainer: CyberNilsen <contact@cybernilsen.com>
Description: Music downloader for YouTube and Spotify
 Cross-platform music downloader with smart metadata detection.
"""
    (debian_dir / "control").write_text(control_content)
    
    # Create .desktop file
    desktop_content = f"""[Desktop Entry]
Type=Application
Name={PROJECT_NAME}
Comment=Music Downloader
Exec=/usr/bin/{PROJECT_NAME.lower()}
Terminal=false
Categories=Audio;AudioVideo;
"""
    (usr_share / f"{PROJECT_NAME.lower()}.desktop").write_text(desktop_content)
    
    # Build DEB
    deb_file = Path(output_dir) / f"{PROJECT_NAME.lower()}_{version}_amd64.deb"
    
    if shutil.which("dpkg-deb"):
        cmd = ["dpkg-deb", "--build", str(deb_dir), str(deb_file)]
        result = run_command(cmd, check=False)
        
        # Cleanup
        shutil.rmtree(deb_dir)
        
        if result.returncode == 0:
            print_success(f"DEB created: {deb_file}")
            return deb_file
    
    # Fallback to TAR.GZ
    print_warning("dpkg-deb not found, creating TAR.GZ instead")
    if deb_dir.exists():
        shutil.rmtree(deb_dir)
    
    tar_file = Path(output_dir) / f"{PROJECT_NAME}-{version}-linux-x64.tar.gz"
    shutil.make_archive(str(tar_file.with_suffix('').with_suffix('')), 'gztar', publish_dir)
    print_success(f"TAR.GZ created: {tar_file}")
    return tar_file

def main():
    parser = argparse.ArgumentParser(
        description="Build Symphex installer for your current platform",
        epilog="Example: python build_release.py --version 1.3.0 --package-type both"
    )
    
    parser.add_argument("--version", required=True, help="Version number (e.g., 1.3.0)")
    parser.add_argument("--output", default="release", help="Output directory (default: release)")
    parser.add_argument("--all-platforms", action="store_true", help="Build for all platforms (default: current OS only)")
    parser.add_argument("--package-type", choices=["zip", "installer", "both"], default="both",
                       help="Windows package type: zip, installer (Inno Setup), or both (default: both)")
    
    args = parser.parse_args()
    
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
    installers = []
    
    # Determine which platforms to build
    build_windows = args.all_platforms or current_os == "Windows"
    build_macos = args.all_platforms or current_os == "Darwin"
    build_linux = args.all_platforms or current_os == "Linux"
    
    # Build Windows
    if build_windows:
        print_header("Building Windows Installer")
        win_publish = publish_app(csproj_path, "win-x64", args.version, publish_base / "win-x64")
        win_files = create_windows_msi(win_publish, args.version, output_dir, args.package_type)
        if win_files:
            installers.extend(win_files)
    
    # Build macOS x64
    if build_macos:
        print_header("Building macOS Intel")
        mac_x64_publish = publish_app(csproj_path, "osx-x64", args.version, publish_base / "osx-x64")
        mac_x64_installer = create_macos_pkg(mac_x64_publish, args.version, output_dir, "x64")
        installers.append(mac_x64_installer)
        
        # Build macOS ARM64
        print_header("Building macOS Apple Silicon")
        mac_arm_publish = publish_app(csproj_path, "osx-arm64", args.version, publish_base / "osx-arm64")
        mac_arm_installer = create_macos_pkg(mac_arm_publish, args.version, output_dir, "arm64")
        installers.append(mac_arm_installer)
    
    # Build Linux
    if build_linux:
        print_header("Building Linux")
        linux_publish = publish_app(csproj_path, "linux-x64", args.version, publish_base / "linux-x64")
        linux_installer = create_linux_deb(linux_publish, args.version, output_dir)
        installers.append(linux_installer)
    
    # Summary
    print_header("✨ Build Complete! ✨")
    
    print(f"\n{Colors.BOLD}Files created:{Colors.END}")
    for installer in installers:
        if installer.exists():
            size_mb = installer.stat().st_size / (1024 * 1024)
            print(f"  ✓ {installer.name} ({size_mb:.2f} MB)")
    
    print(f"\n{Colors.BOLD}Output directory:{Colors.END} {output_dir.absolute()}")
    
    print(f"\n{Colors.BOLD}Next steps:{Colors.END}")
    print(f"  1. Test the installer(s) on your platform")
    print(f"  2. When ready, manually upload to GitHub Releases")
    print(f"  3. Run the app cast generator to create update manifest")
    
    print(f"\n{Colors.BOLD}Note:{Colors.END}")
    if not args.all_platforms:
        print(f"  • Only built for {current_os} (use --all-platforms to build for all)")
    print(f"  • Windows installer requires Inno Setup: https://jrsoftware.org/isdl.php")
    print(f"  • Use --package-type to choose: zip, installer, or both")
    print(f"  • macOS PKG requires running on macOS")
    print(f"  • Linux DEB requires dpkg-deb")
    print(f"  • Script falls back to ZIP/TAR.GZ if tools are missing")

if __name__ == "__main__":
    main()
