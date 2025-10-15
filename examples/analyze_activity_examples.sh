#!/bin/bash

# Example script showing various ways to analyze audio activity in SRS recordings
# This script demonstrates the new File Analysis functionality available in both
# the GUI (File Analysis tab) and CLI (--analyze-activity command)

echo "=== DCS-SRS File Analysis Examples ==="
echo

# Check if recording file is provided
if [ $# -eq 0 ]; then
    echo "Usage: $0 <recording.raw>"
    echo
    echo "This script demonstrates various file analysis options available in:"
    echo "  - GUI: File Analysis tab in the player application"
    echo "  - CLI: --analyze-activity command"
    echo
    echo "Make sure you have a DCS-SRS recording file to analyze."
    exit 1
fi

RECORDING_FILE="$1"
CLI_EXECUTABLE="./DCS-SRS-RecordingClient.exe"

# Check if file exists
if [ ! -f "$RECORDING_FILE" ]; then
    echo "Error: Recording file '$RECORDING_FILE' not found!"
    exit 1
fi

# Check if CLI executable exists
if [ ! -f "$CLI_EXECUTABLE" ]; then
    echo "Error: DCS-SRS-RecordingClient.exe not found in current directory!"
    echo "Please run this script from the directory containing the CLI executable."
    exit 1
fi

echo "Analyzing recording file: $RECORDING_FILE"
echo
echo "You can also perform these analyses using the GUI:"
echo "1. Open the DCS SRS Recording Player"
echo "2. Load your recording file"
echo "3. Go to the 'File Analysis' tab"
echo "4. Use the various analysis and export options"
echo

# Example 1: Basic activity analysis with default settings
echo "1. Basic Audio Activity Analysis (default settings):"
echo "   GUI: Click 'Analyze Activity' with default threshold (500) and min duration (300ms)"
echo "   CLI: $CLI_EXECUTABLE --analyze-activity \"$RECORDING_FILE\""
echo
$CLI_EXECUTABLE --analyze-activity "$RECORDING_FILE"
echo
echo "Press Enter to continue to next example..."
read

# Example 2: More sensitive analysis (lower threshold)
echo "2. More Sensitive Analysis (threshold 200):"
echo "   This will pick up quieter transmissions and more background noise"
echo "   GUI: Set threshold to 200, click 'Analyze Activity'"
echo "   CLI: $CLI_EXECUTABLE --analyze-activity \"$RECORDING_FILE\" --threshold 200"
echo
$CLI_EXECUTABLE --analyze-activity "$RECORDING_FILE" --threshold 200
echo
echo "Press Enter to continue to next example..."
read

# Example 3: Less sensitive analysis (higher threshold)
echo "3. Less Sensitive Analysis (threshold 1500):"
echo "   This will only pick up clear, loud transmissions"
echo "   GUI: Set threshold to 1500, click 'Analyze Activity'"
echo "   CLI: $CLI_EXECUTABLE --analyze-activity \"$RECORDING_FILE\" --threshold 1500"
echo
$CLI_EXECUTABLE --analyze-activity "$RECORDING_FILE" --threshold 1500
echo
echo "Press Enter to continue to next example..."
read

# Example 4: Filter out very short transmissions
echo "4. Filter Short Transmissions (minimum 500ms):"
echo "   This will ignore button clicks and very brief audio"
echo "   GUI: Set min duration to 500, click 'Analyze Activity'"
echo "   CLI: $CLI_EXECUTABLE --analyze-activity \"$RECORDING_FILE\" --min-duration 500"
echo
$CLI_EXECUTABLE --analyze-activity "$RECORDING_FILE" --min-duration 500
echo
echo "Press Enter to continue to next example..."
read

# Example 5: Export activity to CSV
CSV_OUTPUT="${RECORDING_FILE%.*}_activity.csv"
echo "5. Export Activity Analysis to CSV:"
echo "   This will create a CSV file with all activity periods for Excel/spreadsheet analysis"
echo "   GUI: After running analysis, click 'Export Activity to CSV'"
echo "   CLI: $CLI_EXECUTABLE --analyze-activity \"$RECORDING_FILE\" --export-csv \"$CSV_OUTPUT\""
echo
$CLI_EXECUTABLE --analyze-activity "$RECORDING_FILE" --export-csv "$CSV_OUTPUT"
if [ -f "$CSV_OUTPUT" ]; then
    echo "   CSV file created: $CSV_OUTPUT"
    echo "   You can open this in Excel, LibreOffice Calc, or any spreadsheet application"
    echo "   The CSV contains: StartTime, EndTime, Duration, Player, Frequency, Amplitudes, etc."
fi
echo
echo "Press Enter to continue to next example..."
read

# Example 6: General file structure analysis
echo "6. General File Structure Analysis:"
echo "   This analyzes the overall recording structure, players, frequencies, and potential issues"
echo "   GUI: Click 'Analyze File Structure' in the File Analysis tab"
echo "   CLI: $CLI_EXECUTABLE --analyze \"$RECORDING_FILE\""
echo
$CLI_EXECUTABLE --analyze "$RECORDING_FILE"
echo
echo "Press Enter to continue to final example..."
read

# Example 7: Combined parameters for production use
FILTERED_CSV="${RECORDING_FILE%.*}_filtered_activity.csv"
echo "7. Production Analysis (recommended settings):"
echo "   - Moderate sensitivity (threshold 500)"
echo "   - Filter short transmissions (minimum 300ms)"
echo "   - Export to CSV for further analysis"
echo "   GUI: Set threshold 500, min duration 300, click 'Analyze Activity', then 'Export Activity to CSV'"
echo "   CLI: $CLI_EXECUTABLE --analyze-activity \"$RECORDING_FILE\" --threshold 500 --min-duration 300 --export-csv \"$FILTERED_CSV\""
echo
$CLI_EXECUTABLE --analyze-activity "$RECORDING_FILE" --threshold 500 --min-duration 300 --export-csv "$FILTERED_CSV"
if [ -f "$FILTERED_CSV" ]; then
    echo "   Filtered CSV file created: $FILTERED_CSV"
fi
echo

# Example 8: Player-specific analysis
echo "8. Player-Specific Analysis:"
echo "   Filter activity for a specific player (replace 'Viper1' with actual player name)"
echo "   CLI Only: $CLI_EXECUTABLE --analyze-activity \"$RECORDING_FILE\" --player \"Viper1\""
echo "   (Note: GUI filtering can be done by examining the CSV export or analysis results)"
echo
echo "Skipping this example - requires knowing actual player names in your recording"
echo

# Example 9: Frequency-specific analysis
echo "9. Frequency-Specific Analysis:"
echo "   Filter activity for a specific frequency (e.g., 251.0 MHz)"
echo "   CLI Only: $CLI_EXECUTABLE --analyze-activity \"$RECORDING_FILE\" --frequency 251.0"
echo "   (Note: GUI has frequency filtering in the Player tab for playback)"
echo
echo "Skipping this example - requires knowing actual frequencies in your recording"
echo

echo "=== Analysis Complete ==="
echo
echo "File Analysis Tab Features:"
echo "? Audio Activity Analysis - detects when people are talking vs silence"
echo "? Adjustable sensitivity (threshold) and minimum duration filtering"
echo "? File Structure Analysis - shows players, frequencies, recording statistics"
echo "? CSV Export - export activity data for Excel/spreadsheet analysis"
echo "? WAV Export - convert recording to standard audio format"
echo
echo "Tips for using the results:"
echo "- Use CSV files in Excel to create charts and timelines of radio activity"
echo "- Filter by player or frequency to focus on specific communications"
echo "- Use timestamps to quickly navigate to interesting parts of your recording"
echo "- Adjust threshold based on your recording quality and background noise"
echo "- Lower threshold = more sensitive (picks up quieter audio + noise)"
echo "- Higher threshold = less sensitive (only clear, loud transmissions)"
echo
echo "Analysis Parameters:"
echo "- Threshold: Audio amplitude level below which sound is considered silence"
echo "  - Range: 0-32767 (16-bit audio)"
echo "  - Default: 500 (good for most recordings)"
echo "  - Noisy environment: try 800-1500"
echo "  - Quiet environment: try 200-400"
echo
echo "- Min Duration: Shortest activity period to report"
echo "  - Default: 300ms (filters out button clicks)"  
echo "  - For detailed analysis: try 100-200ms"
echo "  - For major transmissions only: try 500-1000ms"
echo
echo "For more CLI options, run: $CLI_EXECUTABLE --help"