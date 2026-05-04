$connStr = "Server=SIMRAN\SQLEXPRESS;Database=FoodDeliveryDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
$conn = New-Object -TypeName System.Data.SqlClient.SqlConnection -ArgumentList $connStr
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "UPDATE UserSignup SET Password = '1a3cff9097194301504d7a9c7d7519bdb217c70d46629555c6b789090d3f468d' WHERE Email = 'simranswarnkar123@gmail.com'"
$rows = $cmd.ExecuteNonQuery()
Write-Output "Updated $rows rows"
$conn.Close()
