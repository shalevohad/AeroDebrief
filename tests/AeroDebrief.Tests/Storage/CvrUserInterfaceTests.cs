using Xunit;
using FluentAssertions;
using AeroDebrief.Core;
using AeroDebrief.Core.Storage;
using System.IO;
using System.Linq;

namespace AeroDebrief.Tests.Storage
{
    /// <summary>
    /// Tests to verify that the CVR user interface requirements are met:
    /// - Users see ONLY .cvr files in dialogs
    /// - Internal database files are never exposed
    /// - All format names use CVR branding
    /// </summary>
    public class CvrUserInterfaceTests
    {
        [Fact]
        public void FileFilters_ShouldNotExpose_DatabaseExtension()
        {
            // Arrange & Act
            var filters = RecordingFileLoader.GetFileFilters();
            
            // Assert
            filters.Should().NotContain(".db|", 
                because: "File dialog filters must not expose .db extension to users");
            filters.Should().NotContain("Database (*.db)", 
                because: "File dialog must not show 'Database' format option");
            filters.Should().Contain(".cvr", 
                because: "File dialog must include .cvr extension");
            filters.Should().Contain("Combat Voice Recording", 
                because: "File dialog must show CVR branding");
        }

        [Fact]
        public void FileFilters_ShouldShow_OnlyCvrAndLegacyFormats()
        {
            // Arrange & Act
            var filters = RecordingFileLoader.GetFileFilters();
            var filterParts = filters.Split('|');
            
            // Assert - Check "All Recording Files" filter
            var allRecordingsFilter = filterParts[1]; // Second element is the extensions list
            allRecordingsFilter.Should().Contain("*.cvr", 
                because: "All files filter must include CVR");
            allRecordingsFilter.Should().Contain("*.adb", 
                because: "All files filter must include legacy ADB");
            allRecordingsFilter.Should().NotContain("*.db", 
                because: "All files filter must not include .db extension");
        }

        [Fact]
        public void SupportedExtensions_Internal_CanIncludeDatabase()
        {
            // Arrange & Act
            var extensions = RecordingFileLoader.GetSupportedExtensions();
            
            // Assert - Internal API can include .db for compatibility
            extensions.Should().Contain(".cvr", 
                because: "Internal extensions must include .cvr");
            extensions.Should().Contain(".db", 
                because: "Internal extensions can include .db for programmatic access");
            extensions.Should().Contain(".cvr-debug", 
                because: "Internal extensions can include .cvr-debug for testing");
            
            // Note: This is correct - internal API supports more formats than user-facing dialogs
        }

        [Fact]
        public void FormatName_ShouldUseCvrBranding_NotDatabaseTerminology()
        {
            // Arrange & Act
            var cvrName = CvrFormat.GetFormatName("test.cvr");
            var adbName = CvrFormat.GetFormatName("test.adb");
            var dbName = CvrFormat.GetFormatName("test.db");
            var debugName = CvrFormat.GetFormatName("test.cvr-debug");
            
            // Assert
            cvrName.Should().Contain("CVR", 
                because: "CVR format name must use CVR branding");
            cvrName.Should().NotContain("Database", 
                because: "CVR format name must not mention 'Database'");
            
            adbName.Should().Contain("Legacy", 
                because: "ADB format should be labeled as legacy");
            adbName.Should().NotContain("Database", 
                because: "ADB format name must not mention 'Database'");
            
            dbName.Should().Contain("CVR", 
                because: "Even .db files should be presented as CVR format");
            dbName.Should().NotContain("SQLite", 
                because: "DB format name must not mention 'SQLite'");
            
            debugName.Should().Contain("CVR", 
                because: "Debug format should maintain CVR branding");
            debugName.Should().Contain("Debug", 
                because: "Debug format should indicate it's for testing");
        }

        [Fact]
        public void RecordingConstants_MustEnforceCvrCompression()
        {
            // Arrange & Act
            var compressionRequired = RecordingConstants.FORCE_CVR_COMPRESSION;
            
            // Assert
            compressionRequired.Should().BeTrue(
                because: "CVR compression must be enforced to prevent exposing .db files to users");
        }

        [Fact]
        public void CvrFileExtension_IsCorrect()
        {
            // Arrange
            var testPath = "recording.cvr";
            
            // Act
            var isCvr = CvrFormat.IsCvrFile(testPath);
            
            // Assert
            isCvr.Should().BeTrue(
                because: "CVR extension detection must work correctly");
        }

        [Fact]
        public void DatabaseFileExtension_IsHiddenFromUsers()
        {
            // Arrange
            var dbPath = "recording.db";
            var cvrDebugPath = "recording.cvr-debug";
            
            // Act
            var dbIsDbFile = CvrFormat.IsDbFile(dbPath);
            var cvrDebugIsDbFile = CvrFormat.IsDbFile(cvrDebugPath);
            
            // Assert
            dbIsDbFile.Should().BeTrue(
                because: "Internal .db detection must work");
            cvrDebugIsDbFile.Should().BeTrue(
                because: "Internal .cvr-debug detection must work");
            
            // Note: These are internal database formats not exposed in file dialogs
        }

        [Fact]
        public void FileDialog_DefaultFilter_IsCvr()
        {
            // Arrange & Act
            var filters = RecordingFileLoader.GetFileFilters();
            var filterParts = filters.Split('|');
            
            // Assert - First filter should be "All Recording Files"
            filterParts[0].Should().Contain("Recording Files", 
                because: "Default filter should be for recording files");
            filterParts[1].Should().StartWith("*.cvr", 
                because: "CVR should be the primary extension in the default filter");
        }
    }
}
