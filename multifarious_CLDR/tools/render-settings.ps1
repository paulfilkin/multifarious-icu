# Renders the settings dialog and the Expand ICU wizard page offscreen to PNG, so the XAML is
# proven to load against Studio's style assemblies without opening Studio. The tests never
# load XAML, and a StaticResource that is missing at run time is a blank page in Studio.
#
# Run in Windows PowerShell, not pwsh: the plugin is net48.
#   powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tools\render-settings.ps1 [-Out <folder>]
# Needs a Debug build of the test project (the plugin and its satellites are copied beside it).
# Studio's control styles look brushes up in the application's resources, so the theme and the
# common resources are merged first; without them Show fails on an unset BorderBrush.
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

$null = [System.Reflection.Assembly]::LoadFrom("$studio\Sdl.Desktop.Platform.Styles.dll")
$plugin = [System.Reflection.Assembly]::LoadFrom("$bin\IcuSupport.dll")
if (-not [System.Windows.Application]::Current) { $null = New-Object System.Windows.Application }
# Studio's theme brushes, which the control styles look up at run time.
foreach ($name in 'theming/default', 'commonresources') {
    $dict = [System.Windows.Application]::LoadComponent([Uri]::new("/Sdl.Desktop.Platform.Styles;component/$name.xaml", [UriKind]::Relative))
    [System.Windows.Application]::Current.Resources.MergedDictionaries.Add($dict)
}

$settingsUtil = [System.Reflection.Assembly]::LoadFrom("$studio\Sdl.Core.Settings.dll").GetType('Sdl.Core.Settings.SettingsUtil')
$bundle = $settingsUtil.GetMethod('CreateSettingsBundle', [type[]]@([Sdl.Core.Settings.ISettingsBundle])).Invoke($null, @($null))

$windowType = $plugin.GetType('multifarious.Icu.BatchTasks.Settings.Views.IcuSettingsWindow')
try {
    $window = [Activator]::CreateInstance($windowType, @('Project 63', $bundle))
} catch {
    $e = $_.Exception
    while ($e) { "EXC: $($e.GetType().Name): $($e.Message)"; $e = $e.InnerException }
    exit 1
}

# Import the template so the tables have something to show.
$vmType = $plugin.GetType('multifarious.Icu.BatchTasks.Settings.ViewModels.GrammarDictionaryViewModel')
$template = $vmType.GetMethod('TemplateText').Invoke($null, $null)
$grammarView = $window.FindName('GrammarView')
$null = $grammarView.DataContext.TryImport('grammar-dictionary.json', $template)

$window.Width = 860; $window.Height = 760
$window.WindowStartupLocation = 'Manual'; $window.Left = -5000; $window.Top = -5000
$window.ShowActivated = $false
$window.Show()

$tabs = $window.Content.Children[0]
function Snap($element, $file) {
    $element.Dispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::Render)
    $element.UpdateLayout()
    $bmp = New-Object System.Windows.Media.Imaging.RenderTargetBitmap([int]$element.ActualWidth, [int]$element.ActualHeight, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bmp.Render($element)
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
    $fs = [System.IO.File]::Create((Join-Path $out $file))
    $enc.Save($fs); $fs.Close()
    "$file $([int]$element.ActualWidth)x$([int]$element.ActualHeight)"
}
function SnapTab($index, $file) {
    $tabs.SelectedIndex = $index
    Snap $window.Content $file
}
SnapTab 0 'dialog-expand.png'
SnapTab 1 'dialog-grammar.png'
# Scroll the grammar tab down for the tables.
$scroller = $grammarView.Content
$scroller.ScrollToVerticalOffset(520)
SnapTab 1 'dialog-grammar-tables.png'
$scroller.ScrollToVerticalOffset(2200)
SnapTab 1 'dialog-grammar-arguments.png'
# A refused edit: the German row without other, the message under the table, the cell red.
$grammar = $grammarView.DataContext
$german = $grammar.Languages | Where-Object { $_.Kind -eq 'gender' -and $_.LocaleKey -eq 'de' }
$german.KeysText = 'MASCULINE, FEMININE'
$scroller.ScrollToVerticalOffset(1800)
SnapTab 1 'dialog-grammar-error.png'
$german.Reset()
SnapTab 2 'dialog-restore.png'

# The wizard page too: the two inner tabs, at the batch task wizard's narrower width.
$pageType = $plugin.GetType('multifarious.Icu.BatchTasks.Settings.Views.IcuExpandSettingsView')
$page = [Activator]::CreateInstance($pageType)
$evmType = $plugin.GetType('multifarious.Icu.BatchTasks.Settings.ViewModels.IcuExpandSettingsViewModel')
$getGroup = $bundle.GetType().GetMethods() | Where-Object { $_.Name -eq 'GetSettingsGroup' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 0 } | Select-Object -First 1
$expandSettings = $getGroup.MakeGenericMethod($plugin.GetType('multifarious.Icu.BatchTasks.Settings.IcuExpandSettings')).Invoke($bundle, $null)
$page.DataContext = [Activator]::CreateInstance($evmType, @($expandSettings))
$hostWindow = New-Object System.Windows.Window
$hostWindow.Width = 620; $hostWindow.Height = 700; $hostWindow.Left = -5000; $hostWindow.Top = -5000; $hostWindow.ShowActivated = $false
$hostWindow.Content = $page
$hostWindow.Show()
Snap $page 'wizard-options.png'
$page.Content.SelectedIndex = 1
Snap $page 'wizard-grammar.png'
$hostWindow.Close(); $window.Close()
