# Regenerates Resources/UIStrings.cs from Resources/UIStrings.resx.
#
# Run after adding, renaming or removing an entry in the resx. A test asserts the two stay in
# step, so forgetting to run this fails the build rather than producing a blank label at runtime.
#
# Hand-rolled rather than using ResXFileCodeGenerator, which only runs inside Visual Studio and
# so would leave a command-line build with a stale accessor. Same tool as the YAML plugin's.

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..\multifarious.Icu.BatchTasks'
$resx = Join-Path $project 'Resources\UIStrings.resx'
$out = Join-Path $project 'Resources\UIStrings.cs'

[xml]$document = Get-Content $resx
$entries = $document.root.data | Where-Object { $_.name }

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('using System.Globalization;')
[void]$sb.AppendLine('using System.Resources;')
[void]$sb.AppendLine()
[void]$sb.AppendLine('namespace multifarious.Icu.BatchTasks.Resources')
[void]$sb.AppendLine('{')
[void]$sb.AppendLine('    /// <summary>')
[void]$sb.AppendLine('    /// Typed access to the settings page and report strings.')
[void]$sb.AppendLine('    ///')
[void]$sb.AppendLine('    /// Generated from UIStrings.resx - do not edit by hand. Regenerate with')
[void]$sb.AppendLine('    /// tools/generate-uistrings.ps1 after adding or renaming an entry, and a test asserts the')
[void]$sb.AppendLine('    /// two stay in step, so a string added to the resx and forgotten here fails the build')
[void]$sb.AppendLine('    /// rather than showing up blank in the dialog.')
[void]$sb.AppendLine('    ///')
[void]$sb.AppendLine('    /// Written out rather than produced by ResXFileCodeGenerator because that generator runs')
[void]$sb.AppendLine('    /// inside Visual Studio, not during a command-line build.')
[void]$sb.AppendLine('    /// </summary>')
[void]$sb.AppendLine('    public static class UIStrings')
[void]$sb.AppendLine('    {')
[void]$sb.AppendLine('        private static readonly ResourceManager Manager = new ResourceManager(')
[void]$sb.AppendLine('            "multifarious.Icu.BatchTasks.Resources.UIStrings", typeof(UIStrings).Assembly);')
[void]$sb.AppendLine()
[void]$sb.AppendLine('        /// <summary>The culture used for lookups. Null follows the thread''s UI culture, which is what Studio sets.</summary>')
[void]$sb.AppendLine('        public static CultureInfo Culture { get; set; }')
[void]$sb.AppendLine()
[void]$sb.AppendLine('        /// <summary>Look a string up by name. Used by the tests; prefer the properties below.</summary>')
[void]$sb.AppendLine('        public static string Get(string name)')
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            return Manager.GetString(name, Culture);')
[void]$sb.AppendLine('        }')

foreach ($entry in $entries) {
    $name = $entry.name
    $comment = ''
    if ($entry.comment) {
        $comment = ($entry.comment -replace "`r?`n", ' ') -replace '<', '&lt;' -replace '>', '&gt;'
        $comment = $comment -replace '&(?!lt;|gt;|amp;)', '&amp;'
    }
    [void]$sb.AppendLine()
    if ($comment) { [void]$sb.AppendLine("        /// <summary>$comment</summary>") }
    [void]$sb.AppendLine("        public static string $name { get { return Manager.GetString(`"$name`", Culture); } }")
}

[void]$sb.AppendLine('    }')
[void]$sb.AppendLine('}')

[System.IO.File]::WriteAllText($out, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Wrote $out from $($entries.Count) entries."
