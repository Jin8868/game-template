$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$excelPath = Join-Path $repositoryRoot 'DesignData\Luban\Datas\Enums.xlsx'
$outputPath = Join-Path $repositoryRoot 'DesignData\Luban\Defines\GeneratedEnums.xml'
$archive = [System.IO.Compression.ZipFile]::OpenRead($excelPath)
try
{
    $entry = $archive.GetEntry("xl/worksheets/sheet1.xml")
    $reader = [System.IO.StreamReader]::new($entry.Open())
    try { [xml]$sheet = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $rows = $sheet.SelectNodes("//*[local-name()='sheetData']/*[local-name()='row']")
    $headers = @{}
    foreach ($cell in $rows[0].SelectNodes("./*[local-name()='c']"))
    {
        $column = [regex]::Match($cell.r, '^[A-Z]+').Value
        $headers[$cell.is.InnerText.ToLowerInvariant()] = $column
    }
    $enums = @{}
    foreach ($row in $rows | Select-Object -Skip 1)
    {
        $values = @{}
        foreach ($cell in $row.SelectNodes("./*[local-name()='c']"))
        {
            $column = [regex]::Match($cell.r, '^[A-Z]+').Value
            $values[$column] = if ($cell.t -eq 'inlineStr') { $cell.is.InnerText } else { $cell.v }
        }
        $enumName = $values[$headers['enum_name']]
        $memberName = $values[$headers['member_name']]
        $value = $values[$headers['value']]
        if ([string]::IsNullOrWhiteSpace($enumName)) { continue }
        if (-not $enums.ContainsKey($enumName)) { $enums[$enumName] = @() }
        $enums[$enumName] += [PSCustomObject]@{ Name = $memberName; Value = $value }
    }
    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $settings.Indent = $true
    $writer = [System.Xml.XmlWriter]::Create($outputPath, $settings)
    try
    {
        $writer.WriteStartDocument()
        $writer.WriteStartElement('module')
        $writer.WriteAttributeString('name', '')
        foreach ($enumName in $enums.Keys)
        {
            $writer.WriteStartElement('enum')
            $writer.WriteAttributeString('name', $enumName)
            foreach ($member in $enums[$enumName])
            {
                $writer.WriteStartElement('var')
                $writer.WriteAttributeString('name', $member.Name)
                $writer.WriteAttributeString('value', $member.Value)
                $writer.WriteEndElement()
            }
            $writer.WriteEndElement()
        }
        $writer.WriteEndElement()
        $writer.WriteEndDocument()
    }
    finally { $writer.Dispose() }
}
finally { $archive.Dispose() }
