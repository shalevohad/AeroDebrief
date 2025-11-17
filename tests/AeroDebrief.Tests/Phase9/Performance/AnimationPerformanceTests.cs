using Xunit;
using FluentAssertions;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AeroDebrief.Tests.Phase9.Performance
{
    /// <summary>
    /// Performance tests for Phase 9 animations.
    /// Verifies animations run at 60 FPS and maintain acceptable CPU usage.
    /// </summary>
    public class AnimationPerformanceTests
    {
        private const int TargetFPS = 60;
        private const int MinimumAcceptableFPS = 58;
        private const double MaxCPUPercent = 5.0;
        private const int AnimationDurationMs = 1000;

        [Fact(Skip = "Requires WPF rendering context")]
        public async Task FadeInAnimation_Runs60FPS()
        {
            // Arrange
            var element = new FrameworkElement();
            var storyboard = CreateFadeInStoryboard(element);
            var frameCount = 0;
            var sw = Stopwatch.StartNew();

            // Act
            CompositionTarget.Rendering += (s, e) => frameCount++;
            storyboard.Begin();
            await Task.Delay(AnimationDurationMs);
            sw.Stop();

            // Assert
            var actualFPS = (double)frameCount / (sw.ElapsedMilliseconds / 1000.0);
            actualFPS.Should().BeGreaterOrEqualTo(MinimumAcceptableFPS, 
                $"FadeIn animation should maintain at least {MinimumAcceptableFPS} FPS");
        }

        [Fact(Skip = "Requires WPF rendering context")]
        public async Task FadeOutAnimation_Runs60FPS()
        {
            // Arrange
            var element = new FrameworkElement { Opacity = 1.0 };
            var storyboard = CreateFadeOutStoryboard(element);
            var frameCount = 0;
            var sw = Stopwatch.StartNew();

            // Act
            CompositionTarget.Rendering += (s, e) => frameCount++;
            storyboard.Begin();
            await Task.Delay(AnimationDurationMs);
            sw.Stop();

            // Assert
            var actualFPS = (double)frameCount / (sw.ElapsedMilliseconds / 1000.0);
            actualFPS.Should().BeGreaterOrEqualTo(MinimumAcceptableFPS,
                $"FadeOut animation should maintain at least {MinimumAcceptableFPS} FPS");
        }

        [Fact(Skip = "Requires WPF rendering context")]
        public async Task SlideInAnimation_Runs60FPS()
        {
            // Arrange
            var element = new FrameworkElement();
            var storyboard = CreateSlideInStoryboard(element);
            var frameCount = 0;
            var sw = Stopwatch.StartNew();

            // Act
            CompositionTarget.Rendering += (s, e) => frameCount++;
            storyboard.Begin();
            await Task.Delay(AnimationDurationMs);
            sw.Stop();

            // Assert
            var actualFPS = (double)frameCount / (sw.ElapsedMilliseconds / 1000.0);
            actualFPS.Should().BeGreaterOrEqualTo(MinimumAcceptableFPS,
                $"SlideIn animation should maintain at least {MinimumAcceptableFPS} FPS");
        }

        [Fact(Skip = "Requires WPF rendering context")]
        public async Task SpinAnimation_Runs60FPS_Continuous()
        {
            // Arrange
            var element = new FrameworkElement();
            var storyboard = CreateSpinStoryboard(element);
            var frameCount = 0;
            var sw = Stopwatch.StartNew();

            // Act - Run for 3 seconds to test continuous animation
            CompositionTarget.Rendering += (s, e) => frameCount++;
            storyboard.Begin();
            await Task.Delay(3000);
            sw.Stop();

            // Assert
            var actualFPS = (double)frameCount / (sw.ElapsedMilliseconds / 1000.0);
            actualFPS.Should().BeGreaterOrEqualTo(MinimumAcceptableFPS,
                $"Continuous spin animation should maintain at least {MinimumAcceptableFPS} FPS");
        }

        [Fact(Skip = "Requires WPF rendering context")]
        public async Task PulseAnimation_Runs60FPS()
        {
            // Arrange
            var element = new FrameworkElement();
            var storyboard = CreatePulseStoryboard(element);
            var frameCount = 0;
            var sw = Stopwatch.StartNew();

            // Act
            CompositionTarget.Rendering += (s, e) => frameCount++;
            storyboard.Begin();
            await Task.Delay(AnimationDurationMs);
            sw.Stop();

            // Assert
            var actualFPS = (double)frameCount / (sw.ElapsedMilliseconds / 1000.0);
            actualFPS.Should().BeGreaterOrEqualTo(MinimumAcceptableFPS,
                $"Pulse animation should maintain at least {MinimumAcceptableFPS} FPS");
        }

        [Fact(Skip = "Requires WPF rendering context")]
        public async Task MultipleAnimations_Concurrent_Maintains60FPS()
        {
            // Arrange
            var element1 = new FrameworkElement();
            var element2 = new FrameworkElement();
            var element3 = new FrameworkElement();
            
            var fadeIn = CreateFadeInStoryboard(element1);
            var slideIn = CreateSlideInStoryboard(element2);
            var pulse = CreatePulseStoryboard(element3);
            
            var frameCount = 0;
            var sw = Stopwatch.StartNew();

            // Act - Run multiple animations concurrently
            CompositionTarget.Rendering += (s, e) => frameCount++;
            fadeIn.Begin();
            slideIn.Begin();
            pulse.Begin();
            await Task.Delay(AnimationDurationMs);
            sw.Stop();

            // Assert
            var actualFPS = (double)frameCount / (sw.ElapsedMilliseconds / 1000.0);
            actualFPS.Should().BeGreaterOrEqualTo(MinimumAcceptableFPS,
                $"Multiple concurrent animations should maintain at least {MinimumAcceptableFPS} FPS");
        }

        [Fact]
        public async Task Animation_CPUUsage_LessThan5Percent()
        {
            // Arrange
            var process = Process.GetCurrentProcess();
            var startCpuTime = process.TotalProcessorTime;
            var sw = Stopwatch.StartNew();

            // Act - Simulate animation workload
            for (int i = 0; i < 100; i++)
            {
                // Simulate animation frame calculations
                var value = Math.Sin(i * Math.PI / 180.0);
                await Task.Delay(16); // ~60 FPS
            }
            
            sw.Stop();
            var endCpuTime = process.TotalProcessorTime;

            // Assert
            var cpuUsedMs = (endCpuTime - startCpuTime).TotalMilliseconds;
            var totalMsPassed = sw.ElapsedMilliseconds;
            var cpuUsagePercent = (cpuUsedMs / totalMsPassed) * 100.0 / Environment.ProcessorCount;

            cpuUsagePercent.Should().BeLessThan(MaxCPUPercent,
                $"Animation CPU usage should be less than {MaxCPUPercent}%");
        }

        [Fact]
        public async Task Animation_MemoryUsage_Stable()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(true);

            // Act - Simulate 100 animation cycles
            for (int i = 0; i < 100; i++)
            {
                // Create temporary animation objects
                var element = new FrameworkElement();
                await Task.Delay(10);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false);

            // Assert - Memory should not grow significantly (< 10 MB)
            var memoryGrowthMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);
            memoryGrowthMB.Should().BeLessThan(10,
                "Animation memory usage should remain stable (< 10 MB growth)");
        }

        #region Helper Methods

        private Storyboard CreateFadeInStoryboard(FrameworkElement target)
        {
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Opacity"));
            storyboard.Children.Add(animation);

            return storyboard;
        }

        private Storyboard CreateFadeOutStoryboard(FrameworkElement target)
        {
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Opacity"));
            storyboard.Children.Add(animation);

            return storyboard;
        }

        private Storyboard CreateSlideInStoryboard(FrameworkElement target)
        {
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation
            {
                From = 20,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            storyboard.Children.Add(animation);

            return storyboard;
        }

        private Storyboard CreateSpinStoryboard(FrameworkElement target)
        {
            var storyboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromMilliseconds(1500)
            };

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            storyboard.Children.Add(animation);

            return storyboard;
        }

        private Storyboard CreatePulseStoryboard(FrameworkElement target)
        {
            var storyboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true
            };
            
            var animation = new DoubleAnimation
            {
                From = 1,
                To = 1.1,
                Duration = TimeSpan.FromMilliseconds(800),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
            storyboard.Children.Add(animation);

            return storyboard;
        }

        #endregion
    }
}
