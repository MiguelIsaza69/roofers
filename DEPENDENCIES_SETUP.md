# Dependencies Setup Guide

**Project**: Roofing Simulator with Putty Physics  
**Date**: 2026-06-11  
**Status**: Phase 2 - Dependency Installation

---

## Required Dependencies

This project requires two main external libraries:

1. **Mirror** - Real-time multiplayer networking framework
2. **Newtonsoft.Json (Json.NET)** - JSON serialization library

---

## 1. Installing Mirror Networking

Mirror is the primary networking solution for multiplayer functionality.

### Option A: Via Unity Package Manager (Recommended)

1. Open Unity Editor
2. Navigate to **Window → TextMesh Pro → Import TMP Examples and Extras** (if needed)
3. Open **Window → Package Manager**
4. Click **"+"** (Add package)
5. Select **"Add package from git URL"**
6. Paste: `https://github.com/MirrorNetworking/Mirror.git?path=/Assets/Mirror`
7. Click **Add**

Mirror will be installed to `Assets/Mirror/`

**Expected Installation**: ~5-10 minutes

### Option B: Via Asset Store

1. Open Unity Asset Store in-editor: **Window → Asset Store**
2. Search for "Mirror"
3. Find "Mirror" by Mirror Networking
4. Click **Import** or **Download**
5. Click **Import** in the import dialog

### Option C: Manual Installation

1. Download Mirror from: https://github.com/MirrorNetworking/Mirror/releases
2. Extract the Assets/Mirror folder
3. Copy to your project's Assets/ folder
4. Unity will auto-import

### Verification

After installation, you should see:
- `Assets/Mirror/` folder
- Mirror scripts properly compiled in your assembly

In Console, run:
```csharp
Debug.Log(typeof(Mirror.NetworkManager));
```

Should output: `Mirror.NetworkManager` without errors

---

## 2. Installing Newtonsoft.Json (Json.NET)

Json.NET is used for career save/load serialization.

### Option A: Via NuGet Package Manager for Unity

1. Install **NuGet for Unity** from Asset Store or via git:
   ```
   https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGet%20for%20Unity
   ```

2. After installation, go **NuGet → Manage NuGet Packages**
3. Search for "Newtonsoft.Json"
4. Select version **13.0.x** or later
5. Click **Install**

NuGet will install to `Assets/Plugins/NuGetForUnity/` or similar

### Option B: Manual DLL Installation

1. Download Newtonsoft.Json from NuGet: https://www.nuget.org/packages/Newtonsoft.Json/
2. Version 13.0.3 (.NET Standard 2.0) is compatible with Unity
3. Extract and find `Newtonsoft.Json.dll`
4. Copy to `Assets/Plugins/`
5. Unity will auto-import as a plugin

### Option C: Source Installation

1. Clone from GitHub: https://github.com/JamesNK/Newtonsoft.Json
2. Build the .NET Standard version
3. Copy the resulting DLL to `Assets/Plugins/`

### Verification

After installation, verify in your script:
```csharp
using Newtonsoft.Json;

var testData = JsonConvert.SerializeObject(new { test = "value" });
Debug.Log(testData); // Should output: {"test":"value"}
```

Should compile without errors

---

## 3. Assembly Definition Configuration

The project uses assembly definitions for proper dependency management.

### Current Setup

**Main Game Assembly**: `Assets/Scripts/RoofingSimulator.asmdef`
```json
{
    "name": "RoofingSimulator",
    "rootNamespace": "RoofingSimulator",
    "references": [],
    "precompiledReferences": ["Newtonsoft.Json"]
}
```

**Test Assembly**: `Tests/RoofingSimulator.Tests.asmdef`
```json
{
    "name": "RoofingSimulator.Tests",
    "rootNamespace": "RoofingSimulator.Tests",
    "references": ["RoofingSimulator"],
    "precompiledReferences": ["nunit.framework.dll"]
}
```

### Adding Mirror Reference (Optional)

If you want Mirror in a separate assembly, create `Assets/Scripts/RoofingSimulator.Multiplayer.asmdef`:

```json
{
    "name": "RoofingSimulator.Multiplayer",
    "rootNamespace": "RoofingSimulator.Multiplayer",
    "references": [
        "GUID:27619889b8ba8c24980f49ee34dbb44a",
        "Mirror"
    ]
}
```

Then update main assembly to reference it:
```json
{
    "references": ["RoofingSimulator.Multiplayer"]
}
```

---

## 4. Post-Installation Checklist

After installing both dependencies:

- [ ] **Mirror installed** - Assets/Mirror/ folder exists
- [ ] **Json.NET available** - Newtonsoft.Json.dll is in Assets/Plugins/
- [ ] **No compilation errors** - Project compiles cleanly
- [ ] **SaveManager compiles** - Assets/Scripts/Persistence/SaveManager.cs has no errors
- [ ] **Can import namespaces**:
  ```csharp
  using Mirror;
  using Newtonsoft.Json;
  ```
- [ ] **Test serialization** - Can serialize/deserialize objects
- [ ] **Test networking** - Can reference Mirror.NetworkManager

---

## 5. Troubleshooting

### "Mirror not found" / "Cannot resolve symbol Mirror"

**Problem**: Mirror namespace not recognized

**Solution**:
1. Verify Assets/Mirror/ folder exists
2. Check if Mirror assembly is imported (should see "Mirror" in your dependencies)
3. Reimport Mirror: Right-click Assets/Mirror → **Reimport**
4. Restart Unity if needed

### "Newtonsoft.Json not found" / "Cannot resolve symbol JsonConvert"

**Problem**: Json.NET DLL not loaded

**Solution**:
1. Verify Newtonsoft.Json.dll is in Assets/Plugins/
2. Select the DLL, check Inspector:
   - **Include Platforms**: All should be checked
   - **Select platforms for plugin**: Ensure target platforms are included
3. Click **Apply**
4. Reimport: Right-click DLL → **Reimport**

### "Version mismatch" or "Multiple versions of Mirror/Json.NET"

**Problem**: Conflicting library versions

**Solution**:
1. Delete old versions from Assets/
2. Clean Library/ folder: Close Unity, delete Library/ folder, reopen Unity
3. Reinstall dependencies cleanly

### Assembly definition errors

**Problem**: Assembly definitions not compiling

**Solution**:
1. Check GUID references are correct
2. Verify referenced assemblies exist
3. Check for circular dependencies
4. Reimport all assemblies

---

## 6. Performance Considerations

### Mirror Networking

- Network tick rate configured to 20 Hz (50ms updates)
- Delta compression for material state reduces bandwidth
- Server-authoritative physics prevents cheating

**Expected bandwidth**: ~6 KB/sec per client with 4 players

### Json.NET Serialization

- Used only for career save/load (not per-frame)
- Minimal overhead for typical career data (< 50 KB per save)
- Backup system creates second copy on each save

**Expected save time**: < 500ms

---

## 7. Next Steps

After installing dependencies:

1. **Commit to git**:
   ```bash
   git add -A
   git commit -m "Phase 2: Install Mirror and Json.NET dependencies"
   ```

2. **Create placeholder NetworkManager scene**:
   - Add Mirror NetworkManager prefab to a startup scene
   - Configure transport settings

3. **Test compilation**:
   - Open MainMenu.unity
   - Verify all scripts compile without errors

4. **Continue Phase 2**:
   - Implement MaterialPhysics system
   - Create PlayerInput and CameraController
   - Build out remaining physics foundation

---

## References

- **Mirror Documentation**: https://mirror-networking.com/docs/
- **Mirror GitHub**: https://github.com/MirrorNetworking/Mirror
- **Json.NET Documentation**: https://www.newtonsoft.com/json/help/html/Introduction.htm
- **Json.NET GitHub**: https://github.com/JamesNK/Newtonsoft.Json

---

**Note**: This project uses C# and modern .NET APIs, so ensure you're using Unity 2022 LTS or later with .NET Standard 2.1 support.
