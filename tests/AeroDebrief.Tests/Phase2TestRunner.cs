using System;
using System.Threading.Tasks;
using NLog;

namespace AeroDebrief.Tests
{
    /// <summary>
    /// Test runner for Phase 2 amplitude extraction pipeline tests.
    /// Can be called from UI or CLI to validate the implementation.
    /// </summary>
    public static class Phase2TestRunner
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// Run all Phase 2 tests.
        /// </summary>
        public static async Task<bool> RunAllPhase2TestsAsync()
        {
            Logger.Info("==============================================");
            Logger.Info("Phase 2: Amplitude Extraction Pipeline Tests");
            Logger.Info("==============================================");
            Logger.Info("");
            
            var allPassed = true;
            
            try
            {
                // Phase 2 Day 1: Unit tests (already run separately)
                Logger.Info("Phase 2 Day 1: Unit Tests");
                Logger.Info("----------------------------------------");
                AmplitudeExtractionTests.RunAllTests();
                Logger.Info("");
                
                // Phase 2.2: Integration tests with mock recordings
                Logger.Info("Phase 2.2: Integration Tests");
                Logger.Info("----------------------------------------");
                await Graphs.AmplitudeExtractionPipelineTests.RunAllTests();
                Logger.Info("");
                
                Logger.Info("==============================================");
                Logger.Info("? All Phase 2 Tests Completed");
                Logger.Info("==============================================");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Phase 2 tests failed with exception");
                allPassed = false;
            }
            
            return allPassed;
        }
        
        /// <summary>
        /// Run only Phase 2.2 integration tests.
        /// </summary>
        public static async Task<bool> RunPhase22TestsAsync()
        {
            Logger.Info("Running Phase 2.2 Integration Tests...");
            
            try
            {
                await Graphs.AmplitudeExtractionPipelineTests.RunAllTests();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Phase 2.2 tests failed");
                return false;
            }
        }
        
        /// <summary>
        /// Quick smoke test - runs fastest subset of tests.
        /// </summary>
        public static async Task<bool> RunSmokeTestsAsync()
        {
            Logger.Info("Running Phase 2 Smoke Tests (quick validation)...");
            
            try
            {
                // Run a few key unit tests
                Logger.Info("Unit tests:");
                AmplitudeExtractionTests.RunAllTests();
                
                // Run simple integration test
                Logger.Info("\nIntegration test:");
                // Note: Individual test methods are private, so we run all
                await Graphs.AmplitudeExtractionPipelineTests.RunAllTests();
                
                Logger.Info("? Smoke tests passed");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Smoke tests failed");
                return false;
            }
        }
    }
}
