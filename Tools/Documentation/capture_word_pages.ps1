param(
    [Parameter(Mandatory = $true)]
    [string]$PartsDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class CraftLiveWordCapture {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
'@

$partNames = @(
    'qa_01_main_accounts.docx',
    'qa_02_main_system.docx',
    'qa_03_main_features.docx',
    'qa_04_main_operations.docx',
    'qa_05_scenes.docx',
    'qa_06_inspector.docx',
    'qa_07_fields.docx',
    'qa_08_manifest_Assets.docx',
    'qa_09_manifest_Packages_ProjectSettings.docx',
    'qa_10_checklist.docx'
)

$partsPath = (Resolve-Path -LiteralPath $PartsDirectory).Path
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputPath = (Resolve-Path -LiteralPath $OutputDirectory).Path

$word = $null
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $true
    $word.DisplayAlerts = 0

    foreach ($partName in $partNames) {
        $doc = $null
        try {
            $inputPath = Join-Path $partsPath $partName
            if (-not (Test-Path -LiteralPath $inputPath)) {
                throw "Missing QA part: $inputPath"
            }

            $doc = $word.Documents.Open($inputPath, $false, $true)
            $doc.Activate()
            $hwnd = [IntPtr]$word.ActiveWindow.Hwnd
            [void][CraftLiveWordCapture]::ShowWindow($hwnd, 3)
            [void][CraftLiveWordCapture]::SetForegroundWindow($hwnd)

            $word.ActiveWindow.View.Type = 3
            # At 60%, one full Letter page fits below the ribbon on this display.
            $word.ActiveWindow.View.Zoom.Percentage = 60
            $pageCount = $doc.ComputeStatistics(2)
            Start-Sleep -Milliseconds 500

            $stem = [IO.Path]::GetFileNameWithoutExtension($partName)
            for ($page = 1; $page -le $pageCount; $page++) {
                $pageRange = $doc.GoTo(1, 1, $page)
                $word.ActiveWindow.ScrollIntoView($pageRange, $true)
                if ($page -gt 1) {
                    # ScrollIntoView leaves a slice of the previous page visible;
                    # ten small-scroll units align the target page with the viewport.
                    $word.ActiveWindow.SmallScroll(10, 0, 0, 0)
                }
                [void][CraftLiveWordCapture]::SetForegroundWindow($hwnd)
                Start-Sleep -Milliseconds 240

                $rect = New-Object CraftLiveWordCapture+RECT
                [void][CraftLiveWordCapture]::GetWindowRect($hwnd, [ref]$rect)
                $width = $rect.Right - $rect.Left
                $height = $rect.Bottom - $rect.Top
                if ($width -le 0 -or $height -le 0) {
                    throw "Invalid Word window rectangle for $partName page $page"
                }

                $bitmap = New-Object System.Drawing.Bitmap($width, $height)
                $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
                try {
                    $size = New-Object System.Drawing.Size($width, $height)
                    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $size)
                    $pngPath = Join-Path $outputPath ("{0}_page-{1:D3}.png" -f $stem, $page)
                    $bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
                }
                finally {
                    $graphics.Dispose()
                    $bitmap.Dispose()
                }
            }

            Write-Output ("CAPTURED {0} pages={1}" -f $partName, $pageCount)
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
