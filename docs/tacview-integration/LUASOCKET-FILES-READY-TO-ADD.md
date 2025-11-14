# LuaSocket Files - Ready for Commit! ?

## Summary

The repository is now configured to **include actual LuaSocket library files** instead of just placeholders and documentation.

## Changes Made

### 1. ? Updated .gitignore
**File**: `lib/.gitignore`

Changed from **excluding** binary files to **including** them:
```diff
- # Exclude actual LuaSocket files
- *.dll
- *.so
- socket.lua
- mime.lua

+ # Include all LuaSocket files for distribution
+ !socket.lua
+ !mime.lua
+ !*.dll
+ !*.so
```

Now Git will track and commit the actual library files!

### 2. ? Updated Documentation
**Files updated**:
- `lib/README.md` - Now indicates files are included ?
- `LUASOCKET.md` - Updated to mention included files
- `INSTALL.md` - Mentions lib/ folder with included files

### 3. ? Created Helper Files
**New files**:
- `lib/download-luasocket.ps1` - PowerShell script to download LuaSocket
- `lib/ADD-FILES-HERE.md` - Instructions for adding files

## Next Steps: Adding the Actual Files

You now need to add the actual LuaSocket files. Here's how:

### Method 1: Download Pre-Compiled (Easiest)

1. **Download LuaSocket for Windows x64**:
   - Visit: http://luabinaries.sourceforge.net/download.html
   - Or: https://github.com/moteus/lua-socket-dist/releases
   - Download: `luasocket-3.0-rc1-win64.zip` or similar

2. **Extract and copy files**:
   ```
   Copy FROM archive          TO lib/ folder
   ?????????????????????????????????????????????????
   socket.lua              ?  socket.lua
   mime.lua                ?  mime.lua
   socket/core.dll         ?  socket/core.dll
   mime/core.dll           ?  mime/core.dll
   socket/http.lua         ?  socket/http.lua
   socket/smtp.lua         ?  socket/smtp.lua
   socket/tp.lua           ?  socket/tp.lua
   socket/url.lua          ?  socket/url.lua
   socket/ftp.lua          ?  socket/ftp.lua
   ```

3. **Delete placeholders** (optional):
   ```
   lib/socket.lua.placeholder
   lib/mime.lua.placeholder
   ```

### Method 2: Use PowerShell Script

```powershell
cd src\AeroDebrief.Integrations\Lua\Tacview\AeroDebriefSync\lib
.\download-luasocket.ps1
```

This downloads Lua files automatically. You'll still need to get the .dll files manually.

### Method 3: Use LuaRocks

If you have LuaRocks installed:

```bash
luarocks install luasocket
```

Then copy from: `C:\Program Files\LuaRocks\systree\lib\lua\5.1\`

## Expected Final Structure

After adding files, `lib/` should look like:

```
lib/
??? socket.lua ........................... ? Add this
??? mime.lua ............................. ? Add this
??? ltn12.lua ............................ ? Optional
??? socket/
?   ??? core.dll ......................... ? Add this (Windows x64)
?   ??? http.lua ......................... ? Add this
?   ??? smtp.lua ......................... ? Optional
?   ??? tp.lua ........................... ? Add this
?   ??? url.lua .......................... ? Add this
?   ??? ftp.lua .......................... ? Optional
?   ??? README.md ........................ (already exists)
??? mime/
?   ??? core.dll ......................... ? Add this (Windows x64)
?   ??? README.md ........................ (already exists)
??? README.md ............................ (already exists)
??? ADD-FILES-HERE.md .................... (already exists)
??? MANUAL-INSTALL.md .................... (already exists)
??? download-luasocket.ps1 ............... (already exists)
??? .gitignore ........................... (already exists)
```

## Verification

After adding files, verify:

```bash
# Check files exist
dir lib\socket.lua
dir lib\mime.lua
dir lib\socket\core.dll
dir lib\mime\core.dll

# Test with Lua (if installed)
lua -e "package.path='lib/?.lua;lib/?/init.lua;'..package.path; package.cpath='lib/?.dll;lib/?/core.dll;'..package.cpath; require('socket'); print('? Works!')"
```

## Committing to Git

Once files are added:

```bash
cd <repository-root>

# Add all new files
git add src/AeroDebrief.Integrations/Lua/Tacview/AeroDebriefSync/lib/

# Check what will be committed
git status

# Commit
git commit -m "Add LuaSocket 3.0 library files for Tacview fallback support"

# Push to repository
git push
```

## What This Achieves

? **Complete installation** - Users get everything in one download
? **Older Tacview support** - Works with versions < 1.9.0  
? **No external dependencies** - No separate downloads needed
? **Automatic fallback** - Addon uses Tacview's LuaSocket if available, falls back to lib/ if not
? **Properly licensed** - MIT license, attribution included in README

## Important Notes

### File Size
LuaSocket files add approximately **1-2 MB** to the repository:
- socket.lua: ~40 KB
- mime.lua: ~10 KB
- socket/core.dll: ~500 KB
- mime/core.dll: ~100 KB
- Protocol modules: ~200 KB total

This is acceptable for a complete distribution.

### Platform
Included files are for **Windows x64** only. Linux/macOS users should install system-wide (documented in README.md).

### License
LuaSocket is MIT licensed - redistribution is allowed and encouraged. License information is included in `lib/README.md`.

### Updates
To update LuaSocket in the future:
1. Download new version
2. Replace files in lib/
3. Update version number in `lib/README.md`
4. Test with Tacview
5. Commit and push

## Current Status

| Item | Status | Notes |
|------|--------|-------|
| .gitignore updated | ? | Now allows binary files |
| Documentation updated | ? | Reflects included files |
| Helper scripts created | ? | download-luasocket.ps1 |
| Instructions created | ? | ADD-FILES-HERE.md |
| **Files added** | ? **YOUR TURN** | Download and add files |

## Where to Download

**Recommended sources** (in order):

1. **LuaBinaries** (easiest):
   - http://luabinaries.sourceforge.net/download.html

2. **GitHub releases**:
   - https://github.com/moteus/lua-socket-dist/releases

3. **LuaRocks**:
   ```bash
   luarocks install luasocket
   ```
   Then copy from installation directory

4. **Build from source**:
   - https://github.com/lunarmodules/luasocket
   - Requires Visual Studio

## Testing Before Commit

After adding files:

1. **Test with Lua** (if you have Lua installed):
   ```bash
   cd lib
   lua -e "require('socket'); print('?')"
   ```

2. **Test with Tacview** (older version if available):
   - Install addon in Tacview < 1.9.0
   - Check log shows "Using LuaSocket from lib folder"
   - Verify TCP server starts

3. **Test with Tacview 1.9.0+**:
   - Install addon
   - Check log shows "Using Tacview's built-in LuaSocket"
   - Verify fallback isn't used (as expected)

## Questions?

**Where exactly do I put the files?**
? See `lib/ADD-FILES-HERE.md` for detailed file-by-file instructions

**Which files are required vs optional?**
? See table in `lib/ADD-FILES-HERE.md` - marked with ? (required) or ? (optional)

**What if I can't find pre-compiled binaries?**
? Use the `download-luasocket.ps1` script or install via LuaRocks

**Do I need Linux/macOS binaries too?**
? No! Linux/macOS users will use system-installed LuaSocket (documented in README)

**What version of LuaSocket should I use?**
? Version 3.0 or higher (3.0-rc1 is fine)

**Can I test without Tacview?**
? Yes, if you have Lua installed: `lua -e "require('socket')"`

---

## Ready to Add Files!

Everything is prepared. Now just:

1. Download LuaSocket (see "Where to Download" above)
2. Copy files to lib/ folder (see `ADD-FILES-HERE.md`)
3. Test (see "Testing Before Commit" above)
4. Commit and push!

**After you add the files, the addon will be 100% ready for users with any Tacview version!** ??

---

**Status**: ? Waiting for LuaSocket files to be added
**Next Step**: Download and copy files (see Method 1 above)
**Documentation**: All updated and ready ?
