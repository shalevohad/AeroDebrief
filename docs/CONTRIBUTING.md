# Contributing to AeroDebrief

Thank you for your interest in contributing to AeroDebrief! This document provides guidelines and instructions for contributing to the project.

## Table of Contents

1. [Code of Conduct](#code-of-conduct)
2. [Getting Started](#getting-started)
3. [How to Contribute](#how-to-contribute)
4. [Development Process](#development-process)
5. [Coding Standards](#coding-standards)
6. [Testing Guidelines](#testing-guidelines)
7. [Documentation](#documentation)
8. [Pull Request Process](#pull-request-process)
9. [Issue Reporting](#issue-reporting)
10. [Community](#community)

## Code of Conduct

### Our Pledge

We are committed to providing a welcoming and inclusive environment for all contributors. We expect all participants to:

- Be respectful and considerate
- Welcome newcomers and help them get started
- Focus on what is best for the community
- Show empathy towards other community members
- Accept constructive criticism gracefully

### Unacceptable Behavior

- Harassment, discrimination, or intimidation of any kind
- Offensive, derogatory, or insulting comments
- Public or private harassment
- Publishing others' private information without permission
- Other conduct which could reasonably be considered inappropriate

### Enforcement

Violations of the code of conduct may result in:
- Warning
- Temporary ban from the project
- Permanent ban from the project

Report violations to the project maintainers via GitHub Issues or email.

## Getting Started

### Prerequisites

Before contributing, ensure you have:

1. **Development Environment**: Set up according to [Developer Guide](DEVELOPER_GUIDE.md)
2. **Git Knowledge**: Basic understanding of Git and GitHub workflows
3. **C# and .NET**: Familiarity with C# and .NET development
4. **Domain Knowledge**: Understanding of audio processing and/or DCS World is helpful but not required

### Setting Up Your Development Environment

1. **Fork the Repository**:
   - Click the "Fork" button on the [AeroDebrief repository](https://github.com/shalevohad/AeroDebrief)
   - This creates a copy under your GitHub account

2. **Clone Your Fork**:
   ```bash
   git clone https://github.com/YOUR-USERNAME/AeroDebrief.git
   cd AeroDebrief
   ```

3. **Add Upstream Remote**:
   ```bash
   git remote add upstream https://github.com/shalevohad/AeroDebrief.git
   ```

4. **Initialize Submodules**:
   ```bash
   git submodule update --init --recursive
   ```

5. **Install Dependencies**:
   ```bash
   dotnet restore
   ```

6. **Verify Setup**:
   ```bash
   dotnet build
   dotnet test
   ```

## How to Contribute

There are many ways to contribute to AeroDebrief:

### 1. Code Contributions

- **Bug Fixes**: Fix reported issues
- **New Features**: Implement requested features
- **Performance Improvements**: Optimize existing code
- **Refactoring**: Improve code structure and maintainability

### 2. Documentation

- **User Documentation**: Improve user guides and tutorials
- **Developer Documentation**: Enhance technical documentation
- **Code Comments**: Add or improve inline documentation
- **Examples**: Create sample code or usage examples

### 3. Testing

- **Bug Reports**: Report bugs with detailed reproduction steps
- **Test Coverage**: Add unit or integration tests
- **Manual Testing**: Test new features and report findings
- **Performance Testing**: Benchmark and profile code

### 4. Design and UX

- **UI Improvements**: Enhance user interface design
- **UX Feedback**: Provide usability feedback
- **Icon/Asset Design**: Create or improve visual assets

### 5. Community Support

- **Answer Questions**: Help other users in Discussions
- **Review PRs**: Review and provide feedback on pull requests
- **Triage Issues**: Help organize and prioritize issues

## Development Process

### Workflow Overview

1. **Find or Create an Issue**: Ensure there's an issue tracking the work
2. **Discuss**: Comment on the issue to discuss your approach
3. **Create Branch**: Create a feature branch from `main`
4. **Develop**: Write code, tests, and documentation
5. **Test**: Ensure all tests pass and code works as expected
6. **Commit**: Make clear, focused commits with good messages
7. **Push**: Push your branch to your fork
8. **Pull Request**: Create a PR against the main repository
9. **Review**: Address feedback from reviewers
10. **Merge**: Maintainers will merge approved PRs

### Branch Naming Conventions

Use descriptive branch names:

- `feature/add-tacview-export` - New features
- `fix/audio-playback-crash` - Bug fixes
- `docs/update-installation-guide` - Documentation updates
- `refactor/simplify-audio-pipeline` - Code refactoring
- `test/add-frequency-analysis-tests` - Test additions

### Keeping Your Fork Updated

Regularly sync with upstream:

```bash
# Fetch upstream changes
git fetch upstream

# Merge upstream main into your main
git checkout main
git merge upstream/main

# Update your feature branch
git checkout feature/your-feature
git rebase main
```

## Coding Standards

### General Principles

- **SOLID Principles**: Follow SOLID design principles
- **DRY**: Don't Repeat Yourself
- **KISS**: Keep It Simple, Stupid
- **YAGNI**: You Aren't Gonna Need It

### C# Style Guide

Follow Microsoft's [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions):

```csharp
// Use PascalCase for public members
public class AudioPacketReader
{
    // Use PascalCase for properties
    public string FilePath { get; set; }
    
    // Use camelCase with underscore prefix for private fields
    private readonly IAudioProcessor _audioProcessor;
    
    // Use PascalCase for methods
    public void LoadFile(string path)
    {
        // Use camelCase for local variables
        var fileInfo = new FileInfo(path);
        
        // Use meaningful names
        if (fileInfo.Exists)
        {
            ProcessFile(fileInfo);
        }
    }
    
    // Private methods also use PascalCase
    private void ProcessFile(FileInfo info)
    {
        // Implementation
    }
}
```

### Code Formatting

- **Indentation**: Use 4 spaces (not tabs)
- **Line Length**: Aim for 120 characters max
- **Braces**: Use Allman style (braces on new line)
- **Spacing**: Add spaces around operators and after keywords

### EditorConfig

The repository includes an `.editorconfig` file. Ensure your IDE respects it:

```ini
# Example from .editorconfig
[*.cs]
indent_style = space
indent_size = 4
trim_trailing_whitespace = true
insert_final_newline = true
```

### XML Documentation

Document all public APIs:

```csharp
/// <summary>
/// Loads an audio recording file for playback.
/// </summary>
/// <param name="filePath">Path to the .srs recording file.</param>
/// <returns>True if the file was loaded successfully; otherwise, false.</returns>
/// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
/// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
public bool LoadFile(string filePath)
{
    // Implementation
}
```

### Naming Conventions

- **Classes**: `AudioPacketReader`, `FrequencyAnalysisService`
- **Interfaces**: `IAudioProcessor`, `IFrequencyAnalyzer`
- **Methods**: `LoadFile`, `ProcessPacket`, `CalculateDuration`
- **Properties**: `TotalDuration`, `IsPlaying`, `CurrentPosition`
- **Constants**: `MaxBufferSize`, `DefaultSampleRate`
- **Private Fields**: `_audioProcessor`, `_bufferManager`

## Testing Guidelines

### Test Requirements

All contributions must include appropriate tests:

- **Bug Fixes**: Add tests that would have caught the bug
- **New Features**: Add tests for all new functionality
- **Refactoring**: Ensure existing tests still pass

### Writing Tests

Use xUnit and FluentAssertions:

```csharp
public class FrequencyAnalyzerTests
{
    [Fact]
    public void AnalyzeFile_WithValidFile_ReturnsFrequencies()
    {
        // Arrange
        var analyzer = new FrequencyAnalyzer();
        var testFile = "TestData/sample.srs";
        
        // Act
        var frequencies = analyzer.AnalyzeFile(testFile);
        
        // Assert
        frequencies.Should().NotBeEmpty();
        frequencies.Should().AllSatisfy(f => 
        {
            f.Frequency.Should().BeGreaterThan(0);
            f.PacketCount.Should().BeGreaterThan(0);
        });
    }
    
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("nonexistent.srs")]
    public void AnalyzeFile_WithInvalidFile_ThrowsException(string path)
    {
        // Arrange
        var analyzer = new FrequencyAnalyzer();
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => analyzer.AnalyzeFile(path));
    }
}
```

### Test Coverage

- Aim for **80%+ code coverage** for new code
- Cover **happy paths** and **error cases**
- Test **edge cases** and **boundary conditions**
- Include **integration tests** for complex workflows

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific tests
dotnet test --filter "FullyQualifiedName~FrequencyAnalyzer"
```

## Documentation

### Documentation Requirements

Documentation should accompany all contributions:

- **Code Documentation**: XML comments for public APIs
- **README Updates**: Update README if adding major features
- **User Guide**: Update user guide for user-facing changes
- **Developer Guide**: Update developer guide for architectural changes
- **CHANGELOG**: Add entry to CHANGELOG (if exists)

### Documentation Style

- Use clear, concise language
- Provide examples where appropriate
- Link to related documentation
- Keep documentation up-to-date with code changes

### Example Documentation

```csharp
/// <summary>
/// Analyzes audio packets to identify active frequencies and player activity.
/// </summary>
/// <remarks>
/// This service maintains real-time analysis state and provides events for UI updates.
/// It uses concurrent data structures for thread-safe operation.
/// 
/// Example usage:
/// <code>
/// var service = new FrequencyAnalysisService();
/// service.AnalysisUpdated += OnAnalysisUpdated;
/// service.ProcessPacket(packet);
/// </code>
/// </remarks>
public class FrequencyAnalysisService
{
    // Implementation
}
```

## Pull Request Process

### Before Creating a PR

- [ ] All tests pass locally
- [ ] Code follows project style guidelines
- [ ] Documentation is updated
- [ ] Commits are clean and well-organized
- [ ] Branch is up-to-date with main

### Creating a Pull Request

1. **Push Your Branch**:
   ```bash
   git push origin feature/your-feature
   ```

2. **Open PR on GitHub**:
   - Go to your fork on GitHub
   - Click "New Pull Request"
   - Select your branch

3. **Fill PR Template**:
   - Provide a clear title
   - Describe what the PR does
   - Reference related issues
   - List any breaking changes
   - Add screenshots for UI changes

### PR Title Format

Use [Conventional Commits](https://www.conventionalcommits.org/) format:

- `feat: add TacView export functionality`
- `fix: resolve audio playback crash on seek`
- `docs: update installation guide`
- `refactor: simplify frequency analysis logic`
- `test: add integration tests for playback`
- `perf: optimize waveform rendering`
- `chore: update dependencies`

### PR Description Template

```markdown
## Description
Brief description of changes

## Related Issues
Closes #123
Relates to #456

## Changes Made
- Added new feature X
- Fixed bug Y
- Improved performance of Z

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing completed

## Screenshots
(If applicable)

## Breaking Changes
(If any)

## Checklist
- [ ] Code follows style guidelines
- [ ] Tests pass
- [ ] Documentation updated
- [ ] No new warnings
```

### Review Process

1. **Automated Checks**: CI builds and tests run automatically
2. **Code Review**: Maintainers review the code
3. **Feedback**: Address any requested changes
4. **Approval**: Once approved, maintainers will merge
5. **Cleanup**: Delete your feature branch after merge

### Addressing Feedback

```bash
# Make changes based on feedback
git add .
git commit -m "Address review feedback"
git push origin feature/your-feature
```

## Issue Reporting

### Before Reporting

- Search existing issues to avoid duplicates
- Verify the issue exists in the latest version
- Gather relevant information (logs, screenshots, etc.)

### Bug Report Template

```markdown
## Bug Description
Clear description of the bug

## Steps to Reproduce
1. Step one
2. Step two
3. Step three

## Expected Behavior
What should happen

## Actual Behavior
What actually happens

## Environment
- OS: Windows 10/11
- AeroDebrief Version: vX.X.X
- .NET Version: 9.0.X
- SRS Version: X.X.X

## Logs
```
Paste relevant logs here
```

## Screenshots
(If applicable)

## Additional Context
Any other relevant information
```

### Feature Request Template

```markdown
## Feature Description
Clear description of the proposed feature

## Use Case
Why is this feature needed?

## Proposed Solution
How should this feature work?

## Alternatives Considered
Other approaches you've thought about

## Additional Context
Any other relevant information
```

## Community

### Communication Channels

- **GitHub Issues**: Bug reports and feature requests
- **GitHub Discussions**: General questions and discussions
- **Pull Requests**: Code reviews and contributions

### Getting Help

If you need help:

1. Check existing documentation
2. Search GitHub Issues and Discussions
3. Ask in GitHub Discussions
4. Create an issue if you find a bug

### Recognition

Contributors will be recognized in:
- Repository contributors list
- Release notes (for significant contributions)
- CONTRIBUTORS file (if exists)

## Questions?

If you have questions about contributing:

- Review the [Developer Guide](DEVELOPER_GUIDE.md)
- Ask in GitHub Discussions
- Comment on relevant issues

---

**Thank you for contributing to AeroDebrief!**  
Your contributions help make AeroDebrief better for the entire DCS community.
