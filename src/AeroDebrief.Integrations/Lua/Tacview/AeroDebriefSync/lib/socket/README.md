# Socket Binary Components

This folder should contain the platform-specific binary files for LuaSocket.

## Windows (x64)
Required file: `core.dll`

Download from: https://github.com/lunarmodules/luasocket/releases

Expected location:
```
lib/socket/core.dll
```

## Linux
Required file: `core.so`

Install via package manager or build from source.

## macOS
Required file: `core.so`

Install via Homebrew or build from source.

## Important Notes

1. **Architecture must match**: If Tacview is 64-bit (most common), use 64-bit LuaSocket
2. **Version compatibility**: Use LuaSocket 3.0+ for best compatibility
3. **Tacview includes it**: Most users don't need this - Tacview 1.9.0+ includes LuaSocket

## Verification

After placing the correct file here, the addon should load without errors.

Check Tacview log (**Help ? Show Log**) for:
```
AeroDebrief Sync: TCP server listening on 127.0.0.1:52001
```

If you see "cannot load module 'socket.core'", the binary is missing or incompatible.

## Getting the Binary

### Option 1: Official Release
Download pre-compiled binaries from LuaSocket releases:
https://github.com/lunarmodules/luasocket/releases

### Option 2: LuaRocks
```bash
luarocks install luasocket
```

Then copy the installed `core.dll` to this folder.

### Option 3: Build from Source
```bash
git clone https://github.com/lunarmodules/luasocket.git
cd luasocket
# Follow platform-specific build instructions
```

## See Also

- `lib/README.md` - Complete installation guide
- `LUASOCKET.md` - Troubleshooting and support
- Official docs: http://w3.impa.br/~diego/software/luasocket/
