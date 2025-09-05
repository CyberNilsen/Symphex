#!/usr/bin/env python3
import os
import shutil
import subprocess
import zipfile
from pathlib import Path

def create_avalonia_macos_bundle():
    # Configuration
    PROJECT_PATH = "./Symphex/Symphex.csproj"
    BUILD_DIR = "./build"
    APP_NAME = "Symphex"
    BUNDLE_IDENTIFIER = "com.yourcompany.symphex"  # Change this to your actual identifier
    
    print("Building Avalonia app for macOS...")
    
    # Clean build directory
    if os.path.exists(BUILD_DIR):
        print(f"Cleaning existing build directory: {BUILD_DIR}")
        shutil.rmtree(BUILD_DIR)
    
    # Build the application - Use osx-arm64 for Apple Silicon
    build_cmd = [
        "dotnet", "publish", PROJECT_PATH,
        "--configuration", "Release",
        "--runtime", "osx-arm64",  # Changed from osx-x64 to osx-arm64
        "--self-contained", "true",
        "--output", BUILD_DIR,
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:PublishTrimmed=false",
        "-p:PublishSingleFile=false"  # Important for Avalonia - don't use single file
    ]
    
    print(f"Running: {' '.join(build_cmd)}")
    result = subprocess.run(build_cmd, capture_output=True, text=True)
    if result.returncode != 0:
        print(f"Build failed: {result.stderr}")
        return False
    
    print("✅ Build completed successfully")
    
    # Check what was built
    build_path = Path(BUILD_DIR)
    if not build_path.exists():
        print(f"❌ Build directory doesn't exist: {build_path}")
        return False
    
    print(f"Build directory contents:")
    for item in build_path.iterdir():
        print(f"  - {item.name} ({'file' if item.is_file() else 'directory'})")
    
    # Find the executable
    executable_path = build_path / APP_NAME
    if not executable_path.exists():
        print(f"❌ Executable not found: {executable_path}")
        # Try to find any executable
        executables = [f for f in build_path.iterdir() if f.is_file() and os.access(f, os.X_OK)]
        if executables:
            print(f"Found executables: {[e.name for e in executables]}")
            executable_path = executables[0]
            APP_NAME = executable_path.name
            print(f"Using: {APP_NAME}")
        else:
            print("No executables found!")
            return False
    
    print("Creating app bundle...")
    
    # Create app bundle structure
    bundle_dir = build_path / f"{APP_NAME}.app"
    contents_dir = bundle_dir / "Contents"
    macos_dir = contents_dir / "MacOS"
    resources_dir = contents_dir / "Resources"
    
    # Create directories
    print(f"Creating bundle structure: {bundle_dir}")
    macos_dir.mkdir(parents=True, exist_ok=True)
    resources_dir.mkdir(parents=True, exist_ok=True)
    
    # Copy (don't move) files to preserve originals for debugging
    files_to_bundle = []
    for item in build_path.iterdir():
        if item.name.endswith('.app'):
            continue
        files_to_bundle.append(item)
    
    print(f"Copying {len(files_to_bundle)} items to bundle...")
    for item in files_to_bundle:
        dest_path = macos_dir / item.name
        try:
            if item.is_file():
                shutil.copy2(item, dest_path)
                print(f"  ✅ Copied file: {item.name}")
            elif item.is_dir():
                shutil.copytree(item, dest_path)
                print(f"  ✅ Copied directory: {item.name}")
        except Exception as e:
            print(f"  ❌ Failed to copy {item.name}: {e}")
    
    # Create Info.plist
    info_plist_content = f"""<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>{APP_NAME}</string>
    <key>CFBundleIdentifier</key>
    <string>{BUNDLE_IDENTIFIER}</string>
    <key>CFBundleName</key>
    <string>{APP_NAME}</string>
    <key>CFBundleDisplayName</key>
    <string>{APP_NAME}</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSSupportsAutomaticGraphicsSwitching</key>
    <true/>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.utilities</string>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright © 2025. All rights reserved.</string>
    <key>LSRequiresNativeExecution</key>
    <true/>
</dict>
</plist>"""
    
    # Write Info.plist
    plist_path = contents_dir / "Info.plist"
    with open(plist_path, "w") as f:
        f.write(info_plist_content)
    print(f"✅ Created Info.plist: {plist_path}")
    
    # Set executable permissions
    executable_in_bundle = macos_dir / APP_NAME
    if executable_in_bundle.exists():
        os.chmod(executable_in_bundle, 0o755)
        print(f"✅ Set executable permissions: {executable_in_bundle}")
    else:
        print(f"❌ Executable not found in bundle: {executable_in_bundle}")
    
    # Set permissions for native libraries
    dylib_count = 0
    for dylib in macos_dir.rglob("*.dylib"):
        os.chmod(dylib, 0o755)
        dylib_count += 1
    
    so_count = 0
    for so_file in macos_dir.rglob("*.so"):
        os.chmod(so_file, 0o755)
        so_count += 1
    
    print(f"✅ Set permissions for {dylib_count} .dylib and {so_count} .so files")
    
    # Remove quarantine attributes
    try:
        subprocess.run(["xattr", "-cr", str(bundle_dir)], check=True, capture_output=True)
        print("✅ Removed quarantine attributes")
    except subprocess.CalledProcessError as e:
        print(f"⚠️  Could not remove quarantine attributes: {e}")
    except FileNotFoundError:
        print("⚠️  xattr command not found (not running on macOS?)")
    
    # Verify bundle structure
    print(f"\nBundle structure:")
    for item in bundle_dir.rglob('*'):
        if item.is_file():
            rel_path = item.relative_to(bundle_dir)
            print(f"  {rel_path}")
    
    # Create zip file
    zip_path = Path("Mac.zip").absolute()
    if zip_path.exists():
        zip_path.unlink()
        print(f"Removed existing zip: {zip_path}")
    
    print(f"Creating zip archive: {zip_path}")
    try:
        with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
            file_count = 0
            for file_path in bundle_dir.rglob('*'):
                if file_path.is_file():
                    # Create relative path from build directory
                    arc_name = file_path.relative_to(build_path)
                    zipf.write(file_path, arc_name)
                    file_count += 1
            print(f"✅ Added {file_count} files to zip")
    except Exception as e:
        print(f"❌ Failed to create zip: {e}")
        return False
    
    print(f"✅ App bundle created: {bundle_dir}")
    print(f"✅ Zip archive created: {zip_path} ({zip_path.stat().st_size / 1024 / 1024:.1f} MB)")
    return True

def add_app_icon(bundle_path, icon_path):
    """Add an app icon to the bundle (optional)"""
    if not os.path.exists(icon_path):
        print(f"Icon file not found: {icon_path}")
        return
    
    resources_dir = Path(bundle_path) / "Contents" / "Resources"
    icon_dest = resources_dir / "AppIcon.icns"
    shutil.copy2(icon_path, icon_dest)
    print(f"✅ Added app icon: {icon_dest}")

if __name__ == "__main__":
    try:
        success = create_avalonia_macos_bundle()
        
        # Optionally add an icon if you have one
        # add_app_icon("./build/Symphex.app", "./path/to/your/icon.icns")
        
        if success:
            print("\n🎉 macOS app bundle created successfully!")
            print("📦 You can now distribute the Mac.zip file")
            print("🔍 Check the ./build/ directory for the .app bundle")
        else:
            print("❌ Failed to create app bundle")
    except KeyboardInterrupt:
        print("\n⚠️  Build interrupted by user")
    except Exception as e:
        print(f"❌ Unexpected error: {e}")
        import traceback
        traceback.print_exc()
