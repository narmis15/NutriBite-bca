$ErrorActionPreference = "Stop"

$connStr = "Server=SIMRAN\SQLEXPRESS;Database=FoodDeliveryDB;Trusted_Connection=True;TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

$tx = $conn.BeginTransaction()
try {
    $cmd = $conn.CreateCommand()
    $cmd.Transaction = $tx
    $cmd.CommandText = @"
        -- 1. Identify Vendors to delete (All except top 10 by VendorId ASC)
        SELECT VendorId INTO #VendorsToDelete
        FROM (
            SELECT VendorId, ROW_NUMBER() OVER (ORDER BY VendorId ASC) as rn
            FROM VendorSignup
        ) sq
        WHERE rn > 10;

        DECLARE @Count INT;
        SELECT @Count = COUNT(*) FROM #VendorsToDelete;
        PRINT 'Vendors to delete: ' + CAST(@Count AS VARCHAR);

        IF @Count > 0
        BEGIN
            -- Subscriptions depend on Foods and Vendors
            DELETE FROM Subscriptions WHERE VendorId IN (SELECT VendorId FROM #VendorsToDelete);
            DELETE FROM Subscriptions WHERE FoodId IN (SELECT Id FROM Foods WHERE VendorId IN (SELECT VendorId FROM #VendorsToDelete));

            -- Delete OrderItems belonging to Orders placed with these vendors
            DELETE FROM OrderItems 
            WHERE OrderId IN (SELECT OrderId FROM OrderTable WHERE VendorId IN (SELECT VendorId FROM #VendorsToDelete));
            
            -- Delete OrderItems referencing Foods from these vendors (just in case they crossed over)
            DELETE FROM OrderItems
            WHERE FoodId IN (SELECT Id FROM Foods WHERE VendorId IN (SELECT VendorId FROM #VendorsToDelete));

            -- Delete Orders
            DELETE FROM OrderTable WHERE VendorId IN (SELECT VendorId FROM #VendorsToDelete);

            -- Delete VendorPayouts
            DELETE FROM VendorPayouts WHERE VendorId IN (SELECT VendorId FROM #VendorsToDelete);

            -- Delete Foods
            DELETE FROM Foods WHERE VendorId IN (SELECT VendorId FROM #VendorsToDelete);
            
            -- Delete Vendor from User map/relations if any?
            
            -- Finally delete the Vendor
            DELETE FROM VendorSignup WHERE VendorId IN (SELECT VendorId FROM #VendorsToDelete);
        END

        DROP TABLE #VendorsToDelete;
"@
    $cmd.ExecuteNonQuery()
    $tx.Commit()
    Write-Output "Successfully deleted excess vendors."
}
catch {
    $tx.Rollback()
    Write-Error "Failed to delete vendors: $_"
}
finally {
    $conn.Close()
}
