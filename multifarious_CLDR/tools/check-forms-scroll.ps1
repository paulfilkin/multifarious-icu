# Proves the ICU Forms window keeps the active row in view, offscreen and without Studio: the
# view is hosted in a small window over an expanded Arabic plural (six rows, three visible),
# the active segment is moved, and the scroller's offset is read back after each move. PNGs
# of each state go to the renders folder. The tests never load XAML, so this is the only
# check short of Studio.
#
# Run in Windows PowerShell, not pwsh: the plugin is net48.
#   powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tools\check-forms-scroll.ps1 [-Out <folder>]
# Needs a Debug build of the test project (the plugin and its satellites are copied beside it).
param([string]$Out = (Join-Path $PSScriptRoot '..\..\Documentation\Testing\renders'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml

$studio = 'C:\Program Files\Trados\Trados Studio\Studio19'
$bin = Join-Path $PSScriptRoot '..\multifarious.Icu.Tests\bin\Debug\net48'
$out = $Out
if (-not (Test-Path $out)) { $null = New-Item -ItemType Directory -Path $out }

$handler = [ResolveEventHandler]{
    param($sender, $resolve)
    $name = (New-Object System.Reflection.AssemblyName($resolve.Name)).Name
    foreach ($dir in @($bin, $studio)) {
        $path = Join-Path $dir "$name.dll"
        if (Test-Path $path) { return [System.Reflection.Assembly]::LoadFrom($path) }
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($handler)

$plugin = [System.Reflection.Assembly]::LoadFrom("$bin\IcuSupport.dll")
$tests = [System.Reflection.Assembly]::LoadFrom("$bin\multifarious.Icu.Tests.dll")
$expansion = [System.Reflection.Assembly]::LoadFrom("$bin\multifarious.Icu.Expansion.dll")
if (-not [System.Windows.Application]::Current) { $null = New-Object System.Windows.Application }

# An expanded unit through the test project's builders: Arabic has six plural forms.
$units = $tests.GetType('multifarious.Icu.Tests.BatchTasks.ParagraphUnits')
$unit = $units.GetMethod('Json').Invoke($null, @('You have {count, plural, one {# message} other {# messages}}.', "['inbox.count']"))
$options = $expansion.GetType('multifarious.Icu.Expansion.ExpansionOptions').GetProperty('Default').GetValue($null)
$processor = [Activator]::CreateInstance($plugin.GetType('multifarious.Icu.BatchTasks.Services.IcuExpandProcessor'), @('en-GB', 'ar-SA', $options, '1.0.0', $null))
$processor.ItemFactory = $units.GetProperty('ItemFactory').GetValue($null)
$null = $processor.ProcessParagraphUnit($unit)
# The paragraph is itself a collection, which @() would unroll into the argument list.
$sourceArgument = New-Object object[] 1
$sourceArgument[0] = $unit.Source
$segments = $units.GetMethod('SegmentsOf').Invoke($null, $sourceArgument)
"unit expanded to $($segments.Count) segments"

$viewModel = [Activator]::CreateInstance($plugin.GetType('multifarious.Icu.BatchTasks.Editor.ViewModels.IcuFormsViewModel'), @($null, $null))
$view = [Activator]::CreateInstance($plugin.GetType('multifarious.Icu.BatchTasks.Editor.Views.IcuFormsView'))
$view.DataContext = $viewModel

$window = New-Object System.Windows.Window
$window.Content = $view
# Tall enough for the header, the column titles and the projection box with about three
# of the six rows between them; the viewport is printed with every step.
$window.Width = 700; $window.Height = 290
$window.WindowStartupLocation = 'Manual'; $window.Left = -5000; $window.Top = -5000
$window.ShowActivated = $false
$window.Show()

$scroller = $view.FindName('RowsScroller')
function Pump() {
    # Lets the Loaded-priority scroll run, then layout settle.
    $view.Dispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::ContextIdle)
    $view.UpdateLayout()
}
function Snap($file) {
    $view.Dispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::Render)
    $bmp = New-Object System.Windows.Media.Imaging.RenderTargetBitmap([int]$view.ActualWidth, [int]$view.ActualHeight, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bmp.Render($view)
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
    $fs = [System.IO.File]::Create((Join-Path $out $file))
    $enc.Save($fs); $fs.Close()
}
function ShowSegment($index, $label, $file) {
    $viewModel.Show($unit, $segments[$index].Properties.Id.Id, 'ar-SA', 'Arabic (Saudi Arabia)', $null)
    Pump
    Snap $file
    Write-Host "$label`: rows=$($viewModel.Rows.Count) viewport=$([int]$scroller.ViewportHeight) extent=$([int]$scroller.ExtentHeight) offset=$([int]$scroller.VerticalOffset)"
    return [double]$scroller.VerticalOffset
}

$failures = 0
$first = ShowSegment 0 'first row active' 'forms-scroll-first.png'
if ($first -ne 0) { "FAIL: first row active should leave the offset at 0"; $failures++ }

$last = ShowSegment 5 'last row active' 'forms-scroll-last.png'
if ($last -le 0) { "FAIL: last row active should scroll down"; $failures++ }
if ($scroller.ScrollableHeight -le 0) { "FAIL: the window is not small enough to need scrolling"; $failures++ }

$second = ShowSegment 1 'second row active' 'forms-scroll-second.png'
if ($second -ne 0) { "FAIL: a row above the viewport should scroll back to the top"; $failures++ }

# A row already in view is left alone: with the offset at 0 the second row is visible, so
# moving from the first row to the second must not move the scroller.
$null = ShowSegment 0 'first row again' 'forms-scroll-first-again.png'
$null = ShowSegment 1 'second row, already in view' 'forms-scroll-second-in-view.png'
if ($scroller.VerticalOffset -ne 0) { "FAIL: a row already in view must not scroll"; $failures++ }

# The same from the bottom: with the last row centred, the row above it is in view too.
$null = ShowSegment 5 'last row again' 'forms-scroll-last-again.png'
$before = $scroller.VerticalOffset
$null = ShowSegment 4 'fifth row, already in view' 'forms-scroll-fifth-in-view.png'
if ($scroller.VerticalOffset -ne $before) { "FAIL: the fifth row was in view under the last and must not scroll"; $failures++ }

$window.Close()
if ($failures -eq 0) { "PASS: the active row is kept in view" } else { "FAILED: $failures"; exit 1 }
