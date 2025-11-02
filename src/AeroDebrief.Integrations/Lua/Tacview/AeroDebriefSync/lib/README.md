# LuaSocket Library - Included for Compatibility

## Overview

This folder contains the LuaSocket library files for compatibility with older Tacview versions (pre-1.9.0).

**Note**: Tacview 1.9.0+ includes LuaSocket, so most users won't need these files! They are included as a fallback for users with older Tacview installations.

## Files Included

This repository includes the LuaSocket 3.0+ library files:

### Core Files (Included ?)
```
lib/
??? socket.lua ..................... Main LuaSocket module ?
??? mime.lua ....................... MIME encoding module ?
??? socket/
?   ??? core.dll ................... LuaSocket binary (Windows x64) ?
?   ??? http.lua ................... HTTP protocol ?
?   ??? smtp.lua ................... SMTP protocol ?
?   ??? tp.lua ..................... Transfer protocol base ?
?   ??? url.lua .................... URL parsing ?
?   ??? ftp.lua .................... FTP protocol ?
??? mime/
    ??? core.dll ................... MIME binary (Windows x64) ?
```

**All files are included in the repository for your convenience!**

## For Users

### Tacview 1.9.0 or Higher
**You don't need these files!** Tacview includes LuaSocket by default.

The addon will automatically:
1. Try Tacview's built-in LuaSocket first
2. Fall back to these files if needed
3. Log which version is being used

### Tacview Older Than 1.9.0
**These files will work automatically!** The addon will detect that Tacview's LuaSocket is unavailable and use these files instead.

No manual installation needed - just copy the addon folder as usual.

## For Developers

### Why Files Are Included

Unlike the original plan to have users download LuaSocket separately, we've included the library files directly in the repository because:

1. **Convenience** - Complete installation in one step
2. **Reliability** - Known working version
3. **Compatibility** - Tested with this addon
4. **No external dependencies** - Users don't need to download separately

### Version Information

- **LuaSocket Version**: 3.0-rc1 or compatible
- **Platform**: Windows x64
- **Lua Version**: 5.1+
- **License**: MIT (see LICENSE section below)

### Files Provided

**Lua Modules**:
- `socket.lua` - Main socket interface
- `mime.lua` - MIME encoding/decoding

**Binary Components** (Windows x64):
- `socket/core.dll` - Socket implementation
- `mime/core.dll` - MIME implementation

**Protocol Modules**:
- `socket/http.lua` - HTTP client
- `socket/smtp.lua` - SMTP client  
- `socket/tp.lua` - Transfer protocol base
- `socket/url.lua` - URL parsing
- `socket/ftp.lua` - FTP client

## Platform Support

### Windows (Included)
? **Full support** - All files included for Windows x64

### Linux / macOS
?? **System installation recommended** - Use package manager:

```bash
# Ubuntu/Debian
sudo apt-get install lua-socket

# macOS
brew install luasocket

# Or via LuaRocks
luarocks install luasocket
```

The addon will find system-installed LuaSocket automatically.

## How It Works

The addon's `main.lua` loads LuaSocket in this order:

```
1. Tacview's built-in LuaSocket (1.9.0+)
   ? Not found?
2. These included lib/ files (fallback)
   ? Not found?
3. System-installed LuaSocket
   ? Not found?
4. Error with instructions
```

You don't need to configure anything - it's automatic!

## Verification

When the addon loads, check Tacview log (**Help ? Show Log**):

? **Using Tacview's LuaSocket** (typical):
```
AeroDebrief Sync: Using Tacview's built-in LuaSocket
AeroDebrief Sync: LuaSocket version: 3.0
```

? **Using included lib/ files** (fallback):
```
AeroDebrief Sync: Using LuaSocket from lib folder
AeroDebrief Sync: LuaSocket version: 3.0
```

Both are correct and will work perfectly!

## Updating LuaSocket

If you want to update the LuaSocket files to a newer version:

1. Download from: https://github.com/lunarmodules/luasocket/releases
2. Replace the files in this `lib/` folder
3. Test the addon in Tacview
4. Update this README with the new version number

## License

LuaSocket is licensed under the MIT License:

```
Copyright © 2004-2013 Diego Nehab

Permission is hereby granted, free of charge, to any person obtaining a
copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.
```

Full license: https://github.com/lunarmodules/luasocket/blob/master/LICENSE

## Source

LuaSocket official repository: https://github.com/lunarmodules/luasocket

The files included here are from the official LuaSocket distribution.

## Troubleshooting

### "cannot load module 'socket.core'"

**On Windows**:
- Ensure you're using the x64 version of Tacview
- Included DLL is for 64-bit systems
- If you need 32-bit, download from LuaSocket releases

**On Linux/macOS**:
- Install via package manager (see Platform Support above)
- Or copy appropriate `.so` files to this folder

### "module 'socket' not found"

**Rare** - The addon should automatically find these files. If this happens:

1. Check that `lib/socket.lua` exists
2. Check Tacview log for path errors
3. See `LUASOCKET.md` for detailed troubleshooting

## For Contributors

### Adding/Updating Files

To add or update LuaSocket files:

1. Download official LuaSocket distribution
2. Copy files to this `lib/` folder
3. Maintain the directory structure shown above
4. Test the addon
5. Commit the changes
6. Update version info in this README

### Testing

Test with both scenarios:
- ? Tacview 1.9.0+ (should use built-in)
- ? Tacview < 1.9.0 (should use lib/ files)

Check the log to verify which LuaSocket is being used.

## Support

For LuaSocket-specific issues:
- See `LUASOCKET.md` in parent folder
- See `MANUAL-INSTALL.md` in this folder (for manual installation scenarios)
- LuaSocket documentation: http://w3.impa.br/~diego/software/luasocket/

For addon issues:
- GitHub: https://github.com/shalevohad/AeroDebrief/issues
- Check main `README.md`

## Summary

? **Files are included** - No separate download needed
? **Automatic fallback** - Works with older Tacview versions  
? **Properly licensed** - MIT license, attribution included
? **Tested and working** - Known compatible version

Most users won't even know these files exist - the addon just works! ??
