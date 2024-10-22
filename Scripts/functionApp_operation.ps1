function Stop-Script($errorMessage) {
    Write-Host "Error: $errorMessage, exit code 1" -ForegroundColor Red
    exit 1
}

function Get-AzLogin{
    $azLoginCheck = az account show --query "{name:name}" --output tsv 2>&1

    if ($azLoginCheck -like "*Please run 'az login'*") {
        return $false
    } 
    elseif ($azLoginCheck -eq ""){
        return $false
    } 
    else {
        return $true
    }
}

# Login
if (!(Get-AzLogin)) {
    try {
        Write-Host "You've not login yet. This process try login now..."
        $tenantId = Read-Host -Prompt "Enter your tenant id"
        $subscriptionId = Read-Host -Prompt "Enter your subscription id"
        az login -- tenant $tenantId
        az account set --subscription $subscriptionId
        Write-Host "Login successfully..." -ForegroundColor Green
    } catch {
        Stop-Script "Failed to login Azure"
    }
}

# Define the Azure Function App name and resource group
$functionAppName = Read-Host -Prompt "Enter your Azure Function App name"
$resourceGroup = Read-Host -Prompt "Enter your Azure Resource Group name"


# ---------------------- Deployment ----------------------
try {
    Write-Host "Start Function App deployment..." -ForegroundColor Yellow

    Set-Location -Path "..\App"

    # Redudant file for deployment
    if (Test-Path -Path ".\bin") {
        Remove-Item -Path ".\bin" -Recurse -Force
    }
    if (Test-Path -Path ".\obj") {
        Remove-Item -Path ".\obj" -Recurse -Force
    }

    # Zip file for deployment
    Compress-Archive -Path . app.zip

    az functionapp deployment source config-zip `
        -g $resourceGroup `
        -n $functionAppName `
        --src app.zip

    func azure functionapp publish $functionAppName

    Remove-Item -Path ".\app.zip" -Force
    Write-Host "Function App deploy successfully!" -ForegroundColor Green
} catch {
    Stop-Script "Failed to deploy function app"
}

# ---------------------------------------------------------------------


# ---------------------- Env Variables Setting ----------------------
Write-Host "Start config Function App environment variable..." -ForegroundColor Yellow

$localSettingsFile = ".\local.settings.json"

# Redundant keys
$excludedKeys = @("AzureWebJobsStorage", "FUNCTIONS_WORKER_RUNTIME")

# Check if the local.settings.json file exists
if (-Not (Test-Path $localSettingsFile)) {
    Stop-Script "local.settings.json not found!"
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

exit 0