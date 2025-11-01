# AeroDebrief Documentation

Welcome to the AeroDebrief documentation! This folder contains comprehensive guides for both users and developers.

## Documentation Overview

### For Users

If you're looking to use AeroDebrief to record and analyze your DCS missions:

- **[Installation Guide](INSTALLATION.md)** - Start here! Step-by-step installation instructions
- **[User Guide](USER_GUIDE.md)** - Complete guide to using AeroDebrief features
  - Recording communications
  - Playing back recordings
  - Using the interface
  - Analytics and insights
  - Troubleshooting

### For Developers

If you want to contribute to AeroDebrief or understand how it works:

- **[Developer Guide](DEVELOPER_GUIDE.md)** - Complete developer documentation
  - Architecture overview
  - Development environment setup
  - Building and testing
  - Core components
  - Extending AeroDebrief
- **[Contributing Guide](CONTRIBUTING.md)** - How to contribute to the project
  - Code of conduct
  - Development process
  - Coding standards
  - Pull request guidelines

### Technical Documentation

For detailed technical implementation information:

- **[Technical Documentation](../DOC/README.md)** - In-depth technical details
  - [Architecture](../DOC/architecture.md) - System architecture and components
  - [Core Audio](../DOC/core-audio.md) - Audio processing implementation
  - [Analysis](../DOC/analysis.md) - Frequency analysis systems
  - [UI Integration](../DOC/ui.md) - User interface architecture
  - [Integrations](../DOC/integrations.md) - External integrations
  - [DevOps](../DOC/devops.md) - Build and CI/CD

## Quick Start

### I want to use AeroDebrief
1. Read the [Installation Guide](INSTALLATION.md)
2. Follow the [User Guide](USER_GUIDE.md) to get started

### I want to contribute code
1. Read the [Developer Guide](DEVELOPER_GUIDE.md)
2. Set up your development environment
3. Review the [Contributing Guide](CONTRIBUTING.md)
4. Check out the [Technical Documentation](../DOC/README.md)

### I want to understand how it works
1. Start with the [Developer Guide - Architecture](DEVELOPER_GUIDE.md#architecture)
2. Deep dive into [Technical Documentation](../DOC/README.md)
3. Explore the source code with this knowledge

## Documentation Structure

```
docs/                          # User and developer guides (you are here)
├── README.md                  # This file
├── INSTALLATION.md            # Installation instructions
├── USER_GUIDE.md              # User guide
├── DEVELOPER_GUIDE.md         # Developer guide
└── CONTRIBUTING.md            # Contribution guidelines

DOC/                           # Technical implementation details
├── README.md                  # Technical docs overview
├── architecture.md            # System architecture
├── architecture.puml          # Architecture diagrams (PlantUML)
├── core-audio.md              # Audio processing details
├── analysis.md                # Analysis systems
├── ui.md                      # UI architecture
├── integrations.md            # Integration patterns
└── devops.md                  # Build and CI/CD
```

## Key Features Documented

### Recording and Playback
- Multi-frequency recording
- Live SRS capture
- Timeline-based playback
- Audio export

### Visualization
- GPU-accelerated waveform rendering
- Multi-frequency waveform display
- Frequency tree view
- Real-time updates

### Analytics
- Presence network graphs
- Power level analysis
- Signal quality metrics
- Comprehensive statistics

### Advanced Features
- Frequency filtering and mixing
- Per-channel gain and pan controls
- Integration with external tools (TacView, DCS Lua)

## Getting Help

### Documentation Issues
If you find errors or omissions in the documentation:
- Create an issue on [GitHub Issues](https://github.com/shalevohad/AeroDebrief/issues)
- Tag it with the `documentation` label
- Suggest improvements or corrections

### Usage Questions
For questions about using AeroDebrief:
- Check the [User Guide FAQ](USER_GUIDE.md#faq)
- Search [GitHub Discussions](https://github.com/shalevohad/AeroDebrief/discussions)
- Create a new discussion if needed

### Development Questions
For questions about contributing:
- Review the [Developer Guide](DEVELOPER_GUIDE.md)
- Check the [Contributing Guide](CONTRIBUTING.md)
- Ask in [GitHub Discussions](https://github.com/shalevohad/AeroDebrief/discussions)

## Contributing to Documentation

Documentation improvements are always welcome! To contribute:

1. Follow the [Contributing Guide](CONTRIBUTING.md)
2. Make changes in the appropriate file
3. Ensure markdown formatting is correct
4. Submit a pull request with clear description

### Documentation Style Guide
- Use clear, simple language
- Provide examples where helpful
- Include screenshots for UI features
- Keep information up-to-date
- Cross-reference related topics
- Use proper markdown formatting

## Related Resources

### External Documentation
- [.NET 9 Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [WPF Documentation](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
- [NAudio Documentation](https://github.com/naudio/NAudio)
- [SRS Documentation](https://github.com/ciribob/DCS-SimpleRadioStandalone)
- [DCS World](https://www.digitalcombatsimulator.com/)

### Community Resources
- [GitHub Repository](https://github.com/shalevohad/AeroDebrief)
- [GitHub Issues](https://github.com/shalevohad/AeroDebrief/issues)
- [GitHub Discussions](https://github.com/shalevohad/AeroDebrief/discussions)
- [Releases](https://github.com/shalevohad/AeroDebrief/releases)

## Version Information

This documentation is for AeroDebrief and is kept up-to-date with the latest release.

- **Documentation Version**: Current
- **Last Updated**: 2024
- **Applicable Versions**: All current versions

For version-specific documentation, check the documentation in that specific release tag.

---

**Need help?** Start with the appropriate guide above or visit our [GitHub Discussions](https://github.com/shalevohad/AeroDebrief/discussions).

Built with ❤️ for the DCS World community
