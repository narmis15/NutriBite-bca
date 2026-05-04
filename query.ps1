$connStr = "Server=SIMRAN\SQLEXPRESS;Database=FoodDeliveryDB;Trusted_Connection=True;TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT f.Id, f.Name, r.Ingredients FROM Foods f LEFT JOIN Recipes r ON f.Id = r.FoodId"
$reader = $cmd.ExecuteReader()
while($reader.Read()) {
    $ing = if ($reader["Ingredients"] -is [System.DBNull]) { "NULL" } else { $reader["Ingredients"] }
    Write-Output "$($reader["Id"]) | $($reader["Name"]) | $ing"
}
$conn.Close()
