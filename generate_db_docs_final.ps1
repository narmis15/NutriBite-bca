$connStr = "Server=SIMRAN\SQLEXPRESS;Database=FoodDeliveryDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
$conn = New-Object System.Data.SqlClient.SqlConnection $connStr
$conn.Open()
$cmd = $conn.CreateCommand()

$tables = @(
    "UserSignup", 
    "VendorSignup", 
    "Foods", 
    "OrderTable", 
    "Payments",
    "Admin",
    "OrderItems",
    "Carttable",
    "Subscriptions",
    "HealthSurveys"
)

$outputFile = "Database_Tables_Final.txt"
Remove-Item -Path $outputFile -ErrorAction SilentlyContinue

foreach ($table in $tables) {
    # Added table name without numbering
    Add-Content -Path $outputFile -Value "$table"
    Add-Content -Path $outputFile -Value "mysql> describe $table;"
    
    $cmd.CommandText = @"
    SELECT 
        c.COLUMN_NAME as Field, 
        c.DATA_TYPE + 
            CASE 
                WHEN c.CHARACTER_MAXIMUM_LENGTH = -1 THEN '(max)'
                WHEN c.CHARACTER_MAXIMUM_LENGTH IS NOT NULL THEN '(' + CAST(c.CHARACTER_MAXIMUM_LENGTH AS VARCHAR) + ')' 
                ELSE '' 
            END as Type, 
        CASE WHEN c.IS_NULLABLE = 'YES' THEN 'YES' ELSE 'NO' END as [Null],
        ISNULL((SELECT TOP 1 'PRI' FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc ON kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY' 
                  AND kcu.TABLE_NAME = c.TABLE_NAME 
                  AND kcu.COLUMN_NAME = c.COLUMN_NAME), '') as [Key],
        ISNULL(c.COLUMN_DEFAULT, 'NULL') as [Default],
        CASE WHEN COLUMNPROPERTY(object_id(c.TABLE_SCHEMA+'.'+c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') = 1 THEN 'auto_increment' ELSE '' END as Extra
    FROM INFORMATION_SCHEMA.COLUMNS c
    WHERE c.TABLE_NAME = '$table'
    ORDER BY c.ORDINAL_POSITION
"@
    $colReader = $cmd.ExecuteReader()
    $cols = @()
    while ($colReader.Read()) {
        $col = New-Object PSObject -Property @{
            Field = $colReader["Field"].ToString()
            Type = $colReader["Type"].ToString()
            Null = $colReader["Null"].ToString()
            Key = $colReader["Key"].ToString()
            Default = $colReader["Default"].ToString()
            Extra = $colReader["Extra"].ToString()
        }
        $cols += $col
    }
    $colReader.Close()
    
    $maxField = 5
    $maxType = 4
    $maxNull = 4
    $maxKey = 3
    $maxDefault = 7
    $maxExtra = 5
    
    foreach ($col in $cols) {
        if ($col.Field.Length -gt $maxField) { $maxField = $col.Field.Length }
        if ($col.Type.Length -gt $maxType) { $maxType = $col.Type.Length }
        if ($col.Null.Length -gt $maxNull) { $maxNull = $col.Null.Length }
        if ($col.Key.Length -gt $maxKey) { $maxKey = $col.Key.Length }
        if ($col.Default.Length -gt $maxDefault) { $maxDefault = $col.Default.Length }
        if ($col.Extra.Length -gt $maxExtra) { $maxExtra = $col.Extra.Length }
    }
    
    $line = "+-" + ("-" * $maxField) + "-+-" + ("-" * $maxType) + "-+-" + ("-" * $maxNull) + "-+-" + ("-" * $maxKey) + "-+-" + ("-" * $maxDefault) + "-+-" + ("-" * $maxExtra) + "-+"
    Add-Content -Path $outputFile -Value $line
    
    $header = "| " + "Field".PadRight($maxField) + " | " + "Type".PadRight($maxType) + " | " + "Null".PadRight($maxNull) + " | " + "Key".PadRight($maxKey) + " | " + "Default".PadRight($maxDefault) + " | " + "Extra".PadRight($maxExtra) + " |"
    Add-Content -Path $outputFile -Value $header
    Add-Content -Path $outputFile -Value $line
    
    foreach ($col in $cols) {
        $row = "| " + $col.Field.PadRight($maxField) + " | " + $col.Type.PadRight($maxType) + " | " + $col.Null.PadRight($maxNull) + " | " + $col.Key.PadRight($maxKey) + " | " + $col.Default.PadRight($maxDefault) + " | " + $col.Extra.PadRight($maxExtra) + " |"
        Add-Content -Path $outputFile -Value $row
    }
    Add-Content -Path $outputFile -Value $line
    Add-Content -Path $outputFile -Value ("{0} rows in set" -f $cols.Count)
    Add-Content -Path $outputFile -Value ""
}

$conn.Close()
Write-Output "Done"
