$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Import-Module (Join-Path $PSScriptRoot '../src/App/Updates.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../src/App/Localization.psm1') -Force
. (Join-Path $PSScriptRoot '../src/App/ReleasePicker.ps1')
function Assert($condition,$message){if(-not $condition){throw $message}}
$historyAlias=(Get-Alias r).Definition
$releases=@(
    [pscustomobject]@{tag_name='v0.4.1';body='Latest release notes'},
    [pscustomobject]@{tag_name='v0.2.0';body=''}
)
foreach($language in @('uk','en')){
    $dialog=Show-PbmReleasePicker $releases ([version]'0.4.0') $language -ForTest
    try{
        Assert ($dialog.Text -eq (Get-PbmText 'Releases and rollback' $language)) 'Localized release picker title failed'
        $combo=@($dialog.Controls | Where-Object {$_ -is [Windows.Forms.ComboBox]})[0]
        $notes=@($dialog.Controls | Where-Object {$_ -is [Windows.Forms.TextBox]})[0]
        Assert ($combo.Items.Count -eq 2 -and $combo.SelectedIndex -eq 0) 'Release choices missing'
        Assert ($notes.Text -eq 'Latest release notes') 'Initial release notes missing'
        $combo.SelectedIndex=1
        Assert ($notes.Text -eq (Get-PbmText 'No release notes.' $language)) 'Rollback selection event failed'
        Assert ($combo.Items[1] -like ('*'+(Get-PbmText 'Older version' $language))) 'Rollback target not labeled'
    }finally{$dialog.Dispose()}
}
Assert ((Get-Alias r).Definition -eq $historyAlias) 'History alias was modified'
'Release picker UI and alias regression tests passed'
