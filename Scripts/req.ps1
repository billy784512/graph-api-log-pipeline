
$url = ""

$method = "GET"
$headers = @{
    "Content-Type" = "application/json"
}
$body = @{
    "input" = "test"
} | ConvertTo-Json


$response = Invoke-WebRequest -Uri $url -Method $method -Headers $headers
$response




