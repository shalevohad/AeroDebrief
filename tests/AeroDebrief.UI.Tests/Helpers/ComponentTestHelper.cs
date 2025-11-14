using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AeroDebrief.UI.Tests.Helpers
{
    /// <summary>
    /// Helper class for testing WPF components.
    /// Provides utilities for creating components, pumping dispatcher, and waiting for events.
    /// </summary>
    public static class ComponentTestHelper
    {
        /// <summary>
        /// Creates a component on the UI thread and returns it.
        /// </summary>
        public static T CreateComponent<T>() where T : UserControl, new()
        {
            T? component = null;
            var createdEvent = new AutoResetEvent(false);

            var thread = new Thread(() =>
            {
                // Create dispatcher for this thread
                component = new T();
                
                // Force layout pass
                component.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                component.Arrange(new Rect(component.DesiredSize));
                component.UpdateLayout();

                createdEvent.Set();

                // Start dispatcher
                Dispatcher.Run();
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            // Wait for component creation
            createdEvent.WaitOne(TimeSpan.FromSeconds(5));

            return component ?? throw new InvalidOperationException("Failed to create component");
        }

        /// <summary>
        /// Executes an action on the component's dispatcher.
        /// </summary>
        public static void InvokeOnDispatcher(UserControl component, Action action)
        {
            if (component.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                component.Dispatcher.Invoke(action);
            }
        }

        /// <summary>
        /// Waits for dispatcher to process all pending operations.
        /// </summary>
        public static void PumpDispatcher(UserControl component, int maxIterations = 10)
        {
            for (int i = 0; i < maxIterations; i++)
            {
                component.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
        }

        /// <summary>
        /// Waits for a condition to be true, pumping the dispatcher while waiting.
        /// </summary>
        public static bool WaitForCondition(UserControl component, Func<bool> condition, TimeSpan timeout)
        {
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < timeout)
            {
                if (component.Dispatcher.CheckAccess())
                {
                    if (condition())
                        return true;
                }
                else
                {
                    bool result = false;
                    component.Dispatcher.Invoke(() => result = condition());
                    if (result)
                        return true;
                }

                PumpDispatcher(component, 1);
                Thread.Sleep(10);
            }

            return false;
        }

        /// <summary>
        /// Sets a dependency property value on the UI thread.
        /// </summary>
        public static void SetProperty<T>(UserControl component, DependencyProperty property, T value)
        {
            InvokeOnDispatcher(component, () => component.SetValue(property, value));
            PumpDispatcher(component);
        }

        /// <summary>
        /// Gets a dependency property value from the UI thread.
        /// </summary>
        public static T GetProperty<T>(UserControl component, DependencyProperty property)
        {
            T? result = default;
            InvokeOnDispatcher(component, () => result = (T)component.GetValue(property));
            return result!;
        }

        /// <summary>
        /// Simulates a button click on the UI thread.
        /// </summary>
        public static void ClickButton(Button button)
        {
            InvokeOnDispatcher((UserControl)button.Parent, () =>
            {
                var method = typeof(Button).GetMethod("OnClick", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                method?.Invoke(button, null);
            });
        }

        /// <summary>
        /// Simulates a key press on a component.
        /// </summary>
        public static void PressKey(UserControl component, System.Windows.Input.Key key)
        {
            InvokeOnDispatcher(component, () =>
            {
                var keyEventArgs = new System.Windows.Input.KeyEventArgs(
                    System.Windows.Input.Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(component),
                    0,
                    key);

                keyEventArgs.RoutedEvent = UIElement.KeyDownEvent;
                component.RaiseEvent(keyEventArgs);
            });
        }

        /// <summary>
        /// Waits for an event to be raised.
        /// </summary>
        public static bool WaitForEvent<TEventArgs>(
            Action<EventHandler<TEventArgs>> subscribe,
            Action<EventHandler<TEventArgs>> unsubscribe,
            Action trigger,
            TimeSpan timeout) where TEventArgs : EventArgs
        {
            var eventRaised = new AutoResetEvent(false);
            TEventArgs? capturedArgs = default;

            EventHandler<TEventArgs> handler = (sender, args) =>
            {
                capturedArgs = args;
                eventRaised.Set();
            };

            subscribe(handler);
            trigger();
            var result = eventRaised.WaitOne(timeout);
            unsubscribe(handler);

            return result;
        }

        /// <summary>
        /// Cleans up a component and shuts down its dispatcher.
        /// </summary>
        public static void CleanupComponent(UserControl component)
        {
            if (component?.Dispatcher != null && !component.Dispatcher.HasShutdownStarted)
            {
                component.Dispatcher.InvokeShutdown();
            }
        }
    }
}
