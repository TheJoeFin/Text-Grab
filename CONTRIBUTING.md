# Contributing to Text-Grab

Thank you for your interest in contributing to Text-Grab! This document provides guidelines and instructions for contributing to the project.

## Development Environment Setup

### Prerequisites

- **Windows 10/11** (required for full development and testing)
- **Visual Studio 2022** or **Visual Studio Code** with C# extension
- **.NET 9.0 SDK** or later: [Download .NET](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Windows 10 SDK 22621.0** (for Windows-specific APIs)

### Getting Started

1. Fork and clone the repository:
   ```bash
   git clone https://github.com/YOUR_USERNAME/Text-Grab.git
   cd Text-Grab
   ```

2. Restore dependencies:
   ```bash
   dotnet restore Text-Grab.sln
   ```

3. Build the project:
   ```bash
   dotnet build Text-Grab/Text-Grab.csproj -c Release
   ```

4. Run tests:
   ```bash
   dotnet test Tests/Tests.csproj
   ```

## Code Style and Formatting

Text-Grab uses **EditorConfig** (`.editorconfig`) to define code style rules. All code contributions must follow these conventions.

### Automatic Code Formatting

#### Option 1: Using Pre-commit Hooks (Recommended)

Pre-commit hooks automatically check code formatting before each commit. To set up:

1. **Install pre-commit** (requires Python):
   ```bash
   pip install pre-commit
   ```

2. **Install the git hooks**:
   ```bash
   pre-commit install
   ```

3. **Run manually** (optional, to check all files):
   ```bash
   pre-commit run --all-files
   ```

Once installed, pre-commit will automatically run on `git commit` and prevent commits with formatting issues.

#### Option 2: Using dotnet format Manually

You can manually check and fix formatting without pre-commit hooks:

1. **Check formatting** (verify only, no changes):
   ```bash
   dotnet format Text-Grab.sln --verify-no-changes
   ```

2. **Fix formatting** (apply changes):
   ```bash
   dotnet format Text-Grab.sln
   ```

### IDE Integration

#### Visual Studio 2022
- EditorConfig settings are applied automatically
- Use **Edit > Advanced > Format Document** (Ctrl+K, Ctrl+D) to format files

#### Visual Studio Code
- Install the **C# Dev Kit** extension
- EditorConfig support is built-in
- Use **Format Document** (Shift+Alt+F) to format files

### Code Style Rules

Key conventions (enforced by `.editorconfig`):
- **Indentation**: 4 spaces for C#, 2 spaces for YAML
- **Line endings**: CRLF (Windows-style)
- **Naming conventions**:
  - PascalCase for classes, methods, properties
  - camelCase for private fields with `_` prefix (e.g., `_myField`)
  - Interfaces start with `I` (e.g., `IMyInterface`)
- **Braces**: New line for all braces (Allman style)
- **Null checks**: Prefer null propagation (`?.`) and coalesce (`??`)

## Continuous Integration

All pull requests must pass CI checks before merging:

1. **Code style check**: Ensures code follows formatting rules
2. **Build**: Verifies the project compiles successfully
3. **Tests**: Runs all unit tests

The CI pipeline runs automatically on pull requests to the `dev` branch.

## Pull Request Process

1. **Create a feature branch** from `dev`:
   ```bash
   git checkout dev
   git pull origin dev
   git checkout -b feature/my-feature
   ```

2. **Make your changes** following code style guidelines

3. **Test your changes**:
   ```bash
   dotnet test Tests/Tests.csproj
   ```

4. **Format your code** (if not using pre-commit):
   ```bash
   dotnet format Text-Grab.sln
   ```

5. **Commit and push**:
   ```bash
   git add .
   git commit -m "Description of changes"
   git push origin feature/my-feature
   ```

6. **Create a pull request** targeting the `dev` branch

7. **Address review feedback** if requested

## Testing

- Write tests for new features in the `Tests/` directory
- Ensure all existing tests pass before submitting PR
- Run tests with: `dotnet test Tests/Tests.csproj`

## Questions or Issues?

- Open an issue on GitHub for bugs or feature requests
- Check existing issues before creating a new one
- Provide detailed information and steps to reproduce for bugs

## Code of Conduct

Please read and follow our [Code of Conduct](CODE_OF_CONDUCT.md).

## License

By contributing to Text-Grab, you agree that your contributions will be licensed under the MIT License.
