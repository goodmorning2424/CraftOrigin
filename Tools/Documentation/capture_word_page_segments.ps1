param(
    [Parameter(Mandatory = $true)] [string]$PartsDirectory,
    [Parameter(Mandatory = $true)] [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class CraftLiveWordSegmentCapture {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
'@

$partNames = @(
    'qa_01_main_accounts.docx', 'qa_02_main_system.docx', 'qa_03_main_features.docx',
    'qa_04_main_operations.docx', 'qa_05_scenes.docx', 'qa_06_inspector.docx',
    'qa_07_fields.docx', 'qa_08_manifest_Assets.docx',
    'qa_09_manifest_Packages_ProjectSettings.docx', 'qa_10_checklist.docx'
)

$partsPath = (Resolve-Path -LiteralPath $PartsDirectory).Path
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputPath = (Resolve-Path -LiteralPath $OutputDirectory).Path

function Save-WordWindowCapture {
    param([IntPtr]$Hwnd, [string]$PngPath)
    [void][CraftLiveWordSegmentCapture]::SetForegroundWindow($Hwnd)
    Start-Sleep -Milliseconds 180
    $rect = New-Object CraftLiveWordSegmentCapture+RECT
    [void][CraftLiveWordSegmentCapture]::GetWindowRect($Hwnd, [ref]$rect)
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $size = New-Object System.Drawing.Size($width, $height)
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $size)
        $bitmap.Save($PngPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$word = $null
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $true
    $word.DisplayAlerts = 0
    foreach ($partName in $partNames) {
        $doc = $null
        try {
            $inputPath = Join-Path $partsPath $partName
            $doc = $word.Documents.Open($inputPath, $false, $true)
            $doc.Activate()
            $hwnd = [IntPtr]$word.ActiveWindow.Hwnd
            [void][CraftLiveWordSegmentCapture]::ShowWindow($hwnd, 3)
            $word.ActiveWindow.View.Type = 3
            $word.ActiveWindow.View.Zoom.PageFit = 1
            $pageCount = $doc.ComputeStatistics(2)
            $stem = [IO.Path]::GetFileNameWithoutExtension($partName)

            for ($page = 1; $page -le $pageCount; $page++) {
                $startRange = $doc.GoTo(1, 1, $page)
                if ($page -lt $pageCount) {
                    $nextRange = $doc.GoTo(1, 1, $page + 1)
                    $endPosition = [Math]::Max($startRange.Start, $nextRange.Start - 1)
                }
                else {
                    $endPosition = [Math]::Max($startRange.Start, $doc.Content.End - 1)
                }

                $word.ActiveWindow.ScrollIntoView($startRange, $true)
                $topPath = Join-Path $outputPath ("{0}_page-{1:D3}_top.png" -f $stem, $page)
                Save-WordWindowCapture -Hwnd $hwnd -PngPath $topPath

                $endRange = $doc.Range([Math]::Max($startRange.Start, $endPosition - 1), $endPosition)
                $word.ActiveWindow.ScrollIntoView($endRange, $false)
                $bottomPath = Join-Path $outputPath ("{0}_page-{1:D3}_bottom.png" -f $stem, $page)
                Save-WordWindowCapture -Hwnd $hwnd -PngPath $bottomPath
            }
            Write-Output ("CAPTURED_SEGMENTS {0} pages={1}" -f $partName, $pageCount)
        }
        finally {
            if ($doc) {
                $doc.Close(0)
                [void][Runtime.InteropServices.Marshal]::ReleaseComObject($doc)
            }
        }
    }
}
finally {
    if ($word) {
        $word.Quit()
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($word)
    }
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}
