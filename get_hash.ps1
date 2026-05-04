$text = "Simran@123"
$bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
$hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
$hex = [System.BitConverter]::ToString($hash).Replace("-","").ToLower()
Write-Output $hex
