<#
.SYNOPSIS
Promotes main → beta branch for AeroDebrief builds on Windows
#>

Write-Host "🚀 Promoting main → beta (AeroDebrief Beta Build)" -ForegroundColor Cyan
Write-Host "----------------------------------------------------"

# Validate branch
$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "main") {
    Write-Host "❌ You must run this script from the 'main' branch." -ForegroundColor Red
    exit 1
}

# Ensure clean workspace
$changes = git status --porcelain
if ($changes) {
    Write-Host "❌ Uncommitted changes detected. Commit or stash before continuing." -ForegroundColor Red
    exit 1
}

# Fetch latest
git fetch origin
git pull origin main

# Checkout beta and merge
if (-not (git show-ref --verify --quiet refs/heads/beta)) {
    git checkout -b beta
} else {
    git checkout beta
    git pull origin beta
}
git merge main --no-edit
git push origin beta

# Tag for tracking
$tag = "beta-promote-" + (Get-Date -Format "yyyy.MM.dd.HHmm")
git tag -a $tag -m "Promoted main → beta"
git push origin $tag

Write-Host "✅ Promotion to beta completed successfully!" -ForegroundColor Green
Write-Host "AeroDebrief Beta build will trigger automatically via GitHub Actions."
