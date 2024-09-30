# Define the Azure Function App name and resource group
$functionAppName = Read-Host -Prompt "Enter your Azure Function App name"
$resourceGroup = Read-Host -Prompt "Enter your Azure Resource Group name"


# ---------------------- Deployment ----------------------
Write-Host "Start Function App deployment..." -ForegroundColor Yellow

Set-Location -Path "..\App"

# Redudant file for deployment
if (Test-Path -Path ".\bin") {
    Remove-Item -Path ".\bin" -Recurse -Force
}
if (Test-Path -Path ".\obj") {
    Remove-Item -Path ".\obj" -Recurse -Force
}

Compress-Archive -Path . app.zip

az functionapp deployment source config-zip `
    -g $resourceGroup `
    -n $functionAppName `
    --src app.zip

func azure functionapp publish $functionAppName

Remove-Item -Path ".\app.zip" -Force
Write-Host "Function App deploy successfully!" -ForegroundColor Green
# ---------------------------------------------------------------------


# ---------------------- Env Variables Setting ----------------------
Write-Host "Start config Function App environment variable..." -ForegroundColor Yellow

$localSettingsFile = ".\local.settings.json"

# Keys to exclude from the settings
$excludedKeys = @("AzureWebJobsStorage", "FUNCTIONS_WORKER_RUNTIME")

# Check if the local.settings.json file exists
if (-Not (Test-Path $localSettingsFile)) {
    Write-Host "local.settings.json not found!" -ForegroundColor Red
    exit 1
}

# Parse
$localSettings = Get-Content $localSettingsFile | ConvertFrom-Json

$appSettings = @()

# Loop through the key-value pairs in the "Values" section, filtering out excluded keys
foreach ($setting in $localSettings.Values.PSObject.Properties) {
    $key = $setting.Name
    $value = $setting.Value

    if ($excludedKeys -contains $key) {
        Write-Host "Skipping excluded key: $key"
        continue
    }

    Write-Host "Add $key : $value to temp array"
    $appSettings += "$key=$value"
}

Write-Host "Setting all environment variables in temp array to $functionAppName"
az functionapp config appsettings set `
    --name $functionAppName `
    --resource-group $resourceGroup `
    --settings $appSettings `
    --output none


Write-Host "All environment variables have been set up successfully!" -ForegroundColor Green
Write-Host "Below shows all environment values" -ForegroundColor Green

az functionapp config appsettings list -g $resourceGroup -n $functionAppName 