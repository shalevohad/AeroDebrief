# Server Bookmarks Storage

## Overview

Server bookmarks are stored in the user's profile directory to ensure they persist across application updates and are user-specific.

## Storage Location

**Path:** `%APPDATA%\AeroDebrief\server_bookmarks.json`

**Full Path Example:** `C:\Users\[YourUsername]\AppData\Roaming\AeroDebrief\server_bookmarks.json`

## File Format

The bookmarks are stored as a JSON array with the following structure:

```json
[
  {
    "Name": "Production Server",
    "Ip": "192.168.1.100",
    "Port": 5002
  },
  {
    "Name": "Development Server",
    "Ip": "127.0.0.1",
    "Port": 5002
  }
]
```

## Sorting

Bookmarks are automatically sorted by `Name` in **descending order (Z-A)** when:
- Loading from disk on application startup
- Saving after adding/updating/deleting a bookmark
- The observable collection is updated to maintain sort order in the UI

## Auto-Update Behavior

When connected to a server:
- **IP and Port fields are disabled** (read-only)
- **Server Name field remains editable**
- Clicking "Add" will:
  - Update the existing bookmark's name if one exists with the same IP:Port
  - Create a new bookmark if no match is found
- The bookmark list is automatically resorted and saved

## Backup and Recovery

### Manual Backup
To backup your bookmarks:
1. Navigate to `%APPDATA%\AeroDebrief\`
2. Copy `server_bookmarks.json` to a safe location

### Restore from Backup
1. Close AeroDebrief
2. Copy your backed-up `server_bookmarks.json` to `%APPDATA%\AeroDebrief\`
3. Restart AeroDebrief

### Export/Import (Future Feature)
The UI includes placeholder buttons for Export/Import functionality that will allow:
- Exporting bookmarks to a custom location
- Importing bookmarks from shared team files
- Merging bookmarks from multiple sources

## Directory Creation

The `%APPDATA%\AeroDebrief\` directory is automatically created if it doesn't exist when:
- First bookmark is added
- Application attempts to load bookmarks on startup

## Comparison with Config Files

| Data Type | Storage Location | Format | Reason |
|-----------|-----------------|--------|---------|
| Player Settings | `[AppDir]\configs\player.cfg` | SharpConfig (.cfg) | Application-level settings |
| Recorder Settings | `[AppDir]\configs\recorder.cfg` | SharpConfig (.cfg) | Application-level settings |
| **Server Bookmarks** | **`%APPDATA%\AeroDebrief\server_bookmarks.json`** | **JSON** | **User-specific data** |

## Benefits of User Profile Storage

1. **User-Specific**: Each Windows user has their own bookmarks
2. **Survives Updates**: Bookmarks persist when application is updated/reinstalled
3. **Roaming Support**: Can be synced across machines via Windows Roaming Profiles
4. **Standard Location**: Follows Windows best practices for user data
5. **Easy Backup**: Users can easily find and backup their data

## Troubleshooting

### Bookmarks Not Saving
1. Check if directory exists: `%APPDATA%\AeroDebrief\`
2. Verify write permissions to the directory
3. Check application logs for errors (NLog)

### Bookmarks Not Loading
1. Verify file exists: `%APPDATA%\AeroDebrief\server_bookmarks.json`
2. Check JSON syntax is valid
3. Ensure file is not locked by another process

### Reset Bookmarks
To start fresh:
1. Close AeroDebrief
2. Delete `%APPDATA%\AeroDebrief\server_bookmarks.json`
3. Restart AeroDebrief (will start with empty bookmark list)

## Logging

All bookmark operations are logged with NLog:
- Load success/failure
- Save success/failure
- Number of bookmarks loaded/saved
- File paths used

Check the application logs for detailed diagnostic information.
