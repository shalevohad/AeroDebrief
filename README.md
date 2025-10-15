# DCS-SRS-RecordingClient

An open source Recording Client Integration for SRS DCS Radio.

## Essence

DCS-SRS-RecordingClient is designed to integrate with the [SRS DCS Radio](https://github.com/ciribob/DCS-SimpleRadioStandalone), providing users with the capability to record radio communications during their DCS sessions. This client is open source and aims to simplify and automate the process of capturing and storing voice transmissions within the DCS ecosystem.

## What It Brings to Users

- **Automated Recording:** Seamlessly record all SRS radio comms during your flight sessions.
- **Integration:** Designed to work smoothly with SRS and DCS World.
- **Open Source:** Modify and extend the recording functionality as needed for your group or use case.
- **User-Friendly:** Simple setup and operation.

## Minimum Requirements

- **SRS Server:** You must be connected to an SRS server version **2.3.20 or later**.

## Usage Example

To record SRS radio communications, run the CLI with your SRS server’s IP and port:

```sh
DCS-SRS-RecordingClient.exe <SRS_SERVER_IP> <PORT>
```

**Example:**

```sh
DCS-SRS-RecordingClient.exe 192.168.1.100 5002
```

- The program will display its version and minimum server version required.
- It will attempt to connect to the SRS server at `192.168.1.100` on port `5002`.
- If connected, it will start recording all incoming voice packets to the default output file (as configured).
- You will see output like:
  ```
  SRS Recording Client Version: 0.0.1
  Minimum required server version: 2.3.20

  -----------------------------------------------------
  Trying to connect to server at 192.168.1.100 (Port:5002)...
  [Connection Status] Connected to server: 192.168.1.100
  Recording to file: 'recording.wav'...
  to stop recording and disconnect: press Ctrl+C or close the window
  -----------------------------------------------------
  Listening for incoming packets to record:
  ```

- To stop recording, press `Ctrl+C` or close the command window.

**Note:** The client will automatically reconnect if the server disconnects due to timeout or version mismatch, and it will alert if your server is not a compatible version.

For advanced configuration (output file path, etc.), edit the configuration file or use command-line arguments as supported in future releases.

For more info, see the [CLI source code](https://github.com/shalevohad/DCS-SRS-RecordingClient/blob/master/DCS-SRS-RecordingClient.CLI/Program.cs).

## Download & Installation

1. **Download**  
   - Visit the [GitHub Releases page](https://github.com/shalevohad/DCS-SRS-RecordingClient/releases) to get the latest version.
   - Download the appropriate binary for your system.

2. **Install**  
   - Extract the files to a desired folder.
   - Ensure SRS is installed and running for DCS World.

3. **Run**  
   - Execute `DCS-SRS-RecordingClient.exe`.
   - The client will automatically connect to SRS and begin recording comms.

4. **Configuration**  
   - You may specify output directories and recording parameters in the configuration file if provided.

## Contributing

Contributions are welcome! Please open issues or submit pull requests on the [GitHub page](https://github.com/shalevohad/DCS-SRS-RecordingClient).

## License

This project is open source. See the repository for details.

---
For questions or support, please visit the [repository](https://github.com/shalevohad/DCS-SRS-RecordingClient).
