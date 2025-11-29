using System;
using System.Linq;
using AeroDebrief.UI.Services.Audio;
using NLog;

namespace AeroDebrief.Tests
{
    /// <summary>
    /// Manual tests for amplitude extraction and dBFS conversion utilities.
    /// Tests Phase 2 Day 1 implementation.
    /// </summary>
    public static class AmplitudeExtractionTests
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// Runs all amplitude extraction tests.
        /// </summary>
        public static void RunAllTests()
        {
            Logger.Info("======================================");
            Logger.Info("Starting Amplitude Extraction Tests (Peak Amplitude)");
            Logger.Info("======================================");

            var passedTests = 0;
            var totalTests = 0;

            // dBFS Conversion Tests
            totalTests++; if (Test_DbFS_SilenceReturnsMinimum()) passedTests++;
            totalTests++; if (Test_DbFS_FullScaleReturnsZero()) passedTests++;
            totalTests++; if (Test_DbFS_HalfScaleReturnsNegative6()) passedTests++;
            totalTests++; if (Test_DbFS_QuarterScaleReturnsNegative12()) passedTests++;
            totalTests++; if (Test_DbFS_VeryQuietReturnsMinimum()) passedTests++;
            totalTests++; if (Test_DbFS_RoundTrip()) passedTests++;
            totalTests++; if (Test_DbFS_Pcm16BitMax()) passedTests++;
            totalTests++; if (Test_DbFS_IsValidDbFS()) passedTests++;
            totalTests++; if (Test_DbFS_ClampDbFS()) passedTests++;
            
            // Peak Amplitude Calculation Tests
            totalTests++; if (Test_PeakAmplitude_EmptyArray()) passedTests++;
            totalTests++; if (Test_PeakAmplitude_AllZeros()) passedTests++;
            totalTests++; if (Test_PeakAmplitude_ConstantValue()) passedTests++;
            totalTests++; if (Test_PeakAmplitude_SineWave()) passedTests++;
            totalTests++; if (Test_PeakAmplitude_PositiveAndNegative()) passedTests++;
            totalTests++; if (Test_PeakAmplitude_MixedValues()) passedTests++;
            totalTests++; if (Test_PeakAmplitude_ToDbFS()) passedTests++;

            Logger.Info("======================================");
            Logger.Info($"Tests Completed: {passedTests}/{totalTests} passed");
            Logger.Info("======================================");
        }

        #region dBFS Conversion Tests

        private static bool Test_DbFS_SilenceReturnsMinimum()
        {
            try
            {
                double rms = 0.0;
                double dbFS = DbFSConverter.RmsToDbFS(rms);
                
                if (Math.Abs(dbFS - DbFSConverter.MinDbFS) < 0.01)
                {
                    Logger.Info("? DbFS_SilenceReturnsMinimum PASSED");
                    return true;
                }
                
                Logger.Error($"? DbFS_SilenceReturnsMinimum FAILED: Expected {DbFSConverter.MinDbFS}, got {dbFS}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? DbFS_SilenceReturnsMinimum FAILED with exception");
                return false;
            }
        }

        private static bool Test_DbFS_FullScaleReturnsZero()
        {
            try
            {
                double rms = 1.0;
                double dbFS = DbFSConverter.RmsToDbFS(rms);
                
                if (Math.Abs(dbFS - 0.0) < 0.01)
                {
                    Logger.Info("? DbFS_FullScaleReturnsZero PASSED");
                    return true;
                }
                
                Logger.Error($"? DbFS_FullScaleReturnsZero FAILED: Expected 0.0, got {dbFS}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? DbFS_FullScaleReturnsZero FAILED with exception");
                return false;
            }
        }

        private static bool Test_DbFS_HalfScaleReturnsNegative6()
        {
            try
            {
                double rms = 0.5;
                double dbFS = DbFSConverter.RmsToDbFS(rms);
                
                // 20 * log10(0.5) = -6.02 dB
                if (Math.Abs(dbFS - (-6.02)) < 0.1)
                {
                    Logger.Info($"? DbFS_HalfScaleReturnsNegative6 PASSED: {dbFS:F2} dBFS");
                    return true;
                }
                
                Logger.Error($"? DbFS_HalfScaleReturnsNegative6 FAILED: Expected -6.02, got {dbFS}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? DbFS_HalfScaleReturnsNegative6 FAILED with exception");
                return false;
            }
        }

        private static bool Test_DbFS_QuarterScaleReturnsNegative12()
        {
            try
            {
                double rms = 0.25;
                double dbFS = DbFSConverter.RmsToDbFS(rms);
                
                // 20 * log10(0.25) = -12.04 dB
                if (Math.Abs(dbFS - (-12.04)) < 0.1)
                {
                    Logger.Info($"? DbFS_QuarterScaleReturnsNegative12 PASSED: {dbFS:F2} dBFS");
                    return true;
                }
                
                Logger.Error($"? DbFS_QuarterScaleReturnsNegative12 FAILED: Expected -12.04, got {dbFS}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? DbFS_QuarterScaleReturnsNegative12 FAILED with exception");
                return false;
            }
        }

        private static bool Test_DbFS_VeryQuietReturnsMinimum()
        {
            try
            {
                double rms = 1e-7; // Below minimum threshold
                double dbFS = DbFSConverter.RmsToDbFS(rms);
                
                if (Math.Abs(dbFS - DbFSConverter.MinDbFS) < 0.01)
                {
                    Logger.Info("? DbFS_VeryQuietReturnsMinimum PASSED");
                    return true;
                }
                
                Logger.Error($"? DbFS_VeryQuietReturnsMinimum FAILED: Expected {DbFSConverter.MinDbFS}, got {dbFS}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? DbFS_VeryQuietReturnsMinimum FAILED with exception");
                return false;
            }
        }

        private static bool Test_DbFS_RoundTrip()
        {
            try
            {
                double[] testValues = { 0.1, 0.25, 0.5, 0.75, 1.0 };
                
                foreach (var original in testValues)
                {
                    var dbFS = DbFSConverter.LinearToDbFS(original);
                    var restored = DbFSConverter.DbFSToLinear(dbFS);
                    
                    if (Math.Abs(restored - original) > 0.001)
                    {
                        Logger.Error($"? DbFS_RoundTrip FAILED: {original} -> {dbFS} dBFS -> {restored}");
                        return false;
                    }
                }
                
                Logger.Info("? DbFS_RoundTrip PASSED for all test values");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? DbFS_RoundTrip FAILED with exception");
                return false;
            }
        }

        private static bool Test_DbFS_Pcm16BitMax()
        {
            try
            {
                short pcmValue = 32767; // Max for 16-bit
                double dbFS = DbFSConverter.PcmToDbFS(pcmValue);
                
                if (Math.Abs(dbFS - 0.0) < 0.01)
                {
                    Logger.Info($"? DbFS_Pcm16BitMax PASSED: {dbFS:F2} dBFS");
                    return true;
                }
                
                Logger.Error($"? DbFS_Pcm16BitMax FAILED: Expected 0.0, got {dbFS}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? DbFS_Pcm16BitMax FAILED with exception");
                return false;
            }
        }

        private static bool Test_DbFS_IsValidDbFS()
        {
            try
            {
                bool allPassed = true;
                
                if (!DbFSConverter.IsValidDbFS(0.0)) { Logger.Error("0.0 should be valid"); allPassed = false; }
                if (!DbFSConverter.IsValidDbFS(-60.0)) { Logger.Error("-60.0 should be valid"); allPassed = false; }
                if (!DbFSConverter.IsValidDbFS(-120.0)) { Logger.Error("-120.0 should be valid"); allPassed = false; }
                if (DbFSConverter.IsValidDbFS(10.0)) { Logger.Error("10.0 should be invalid"); allPassed = false; }
                if (DbFSConverter.IsValidDbFS(-150.0)) { Logger.Error("-150.0 should be invalid"); allPassed = false; }
                if (DbFSConverter.IsValidDbFS(double.NaN)) { Logger.Error("NaN should be invalid"); allPassed = false; }
                
                if (allPassed)
                {
                    Logger.Info("? DbFS_IsValidDbFS PASSED");
                    return true;
                }
                
                Logger.Error("? DbFS_IsValidDbFS FAILED");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? DbFS_IsValidDbFS FAILED with exception");
                return false;
            }
        }

        private static bool Test_DbFS_ClampDbFS()
        {
            try
            {
                if (Math.Abs(DbFSConverter.ClampDbFS(10.0) - 0.0) > 0.01) return false;
                if (Math.Abs(DbFSConverter.ClampDbFS(-150.0) - (-120.0)) > 0.01) return false;
                if (Math.Abs(DbFSConverter.ClampDbFS(double.NaN) - (-120.0)) > 0.01) return false;
                if (Math.Abs(DbFSConverter.ClampDbFS(-60.0) - (-60.0)) > 0.01) return false;
                
                Logger.Info("? DbFS_ClampDbFS PASSED");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? DbFS_ClampDbFS FAILED with exception");
                return false;
            }
        }

        #endregion

        #region Peak Amplitude Calculation Tests

        private static bool Test_PeakAmplitude_EmptyArray()
        {
            try
            {
                var samples = new float[0];
                double peak = AmplitudeExtractor.CalculatePeakAmplitude(samples);
                
                if (Math.Abs(peak - 0.0) < 0.001)
                {
                    Logger.Info("? PeakAmplitude_EmptyArray PASSED");
                    return true;
                }
                
                Logger.Error($"? PeakAmplitude_EmptyArray FAILED: Expected 0.0, got {peak}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? PeakAmplitude_EmptyArray FAILED with exception");
                return false;
            }
        }

        private static bool Test_PeakAmplitude_AllZeros()
        {
            try
            {
                var samples = new float[100];
                double peak = AmplitudeExtractor.CalculatePeakAmplitude(samples);
                
                if (Math.Abs(peak - 0.0) < 0.001)
                {
                    Logger.Info("? PeakAmplitude_AllZeros PASSED");
                    return true;
                }
                
                Logger.Error($"? PeakAmplitude_AllZeros FAILED: Expected 0.0, got {peak}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? PeakAmplitude_AllZeros FAILED with exception");
                return false;
            }
        }

        private static bool Test_PeakAmplitude_ConstantValue()
        {
            try
            {
                var samples = Enumerable.Repeat(0.5f, 100).ToArray();
                double peak = AmplitudeExtractor.CalculatePeakAmplitude(samples);
                
                if (Math.Abs(peak - 0.5) < 0.001)
                {
                    Logger.Info($"? PeakAmplitude_ConstantValue PASSED: {peak:F3}");
                    return true;
                }
                
                Logger.Error($"? PeakAmplitude_ConstantValue FAILED: Expected 0.5, got {peak}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? PeakAmplitude_ConstantValue FAILED with exception");
                return false;
            }
        }

        private static bool Test_PeakAmplitude_SineWave()
        {
            try
            {
                // For a sine wave, peak amplitude should equal the sine amplitude
                float amplitude = 1.0f;
                var samples = GenerateSineWave(amplitude, frequency: 440.0, sampleRate: 48000, duration: 0.1);
                double peak = AmplitudeExtractor.CalculatePeakAmplitude(samples);
                
                // Peak should be very close to the amplitude
                if (Math.Abs(peak - amplitude) < 0.01)
                {
                    Logger.Info($"? PeakAmplitude_SineWave PASSED: {peak:F3} (expected {amplitude:F3})");
                    return true;
                }
                
                Logger.Error($"? PeakAmplitude_SineWave FAILED: Expected {amplitude:F3}, got {peak:F3}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? PeakAmplitude_SineWave FAILED with exception");
                return false;
            }
        }

        private static bool Test_PeakAmplitude_PositiveAndNegative()
        {
            try
            {
                var positiveSamples = Enumerable.Repeat(0.5f, 100).ToArray();
                var negativeSamples = Enumerable.Repeat(-0.5f, 100).ToArray();
                
                double peakPositive = AmplitudeExtractor.CalculatePeakAmplitude(positiveSamples);
                double peakNegative = AmplitudeExtractor.CalculatePeakAmplitude(negativeSamples);
                
                if (Math.Abs(peakPositive - peakNegative) < 0.001)
                {
                    Logger.Info($"? PeakAmplitude_PositiveAndNegative PASSED: {peakPositive:F3}");
                    return true;
                }
                
                Logger.Error($"? PeakAmplitude_PositiveAndNegative FAILED: Positive={peakPositive:F3}, Negative={peakNegative:F3}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? PeakAmplitude_PositiveAndNegative FAILED with exception");
                return false;
            }
        }

        private static bool Test_PeakAmplitude_MixedValues()
        {
            try
            {
                var samples = new float[] { 0.1f, -0.3f, 0.7f, -0.5f, 0.2f };
                double peak = AmplitudeExtractor.CalculatePeakAmplitude(samples);
                
                // Peak should be 0.7 (maximum absolute value)
                if (Math.Abs(peak - 0.7) < 0.001)
                {
                    Logger.Info($"? PeakAmplitude_MixedValues PASSED: {peak:F3}");
                    return true;
                }
                
                Logger.Error($"? PeakAmplitude_MixedValues FAILED: Expected 0.7, got {peak}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? PeakAmplitude_MixedValues FAILED with exception");
                return false;
            }
        }

        private static bool Test_PeakAmplitude_ToDbFS()
        {
            try
            {
                // Test peak amplitude to dBFS conversion
                var samples = new float[] { 0.5f, -0.3f, 0.2f };
                double peak = AmplitudeExtractor.CalculatePeakAmplitude(samples);
                double dbFS = DbFSConverter.LinearToDbFS(peak);
                
                // Peak of 0.5 should be -6 dBFS
                if (Math.Abs(dbFS - (-6.02)) < 0.1)
                {
                    Logger.Info($"? PeakAmplitude_ToDbFS PASSED: Peak={peak:F3}, dBFS={dbFS:F2}");
                    return true;
                }
                
                Logger.Error($"? PeakAmplitude_ToDbFS FAILED: Peak={peak:F3}, Expected -6.02 dBFS, got {dbFS:F2}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "? PeakAmplitude_ToDbFS FAILED with exception");
                return false;
            }
        }

        #endregion

        #region Helper Methods

        private static float[] GenerateSineWave(float amplitude, double frequency, int sampleRate, double duration)
        {
            int sampleCount = (int)(sampleRate * duration);
            var samples = new float[sampleCount];
            
            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / sampleRate;
                samples[i] = amplitude * (float)Math.Sin(2.0 * Math.PI * frequency * t);
            }
            
            return samples;
        }

        #endregion
    }
}
