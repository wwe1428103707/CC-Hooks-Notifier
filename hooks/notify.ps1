#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Claude Code Hooks Notifier -- native Windows toast notifications.
.DESCRIPTION
    Reads hook events from stdin and displays Windows toast notifications.
    - Notification (idle_prompt)   -> toast only
    - PermissionRequest            -> toast + interactive WinForms dialog
#>

param()

$ErrorActionPreference = 'Stop'

# -- Minimize console window ------------------------------------------------
Add-Type -Name N -Namespace W -MemberDefinition @'
    [DllImport("kernel32.dll")] public static extern System.IntPtr GetConsoleWindow();
    [DllImport("user32.dll")]   public static extern bool ShowWindow(System.IntPtr h, int n);
'@
$h = [W.N]::GetConsoleWindow(); if ($h -ne [IntPtr]::Zero) { $null = [W.N]::ShowWindow($h, 6) }

# -- Native Windows toast (3 attempts) --------------------------------------
function Show-WindowsToast {
    param([string]$Title, [string]$Body, [string]$AppId = 'ClaudeCode.Hooks')

    # Attempt 1: WinRT via XML (avoid enum issues in PS 5.1)
    $ok = $false
    try {
        Add-Type -AssemblyName System.Runtime.WindowsRuntime -ErrorAction Stop
        $null = [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]
        $null = [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom, ContentType = WindowsRuntime]

        $xml = @"
<toast><visual><binding template='ToastText02'>
  <text id='1'>$([Security.SecurityElement]::Escape($Title))</text>
  <text id='2'>$([Security.SecurityElement]::Escape($Body))</text>
</binding></visual></toast>
"@
        $doc = New-Object Windows.Data.Xml.Dom.XmlDocument
        $doc.LoadXml($xml)
        $toast = [Windows.UI.Notifications.ToastNotification]::new($doc)
        [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($AppId).Show($toast)
        $ok = $true
    } catch { Write-Warning "WinRT toast failed: $_" }

    if ($ok) { return }

    # Attempt 2: Windows.Forms balloon
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        $icon = New-Object System.Windows.Forms.NotifyIcon
        $icon.Icon = [System.Drawing.SystemIcons]::Information
        $icon.BalloonTipTitle = $Title
        $icon.BalloonTipText  = $Body
        $icon.Visible = $true
        $icon.ShowBalloonTip(5000)
        Start-Sleep -Milliseconds 600
        $icon.Visible = $false
        $icon.Dispose()
        $ok = $true
    } catch { Write-Warning "WinForms balloon failed: $_" }

    if ($ok) { return }

    # Attempt 3: msg.exe (last resort)
    try {
        $null = Start-Process -FilePath 'msg.exe' -ArgumentList '*', "/TIME:5", "$Title -- $Body" -NoNewWindow -Wait
    } catch { Write-Warning "msg.exe also failed: $_" }
}

# -- Permission Request: WinForms dialog ------------------------------------
function Show-PermissionDialog {
    param($Data)

    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    [System.Windows.Forms.Application]::EnableVisualStyles()

    $toolName = if ($Data.tool_name) { $Data.tool_name } else { 'Unknown' }

    # Format detail lines
    $lines = @()
    if ($Data.tool_input) {
        foreach ($prop in $Data.tool_input.PSObject.Properties) {
            if ($prop.Name -eq 'description') { continue }
            $v = if ($prop.Value -is [string]) { $prop.Value } else { ($prop.Value | ConvertTo-Json -Compress -Depth 3) }
            $lines += "$($prop.Name): $v"
        }
    }
    $detailText = if ($lines.Count -gt 0) { $lines -join "`r`n" } else { '(no details)' }

    # Build form
    $form = New-Object System.Windows.Forms.Form
    $form.Text            = 'Claude Code - Permission Request'
    $form.Size            = New-Object System.Drawing.Size(520, 330)
    $form.StartPosition   = 'CenterScreen'
    $form.TopMost         = $true
    $form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
    $form.MaximizeBox     = $false
    $form.MinimizeBox     = $false
    $form.BackColor       = [System.Drawing.Color]::FromArgb(248, 249, 250)
    $form.Font            = New-Object System.Drawing.Font('Microsoft YaHei UI', 9)

    # Header
    $hdr = New-Object System.Windows.Forms.Label
    $hdr.Text   = 'Claude needs authorization'
    $hdr.Font   = New-Object System.Drawing.Font('Microsoft YaHei UI', 12, [System.Drawing.FontStyle]::Bold)
    $hdr.ForeColor = [System.Drawing.Color]::FromArgb(33, 37, 41)
    $hdr.Location = New-Object System.Drawing.Point(20, 16)
    $hdr.Size     = New-Object System.Drawing.Size(470, 26)
    $form.Controls.Add($hdr)

    # Tool name
    $tl = New-Object System.Windows.Forms.Label
    $tl.Text   = "Tool:  $toolName"
    $tl.Font   = New-Object System.Drawing.Font('Consolas', 10, [System.Drawing.FontStyle]::Bold)
    $tl.ForeColor = [System.Drawing.Color]::FromArgb(67, 97, 238)
    $tl.Location = New-Object System.Drawing.Point(20, 50)
    $tl.Size     = New-Object System.Drawing.Size(470, 18)
    $form.Controls.Add($tl)

    # Detail box
    $tb = New-Object System.Windows.Forms.TextBox
    $tb.Multiline  = $true; $tb.ReadOnly = $true; $tb.ScrollBars = 'Vertical'
    $tb.Font       = New-Object System.Drawing.Font('Consolas', 9)
    $tb.Location   = New-Object System.Drawing.Point(20, 78)
    $tb.Size       = New-Object System.Drawing.Size(470, 140)
    $tb.BackColor  = [System.Drawing.Color]::White
    $tb.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
    $tb.Text       = $detailText
    $form.Controls.Add($tb)

    # Button bar
    $bar = New-Object System.Windows.Forms.Panel
    $bar.Location = New-Object System.Drawing.Point(0, 236)
    $bar.Size     = New-Object System.Drawing.Size(520, 50)
    $bar.BackColor = [System.Drawing.Color]::FromArgb(241, 243, 245)
    $bar.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
    $form.Controls.Add($bar)

    # Deny
    $bD = New-Object System.Windows.Forms.Button
    $bD.Text = 'Deny'
    $bD.Location = New-Object System.Drawing.Point(410, 12)
    $bD.Size     = New-Object System.Drawing.Size(85, 28)
    $bD.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $bD.BackColor = [System.Drawing.Color]::White
    $bD.ForeColor = [System.Drawing.Color]::FromArgb(220, 53, 69)
    $bD.FlatAppearance.BorderColor = [System.Drawing.Color]::FromArgb(220, 53, 69)
    $bD.DialogResult = [System.Windows.Forms.DialogResult]::No
    $bD.TabIndex = 2
    $bar.Controls.Add($bD)

    # Allow
    $bA = New-Object System.Windows.Forms.Button
    $bA.Text = 'Allow'
    $bA.Location = New-Object System.Drawing.Point(310, 12)
    $bA.Size     = New-Object System.Drawing.Size(85, 28)
    $bA.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $bA.BackColor = [System.Drawing.Color]::FromArgb(67, 97, 238)
    $bA.ForeColor = [System.Drawing.Color]::White
    $bA.FlatAppearance.BorderColor = [System.Drawing.Color]::FromArgb(67, 97, 238)
    $bA.DialogResult = [System.Windows.Forms.DialogResult]::Yes
    $bA.TabIndex = 0
    $bar.Controls.Add($bA)

    # Toaster alert
    Show-WindowsToast -Title "Claude Code - Permission Required" `
        -Body "Tool: $toolName -- click Allow or Deny" -AppId 'ClaudeCode.Hooks'

    $form.AcceptButton = $bA
    $form.CancelButton = $bD
    $result = $form.ShowDialog()

    $decision = [Ordered]@{ behavior = 'deny' }
    if ($result -eq [System.Windows.Forms.DialogResult]::Yes) { $decision.behavior = 'allow' }

    Show-WindowsToast -Title "Claude Code - $(if($decision.behavior -eq 'allow'){'Allowed'}else{'Denied'})" `
        -Body "Tool: $toolName" -AppId 'ClaudeCode.Hooks'

    return $decision
}

# -- Main -------------------------------------------------------------------
function Main {
    try {
        $raw = [Console]::In.ReadToEnd()
        if ([string]::IsNullOrWhiteSpace($raw)) { return }
        $data = $raw | ConvertFrom-Json -ErrorAction Stop
    } catch {
        "--- stdin error: $($_.Exception.Message) ---" |
            Out-File "$PSScriptRoot\notify_error.log" -Append -Encoding utf8
        return
    }

    $eventName = $data.hook_event_name
    if (-not $eventName) { return }

    try {
        if ($eventName -eq 'PermissionRequest') {
            $decision = Show-PermissionDialog $data
            $output = @{
                hookSpecificOutput = @{
                    hookEventName = 'PermissionRequest'
                    decision      = $decision
                }
            }
            $output | ConvertTo-Json -Compress -Depth 5
        }
        elseif ($eventName -eq 'Notification') {
            $etype = if ($data.hook_event_type) { $data.hook_event_type } else { '' }
            $stype = if ($data.hook_event_subtype) { $data.hook_event_subtype } else { '' }
            $title = 'Claude Code'
            $body  = if ($etype -eq 'idle_prompt') {
                "Task complete -- ready for your input"
            } elseif ($etype -eq 'task_start') {
                "Task started: $stype"
            } elseif ($etype -eq 'task_end') {
                "Task completed: $stype"
            } else {
                "Hook event: $etype $stype"
            }
            Show-WindowsToast -Title $title -Body $body -AppId 'ClaudeCode.Hooks'
        }
        else {
            Show-WindowsToast -Title "Claude Code - $eventName" -Body "Hook event triggered" -AppId 'ClaudeCode.Hooks'
        }
    } catch {
        "--- hook error: $($_.Exception.Message) ---`n$($_.ScriptStackTrace)" |
            Out-File "$PSScriptRoot\notify_error.log" -Append -Encoding utf8
    }
}

Main
