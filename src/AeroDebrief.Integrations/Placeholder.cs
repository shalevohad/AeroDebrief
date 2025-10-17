namespace AeroDebrief.Integrations
{
    /// <summary>
    /// Placeholder container for integration helpers.
    ///
    /// Use this project to implement connectors and exporters for third-party
    /// tools. Typical integrations include:
    /// - TacView: export or stream flight data to TacView format so flights can be
    ///   visualised alongside recorded audio metadata.
    /// - DCS Lua export: helpers for generating Lua-based exports from recorded
    ///   session metadata.
    ///
    /// Current contents are intentionally minimal — add focused integration
    /// classes and document their usage here as implementations are added.
    /// </summary>
    public class IntegrationsPlaceholder
    {
        // Example integration TODOs:
        // - Implement TacView exporter that translates SRClient/Radar state + audio metadata
        //   into TacView "tracks" or markers. Prefer adding a dedicated `TacViewExporter` class.
        // - Implement Lua export helpers for DCS mission/session export where useful.
    }
}
