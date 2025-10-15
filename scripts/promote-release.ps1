<#
.SYNOPSIS
Promotes beta → release branch for AeroDebrief production builds on Windows
#>

Write-Host "🚀 Promoting beta → release (AeroDebrief Production Release)" -ForegroundColor Cyan
Write-Host "------------------------------------------------------------"

# Validate branch
$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "beta") {
    Write-Host "❌ You must run this script from the 'beta' branch." -ForegroundColor Red
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
git pull origin beta

# Checkout release and merge
if (-not (git show-ref --verify --quiet refs/heads/release)) {
    git checkout -b release
} else {
    git checkout release
    git pull origin release
}
git merge beta --no-edit
git push origin release

# Tag version
$version = "v" + (Get-Date -Format "yyyy.MM.dd.HHmm")
git tag -a $version -m "Official AeroDebrief Release $version"
git push origin $version

Write-Host "✅ Promotion to release completed successfully!" -ForegroundColor Green
Write-Host "AeroDebrief Production release build will trigger automatically."
