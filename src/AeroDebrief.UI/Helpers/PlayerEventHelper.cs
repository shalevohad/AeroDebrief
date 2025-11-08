using System;
using System.Windows;
using AeroDebrief.UI.Events;

namespace AeroDebrief.UI.Helpers
{
    /// <summary>
    /// Helper class for working with player events
    /// Provides convenient methods for adding/removing event handlers and raising events
    /// </summary>
    public static class PlayerEventHelper
    {
        #region Seek Events

        public static void AddSeekRequestedHandler(this UIElement element, RoutedEventHandler<SeekRequestedEventArgs> handler)
        {
            element.AddHandler(PlayerEvents.SeekRequestedEvent, handler);
        }

        public static void RemoveSeekRequestedHandler(this UIElement element, RoutedEventHandler<SeekRequestedEventArgs> handler)
        {
            element.RemoveHandler(PlayerEvents.SeekRequestedEvent, handler);
        }

        public static void RaiseSeekRequested(this UIElement element, double normalizedPosition)
        {
            var args = new SeekRequestedEventArgs(PlayerEvents.SeekRequestedEvent, element, normalizedPosition);
            element.RaiseEvent(args);
        }

        #endregion

        #region Zoom Events

        public static void AddZoomChangedHandler(this UIElement element, RoutedEventHandler<ZoomChangedEventArgs> handler)
        {
            element.AddHandler(PlayerEvents.ZoomChangedEvent, handler);
        }

        public static void RemoveZoomChangedHandler(this UIElement element, RoutedEventHandler<ZoomChangedEventArgs> handler)
        {
            element.RemoveHandler(PlayerEvents.ZoomChangedEvent, handler);
        }

        public static void RaiseZoomChanged(this UIElement element, double startTime, double endTime, double zoomLevel)
        {
            var args = new ZoomChangedEventArgs(PlayerEvents.ZoomChangedEvent, element)
            {
                StartTime = startTime,
                EndTime = endTime,
                ZoomLevel = zoomLevel
            };
            element.RaiseEvent(args);
        }

        public static void AddZoomRegionSelectedHandler(this UIElement element, RoutedEventHandler<ZoomRegionSelectedEventArgs> handler)
        {
            element.AddHandler(PlayerEvents.ZoomRegionSelectedEvent, handler);
        }

        public static void RemoveZoomRegionSelectedHandler(this UIElement element, RoutedEventHandler<ZoomRegionSelectedEventArgs> handler)
        {
            element.RemoveHandler(PlayerEvents.ZoomRegionSelectedEvent, handler);
        }

        public static void RaiseZoomRegionSelected(this UIElement element, double startTime, double endTime)
        {
            var args = new ZoomRegionSelectedEventArgs(PlayerEvents.ZoomRegionSelectedEvent, element, startTime, endTime);
            element.RaiseEvent(args);
        }

        #endregion

        #region Transport Control Events

        public static void AddPlayRequestedHandler(this UIElement element, RoutedEventHandler handler)
        {
            element.AddHandler(PlayerEvents.PlayRequestedEvent, handler);
        }

        public static void RemovePlayRequestedHandler(this UIElement element, RoutedEventHandler handler)
        {
            element.RemoveHandler(PlayerEvents.PlayRequestedEvent, handler);
        }

        public static void RaisePlayRequested(this UIElement element)
        {
            element.RaiseEvent(new RoutedEventArgs(PlayerEvents.PlayRequestedEvent, element));
        }

        public static void AddPauseRequestedHandler(this UIElement element, RoutedEventHandler handler)
        {
            element.AddHandler(PlayerEvents.PauseRequestedEvent, handler);
        }

        public static void RemovePauseRequestedHandler(this UIElement element, RoutedEventHandler handler)
        {
            element.RemoveHandler(PlayerEvents.PauseRequestedEvent, handler);
        }

        public static void RaisePauseRequested(this UIElement element)
        {
            element.RaiseEvent(new RoutedEventArgs(PlayerEvents.PauseRequestedEvent, element));
        }

        public static void AddStopRequestedHandler(this UIElement element, RoutedEventHandler handler)
        {
            element.AddHandler(PlayerEvents.StopRequestedEvent, handler);
        }

        public static void RemoveStopRequestedHandler(this UIElement element, RoutedEventHandler handler)
        {
            element.RemoveHandler(PlayerEvents.StopRequestedEvent, handler);
        }

        public static void RaiseStopRequested(this UIElement element)
        {
            element.RaiseEvent(new RoutedEventArgs(PlayerEvents.StopRequestedEvent, element));
        }

        public static void AddSkipForwardRequestedHandler(this UIElement element, RoutedEventHandler<SkipEventArgs> handler)
        {
            element.AddHandler(PlayerEvents.SkipForwardRequestedEvent, handler);
        }

        public static void RemoveSkipForwardRequestedHandler(this UIElement element, RoutedEventHandler<SkipEventArgs> handler)
        {
            element.RemoveHandler(PlayerEvents.SkipForwardRequestedEvent, handler);
        }

        public static void RaiseSkipForwardRequested(this UIElement element, double intervalSeconds)
        {
            var args = new SkipEventArgs(PlayerEvents.SkipForwardRequestedEvent, element, intervalSeconds);
            element.RaiseEvent(args);
        }

        public static void AddSkipBackwardRequestedHandler(this UIElement element, RoutedEventHandler<SkipEventArgs> handler)
        {
            element.AddHandler(PlayerEvents.SkipBackwardRequestedEvent, handler);
        }

        public static void RemoveSkipBackwardRequestedHandler(this UIElement element, RoutedEventHandler<SkipEventArgs> handler)
        {
            element.RemoveHandler(PlayerEvents.SkipBackwardRequestedEvent, handler);
        }

        public static void RaiseSkipBackwardRequested(this UIElement element, double intervalSeconds)
        {
            var args = new SkipEventArgs(PlayerEvents.SkipBackwardRequestedEvent, element, intervalSeconds);
            element.RaiseEvent(args);
        }

        #endregion

        #region Waveform Events

        public static void AddWaveformSizeChangedHandler(this UIElement element, RoutedEventHandler<WaveformSizeChangedEventArgs> handler)
        {
            element.AddHandler(PlayerEvents.WaveformSizeChangedEvent, handler);
        }

        public static void RemoveWaveformSizeChangedHandler(this UIElement element, RoutedEventHandler<WaveformSizeChangedEventArgs> handler)
        {
            element.RemoveHandler(PlayerEvents.WaveformSizeChangedEvent, handler);
        }

        public static void RaiseWaveformSizeChanged(this UIElement element, double width, double height)
        {
            var args = new WaveformSizeChangedEventArgs(PlayerEvents.WaveformSizeChangedEvent, element)
            {
                NewWidth = width,
                NewHeight = height
            };
            element.RaiseEvent(args);
        }

        #endregion

        #region Minimap Events

        public static void AddMinimapClickedHandler(this UIElement element, RoutedEventHandler<MinimapClickedEventArgs> handler)
        {
            element.AddHandler(PlayerEvents.MinimapClickedEvent, handler);
        }

        public static void RemoveMinimapClickedHandler(this UIElement element, RoutedEventHandler<MinimapClickedEventArgs> handler)
        {
            element.RemoveHandler(PlayerEvents.MinimapClickedEvent, handler);
        }

        public static void RaiseMinimapClicked(this UIElement element, double startTime, double endTime)
        {
            var args = new MinimapClickedEventArgs(PlayerEvents.MinimapClickedEvent, element, startTime, endTime);
            element.RaiseEvent(args);
        }

        public static void AddMinimapDraggedHandler(this UIElement element, RoutedEventHandler<MinimapDraggedEventArgs> handler)
        {
            element.AddHandler(PlayerEvents.MinimapDraggedEvent, handler);
        }

        public static void RemoveMinimapDraggedHandler(this UIElement element, RoutedEventHandler<MinimapDraggedEventArgs> handler)
        {
            element.RemoveHandler(PlayerEvents.MinimapDraggedEvent, handler);
        }

        public static void RaiseMinimapDragged(this UIElement element, double startTime, double endTime)
        {
            var args = new MinimapDraggedEventArgs(PlayerEvents.MinimapDraggedEvent, element, startTime, endTime);
            element.RaiseEvent(args);
        }

        #endregion

        #region Frequency Events

        public static void AddFrequencySelectionChangedHandler(this UIElement element, RoutedEventHandler<FrequencySelectionChangedEventArgs> handler)
        {
            element.AddHandler(PlayerEvents.FrequencySelectionChangedEvent, handler);
        }

        public static void RemoveFrequencySelectionChangedHandler(this UIElement element, RoutedEventHandler<FrequencySelectionChangedEventArgs> handler)
        {
            element.RemoveHandler(PlayerEvents.FrequencySelectionChangedEvent, handler);
        }

        public static void RaiseFrequencySelectionChanged(this UIElement element, ViewModels.FrequencyViewModel frequency, bool isSelected)
        {
            var args = new FrequencySelectionChangedEventArgs(PlayerEvents.FrequencySelectionChangedEvent, element, frequency, isSelected);
            element.RaiseEvent(args);
        }

        public static void AddMixerValueChangedHandler(this UIElement element, RoutedEventHandler<MixerValueChangedEventArgs> handler)
        {
            element.AddHandler(PlayerEvents.MixerValueChangedEvent, handler);
        }

        public static void RemoveMixerValueChangedHandler(this UIElement element, RoutedEventHandler<MixerValueChangedEventArgs> handler)
        {
            element.RemoveHandler(PlayerEvents.MixerValueChangedEvent, handler);
        }

        public static void RaiseMixerValueChanged(this UIElement element, ViewModels.FrequencyViewModel frequency, string property, float value)
        {
            var args = new MixerValueChangedEventArgs(PlayerEvents.MixerValueChangedEvent, element, frequency, property, value);
            element.RaiseEvent(args);
        }

        public static void AddMixerBooleanChangedHandler(this UIElement element, RoutedEventHandler<MixerBooleanChangedEventArgs> handler)
        {
            element.AddHandler(PlayerEvents.MixerBooleanChangedEvent, handler);
        }

        public static void RemoveMixerBooleanChangedHandler(this UIElement element, RoutedEventHandler<MixerBooleanChangedEventArgs> handler)
        {
            element.RemoveHandler(PlayerEvents.MixerBooleanChangedEvent, handler);
        }

        public static void RaiseMixerBooleanChanged(this UIElement element, ViewModels.FrequencyViewModel frequency, string property, bool value, Core.Models.PlayerFrequencyInfo? player = null)
        {
            var args = new MixerBooleanChangedEventArgs(PlayerEvents.MixerBooleanChangedEvent, element, frequency, property, value, player);
            element.RaiseEvent(args);
        }

        #endregion

        #region Source Selection Events

        public static void AddSourceTypeSelectedHandler(this UIElement element, RoutedEventHandler<SourceTypeSelectedEventArgs> handler)
        {
            element.AddHandler(PlayerEvents.SourceTypeSelectedEvent, handler);
        }

        public static void RemoveSourceTypeSelectedHandler(this UIElement element, RoutedEventHandler<SourceTypeSelectedEventArgs> handler)
        {
            element.RemoveHandler(PlayerEvents.SourceTypeSelectedEvent, handler);
        }

        public static void RaiseSourceTypeSelected(this UIElement element, Controls.Player.SourceType sourceType)
        {
            var args = new SourceTypeSelectedEventArgs(PlayerEvents.SourceTypeSelectedEvent, element, sourceType);
            element.RaiseEvent(args);
        }

        public static void AddFilePanelRequestedHandler(this UIElement element, RoutedEventHandler handler)
        {
            element.AddHandler(PlayerEvents.FilePanelRequestedEvent, handler);
        }

        public static void RemoveFilePanelRequestedHandler(this UIElement element, RoutedEventHandler handler)
        {
            element.RemoveHandler(PlayerEvents.FilePanelRequestedEvent, handler);
        }

        public static void RaiseFilePanelRequested(this UIElement element)
        {
            element.RaiseEvent(new RoutedEventArgs(PlayerEvents.FilePanelRequestedEvent, element));
        }

        public static void AddServerPanelRequestedHandler(this UIElement element, RoutedEventHandler handler)
        {
            element.AddHandler(PlayerEvents.ServerPanelRequestedEvent, handler);
        }

        public static void RemoveServerPanelRequestedHandler(this UIElement element, RoutedEventHandler handler)
        {
            element.RemoveHandler(PlayerEvents.ServerPanelRequestedEvent, handler);
        }

        public static void RaiseServerPanelRequested(this UIElement element)
        {
            element.RaiseEvent(new RoutedEventArgs(PlayerEvents.ServerPanelRequestedEvent, element));
        }

        #endregion
    }
}
