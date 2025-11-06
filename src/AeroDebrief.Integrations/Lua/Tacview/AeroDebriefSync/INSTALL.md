# Tacview Addon Installation Guide

## Quick Install

### Windows

1. **Locate Tacview AddOns Folder**
   ```
   C:\Users\<YourUsername>\AppData\Local\Tacview\AddOns\
   ```
   
   Or press `Win + R` and paste:
   ```
   %APPDATA%\Tacview\AddOns
   ```

2. **Copy Addon Folder**
   
   Copy the entire `AeroDebriefSync` folder from:
   ```
   <AeroDebrief>\src\AeroDebrief.Integrations\Lua\Tacview\AeroDebriefSync\
   ```
   
   To:
   ```
   C:\Users\<YourUsername>\AppData\Local\Tacview\AddOns\AeroDebriefSync\
   ```

3. **Restart Tacview**

4. **Verify Installation**
   - Open Tacview
   - Go to **Help ? Show Log**
   - Look for: `AeroDebrief Sync: Initialized successfully on port 52001`
   - Check menu: **Tacview ? AeroDebrief Sync** should appear

## Prerequisites

### Tacview Version

- **Required**: Tacview 1.9.0 or higher recommended
- **Older versions**: Supported via included LuaSocket library
- Check your version: **Help ? About Tacview**

### LuaSocket Dependency

This addon requires **LuaSocket** for networking.

**Tacview 1.9.0+**: LuaSocket is included - nothing to install! ?

**Older Tacview versions**: LuaSocket files are included in the `lib/` folder as a fallback. The addon will automatically use them if Tacview doesn't have LuaSocket built-in.

**No manual installation needed** - the addon handles everything automatically!

If you see errors about missing 'socket' module, see [`LUASOCKET.md`](LUASOCKET.md) for troubleshooting.

## Folder Structure After Install

```
%APPDATA%\Tacview\AddOns\
??? AeroDebriefSync\
    ??? main.lua
    ??? manifest.txt
    ??? config.lua
    ??? tcp_server.lua
    ??? protocol.lua
    ??? state_manager.lua
    ??? pilot_extractor.lua
    ??? pan_manager.lua
    ??? menu_ui.lua
    ??? utils.lua
    ??? README.md
    ??? INSTALL.md
    ??? LUASOCKET.md
    ??? lib/ ........................... LuaSocket fallback files
        ??? socket.lua ................. (included)
        ??? mime.lua ................... (included)
        ??? socket/
        ?   ??? core.dll ............... (included, Windows x64)
        ?   ??? [other modules]
        ??? mime/
            ??? core.dll ............... (included, Windows x64)
```

## Configuration

### Default Settings

The addon will create a `config.txt` file on first run:

```ini
Port=52001
BindAddress=127.0.0.1
UpdateRate=10
AutoReconnect=true
```

### Changing TCP Port

1. Close Tacview
2. Edit `config.txt` in the addon folder
3. Change `Port=52001` to your desired port
4. Save and restart Tacview
5. **Important**: Update AeroDebrief settings to match the same port!

## Troubleshooting

### Addon Not Loading

**Check Tacview Version**:
- Minimum required: Tacview 1.9.0 or higher
- Check: **Help ? About Tacview**

**Check Log for Errors**:
- **Help ? Show Log**
- Look for errors starting with "AeroDebrief Sync:"

### "Failed to start TCP server"

**Port Already in Use**:
- Another application is using port 52001
- Solution: Change port in `config.txt` (see above)

**Permission Denied**:
- Windows Firewall may be blocking
- Solution: Allow Tacview through Windows Firewall

### Menu Not Appearing

1. Close Tacview completely
2. Delete the addon folder
3. Reinstall (see Quick Install above)
4. Restart Tacview

### Connection Issues

**AeroDebrief Not Connecting**:
1. Verify both Tacview and AeroDebrief are running
2. Check both use the same TCP port
3. Look for "Client connected" message in Tacview log
4. Check AeroDebrief Tacview integration settings

**Firewall Blocking Localhost**:
- Even though we use localhost (127.0.0.1), some firewalls may block it
- Add exception for Tacview.exe in Windows Firewall

## Verification Checklist

After installation, verify these work:

- [ ] Tacview starts without errors
- [ ] Log shows: "AeroDebrief Sync: Initialized successfully"
- [ ] Log shows: "TCP server listening on 127.0.0.1:52001"
- [ ] Menu item appears: **Tacview ? AeroDebrief Sync**
- [ ] Settings dialog opens and shows port 52001
- [ ] Select an aircraft ? Log shows "Selection changed"
- [ ] Play replay ? Log shows "Playback state changed"

**If you see "module 'socket' not found"**:
- Your Tacview version may be too old (< 1.9.0)
- Update Tacview to the latest version
- See [`LUASOCKET.md`](LUASOCKET.md) for manual installation

## Advanced

### Lua Dependencies

The addon uses these Lua libraries (included with Tacview):
- `socket` (LuaSocket)
- `JSON` (JSON encoder/decoder)

No additional installation required.

### Debug Mode

To enable verbose logging:

1. Edit `main.lua`
2. Find the `OnInitialize()` function
3. Add this line at the top:
   ```lua
   Tacview.Log.SetLevel(Tacview.Log.Level.Debug)
   ```
4. Restart Tacview

Now the log will show detailed debug messages.

### Uninstalling

1. Close Tacview
2. Delete the addon folder:
   ```
   %APPDATA%\Tacview\AddOns\AeroDebriefSync\
   ```
3. Restart Tacview

## Support

For issues or questions:
- Check the main README.md in the addon folder
- GitHub Issues: https://github.com/shalevohad/AeroDebrief/issues
- Documentation: `docs/tacview-integration/` folder

## Next Steps

After successful installation:
1. Install/run AeroDebrief
2. Configure Tacview integration in AeroDebrief settings
3. Open a mission replay in Tacview
4. Open corresponding recording in AeroDebrief
5. Select pilots in Tacview
6. Press play - audio should sync automatically!
