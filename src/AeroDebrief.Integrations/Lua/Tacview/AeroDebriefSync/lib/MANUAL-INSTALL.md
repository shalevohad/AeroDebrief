# LuaSocket Manual Installation Guide

## When Do You Need This?

**Short answer: You probably don't!**

Tacview 1.9.0+ includes LuaSocket by default. Only use this guide if:
- You're using Tacview < 1.9.0 (upgrade recommended)
- You see "module 'socket' not found" errors
- Tacview's log shows LuaSocket loading failed

## Quick Check

1. Start Tacview
2. Go to **Help ? About Tacview**
3. Check version:
   - **1.9.0 or higher**: You have LuaSocket built-in! ?
   - **Below 1.9.0**: Follow this guide or upgrade Tacview

## Installation Methods

### Method 1: Upgrade Tacview (Recommended)

The easiest solution is to upgrade Tacview to version 1.9.0 or later:

1. Download latest Tacview from: https://www.tacview.net/
2. Install/update Tacview
3. Restart Tacview
4. Done! LuaSocket is included.

### Method 2: Manual Installation (Advanced)

If you cannot upgrade Tacview, follow these steps:

#### Step 1: Download LuaSocket

**Option A - Pre-compiled (Easiest)**:
1. Visit: https://github.com/lunarmodules/luasocket/releases
2. Download the Windows x64 release (e.g., `luasocket-3.0-rc1-win64.zip`)
3. Extract the archive

**Option B - Via LuaRocks**:
```bash
luarocks install luasocket
```

**Option C - Build from Source** (requires compiler):
```bash
git clone https://github.com/lunarmodules/luasocket.git
cd luasocket
# Follow build instructions for your platform
```

#### Step 2: Locate Required Files

From the downloaded/installed LuaSocket, you need:

```
socket.lua          (main module)
mime.lua           (MIME module)
socket/
  core.dll         (Windows x64 binary)
  http.lua         (optional)
  smtp.lua         (optional)
  tp.lua           (optional)
  url.lua          (optional)
  ftp.lua          (optional)
mime/
  core.dll         (Windows x64 binary)
```

**Important**: For Windows, ensure you use **x64** (64-bit) binaries, as Tacview is typically 64-bit.

#### Step 3: Copy Files to Addon Folder

Copy the files to the addon's `lib` folder:

**Destination**:
```
%APPDATA%\Tacview\AddOns\AeroDebriefSync\lib\
```

**Full path example**:
```
C:\Users\YourName\AppData\Local\Tacview\AddOns\AeroDebriefSync\lib\
??? socket.lua
??? mime.lua
??? socket\
?   ??? core.dll
?   ??? http.lua
?   ??? smtp.lua
?   ??? tp.lua
?   ??? url.lua
?   ??? ftp.lua
??? mime\
    ??? core.dll
```

#### Step 4: Verify Installation

1. Delete or rename the placeholder files:
   - `socket.lua.placeholder` ? Delete or rename
   - `mime.lua.placeholder` ? Delete or rename

2. Restart Tacview

3. Check log (**Help ? Show Log**) for:
   ```
   AeroDebrief Sync: Using LuaSocket from lib folder
   AeroDebrief Sync: LuaSocket version: 3.0
   AeroDebrief Sync: TCP server listening on 127.0.0.1:52001
   ```

4. If successful, you'll see the menu: **Tacview ? AeroDebrief Sync**

## Troubleshooting

### "module 'socket' not found"

**Cause**: LuaSocket files are missing or in wrong location

**Solution**:
1. Verify files are in `%APPDATA%\Tacview\AddOns\AeroDebriefSync\lib\`
2. Check that `socket.lua` exists (not just `socket.lua.placeholder`)
3. Verify path is correct

### "cannot load module 'socket.core'"

**Cause**: Binary component (core.dll) is missing or wrong architecture

**Solution**:
1. Verify `lib\socket\core.dll` exists
2. Ensure it's the **x64** version (Tacview is 64-bit)
3. Check you have Visual C++ Runtime installed

### "The specified module could not be found"

**Cause**: Missing dependencies (usually Visual C++ Runtime)

**Solution**:
1. Download Visual C++ Redistributable:
   - https://aka.ms/vs/17/release/vc_redist.x64.exe
2. Install and restart Tacview

### Still not working?

1. **Check Tacview version**: Should be 1.9.0+ (upgrade if possible)
2. **Check architecture**: Tacview and LuaSocket must both be x64
3. **Check log**: Look for specific error messages
4. **Try system install**: Install LuaSocket system-wide instead

## System-Wide Installation (Alternative)

Instead of the addon's lib folder, install LuaSocket system-wide:

### Windows
```bash
# Install LuaRocks first: https://luarocks.org/
luarocks install luasocket
```

### Linux
```bash
sudo apt-get install lua-socket  # Ubuntu/Debian
```

### macOS
```bash
brew install luasocket
```

Then Lua will find it automatically without needing files in the lib folder.

## Verification Script

To test if LuaSocket is working, create `test_socket.lua` in the AddOns folder:

```lua
-- test_socket.lua
local Addon = {}

function Addon:OnInitialize()
    local success, socket = pcall(require, "socket")
    
    if success then
        Tacview.Log.Info("? LuaSocket loaded successfully!")
        Tacview.Log.Info("Version: " .. tostring(socket._VERSION))
    else
        Tacview.Log.Error("? LuaSocket failed to load!")
        Tacview.Log.Error("Error: " .. tostring(socket))
    end
end

return Addon
```

Load this addon in Tacview and check the log.

## Getting Help

If you're still having issues:

1. **Check version**: `Help ? About Tacview` (should be 1.9.0+)
2. **Check log**: `Help ? Show Log` for error messages
3. **Upgrade Tacview**: Easiest solution for most issues
4. **Ask for help**:
   - Tacview Forums: https://www.tacview.net/forum/
   - GitHub Issues: https://github.com/shalevohad/AeroDebrief/issues

Include in your help request:
- Tacview version
- Operating system
- Complete error message from log
- Steps you've already tried

## Summary

**Recommended approach**:
1. Check Tacview version
2. If < 1.9.0: **Upgrade Tacview** (easiest!)
3. If ? 1.9.0 but still issues: Check log for specific errors
4. Last resort: Manual installation (this guide)

**Remember**: Most users don't need any of this - Tacview 1.9.0+ includes everything! ??

## Files Checklist

After installation, verify these files exist:

- [ ] `lib/socket.lua` (not placeholder)
- [ ] `lib/mime.lua` (not placeholder)
- [ ] `lib/socket/core.dll` (or .so on Linux/macOS)
- [ ] `lib/mime/core.dll` (or .so on Linux/macOS)

Optional but recommended:
- [ ] `lib/socket/http.lua`
- [ ] `lib/socket/tp.lua`
- [ ] `lib/socket/url.lua`

If all files are present and you still have errors, the issue is likely:
- Wrong architecture (x86 vs x64)
- Missing Visual C++ Runtime
- Permissions issue

See troubleshooting section above.
