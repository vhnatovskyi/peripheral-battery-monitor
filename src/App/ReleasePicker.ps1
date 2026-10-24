function Show-PbmReleasePicker($Releases,[version]$Current,[string]$Language){
    function R([string]$text){Get-PbmText $text $Language}
    $dialog=New-Object Windows.Forms.Form
    $dialog.Text=R 'Releases and rollback';$dialog.ClientSize=New-Object Drawing.Size(530,390)
    $dialog.StartPosition='CenterScreen';$dialog.FormBorderStyle='FixedDialog';$dialog.MaximizeBox=$false;$dialog.MinimizeBox=$false
    $dialog.BackColor=[Drawing.ColorTranslator]::FromHtml('#10151F');$dialog.ForeColor=[Drawing.Color]::White
    $dialog.Font=New-Object Drawing.Font('Segoe UI',10)
    $currentLabel=New-Object Windows.Forms.Label;$currentLabel.Text=(R 'Installed version: {0}') -f $Current;$currentLabel.SetBounds(24,20,480,28)
    $label=New-Object Windows.Forms.Label;$label.Text=R 'Choose a release';$label.SetBounds(24,58,480,25)
    $choices=New-Object Windows.Forms.ComboBox;$choices.DropDownStyle='DropDownList';$choices.SetBounds(24,88,482,30)
    foreach($release in $Releases){
        $number=ConvertTo-PbmVersion $release.tag_name
        $state=if($number -eq $Current){R 'Current'}elseif($number -lt $Current){R 'Older version'}else{R 'Newer version'}
        $choices.Items.Add("$number - $state") | Out-Null
    }
    $notes=New-Object Windows.Forms.TextBox;$notes.SetBounds(24,135,482,184);$notes.Multiline=$true;$notes.ReadOnly=$true;$notes.ScrollBars='Vertical'
    $notes.BackColor=[Drawing.ColorTranslator]::FromHtml('#1B2432');$notes.ForeColor=[Drawing.Color]::White;$notes.BorderStyle='FixedSingle'
    $choices.Add_SelectedIndexChanged({$release=$Releases[$choices.SelectedIndex];$notes.Text=if($release.body){$release.body -replace '\r?\n',[Environment]::NewLine}else{R 'No release notes.'}})
    $install=New-Object Windows.Forms.Button;$install.Text=R 'Install selected release';$install.SetBounds(232,337,274,34);$install.DialogResult='OK'
    $cancel=New-Object Windows.Forms.Button;$cancel.Text=R 'Cancel';$cancel.SetBounds(24,337,182,34);$cancel.DialogResult='Cancel'
    $dialog.Controls.AddRange(@($currentLabel,$label,$choices,$notes,$install,$cancel));$dialog.AcceptButton=$install;$dialog.CancelButton=$cancel
    $choices.SelectedIndex=0
    try{if($dialog.ShowDialog() -eq 'OK'){return $Releases[$choices.SelectedIndex]}}finally{$dialog.Dispose()}
    return $null
}
