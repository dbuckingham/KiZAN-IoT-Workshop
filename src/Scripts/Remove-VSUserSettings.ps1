# Find the root of the repository.
$root = git rev-parse --show-toplevel

# Remove Visual Studio user project option files.
Get-ChildItem -Path $root\src -Filter *.user -Recurse | Remove-Item