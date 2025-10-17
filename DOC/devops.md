# DevOps and Automation

This document describes basic build/test automation guidance, and how to keep documentation in the `DOC` folder in sync on push/PR.

## Build and test

The solution targets .NET 9. Use the .NET SDK 9.x for building and running tests.

Basic steps locally:

```bash
# Restore
dotnet restore

# Build
dotnet build --configuration Release

# Run tests
dotnet test
```

## CI example (GitHub Actions)

A simple workflow to validate PRs and pushes should include at least:
- `dotnet restore`
- `dotnet build --configuration Release`
- `dotnet test --no-build`

Place the workflow in `/.github/workflows/ci.yml`.

## Keeping DOC in sync

You requested that the documentation be updated automatically when something is pushed or merged into `main` or when opening a new pull request that has been tested.

Two options:

1. Validate only
   - On `push` and `pull_request`, run `dotnet build` and any doc generation tooling (if used) and fail if docs are invalid or out-of-date.

2. Commit changes automatically (not recommended without approvals)
   - If you want automatic commits of generated docs, the CI can run a generation step and commit back to the branch using a bot account and push credentials. This requires careful policy and usually protection rules on `main`.

Recommended approach:
- Add a CI step that checks whether `DOC` content changed after generation and fails the build with a message instructing the author to run the doc generation locally and include the updates in their PR.

This keeps authors in control of commits while ensuring documentation is kept in sync.
