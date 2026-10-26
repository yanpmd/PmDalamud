Get-ChildItem -Path 'c:\Users\pm\Documents\Code\Dalamud\AetherBagsCN\AetherBags' -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' } | ForEach-Object {
    $lines = (Get-Content -LiteralPath $_.FullName | Measure-Object -Line).Lines
    [PSCustomObject]@{ Lines = $lines; Path = $_.FullName.Substring('c:\Users\pm\Documents\Code\Dalamud\AetherBagsCN\AetherBags\'.Length) }
} | Sort-Object Lines -Descending | Format-Table -AutoSize