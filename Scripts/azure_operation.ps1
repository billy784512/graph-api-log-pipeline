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

# Get user-input related info.
$resourceGroup = Read-Host -Prompt "Enter your Azure Resource Group name"

# Generate a random postfix to prevent duplicated resource name
$length = 4
$characters = 'abcdefghijklmnopqrstuvwxyz0123456789'

$randomIndices = @(1..$length | ForEach-Object { Get-Random -Minimum 0 -Maximum $characters.Length })
$randomPostfix = -join ($randomIndices | ForEach-Object { $characters[$_] })

# Define resource name
$StorageAccountName = "$resourceGroup$randomPostfix"
$functionAppName = "teams-log-pipeline-$randomPostfix"
$appName = "teams-log-pipeline-$randomPostfix"
$eventHubNamespace = "teams-log-pipeline-$randomPostfix"

$eventHubNameList = @("userevents-topic", "callrecords-topic", "chatmessages-topic")
$policyName = "RootManageSharedAccessKey"

# Account Storage
Write-Host "===============================================" -ForegroundColor Yellow
Write-Host "Start creating a new Storage Account in your Azure..." -ForegroundColor Yellow

try {
    az storage account create `
        -n $StorageAccountName --location "westus" `
        -g $resourceGroup `
        --sku Standard_LRS
} catch {
    Stop-Script "Failed to create Storage Account."
}

Write-Host "Storage Account has been created successfully!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green

# Event Hub
Write-Host "===============================================" -ForegroundColor Yellow
Write-Host "Start creating a new Event Hub in your Azure..." -ForegroundColor Yellow

try {
    az eventhubs namespace create `
        --resource-group $resourceGroup `
        --name $eventHubNamespace `
        --location "westus"

    foreach ($eventHubName in $eventHubNameList) {
        az eventhubs eventhub create `
            --resource-group $resourceGroup `
            --namespace-name $eventHubNamespace `
            --name $eventHubName `
            --partition-count 2
    } 
} catch {
    Stop-Script "Failed to create Event Hub."
}

Write-Host "Event Hub has been created successfully!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green

# FunctionApp
Write-Host "===============================================" -ForegroundColor Yellow
Write-Host "Start creating a new Function App in your Azure..." -ForegroundColor Yellow

try {
    az functionapp create --resource-group $resourceGroup `
        --consumption-plan-location "westus" `
        --runtime dotnet-isolated `
        --runtime-version 8.0 `
        --functions-version 4 `
        --name $functionAppName `
        --storage-account $StorageAccountName
} catch {
    Stop-Script "Failed to create Function App."
}

Write-Host "Function App has been created successfully!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green

# App Registration
Write-Host "===============================================" -ForegroundColor Yellow
Write-Host "Start creating a new App Registration in your Azure..." -ForegroundColor Yellow

try {
    $app = az ad app create --display-name $appName --query appId --output json
    $appId = $app | ConvertFrom-Json 
    az ad app permission add --id $appId --api 00000003-0000-0000-c000-000000000000 --api-permissions df021288-bdef-4463-88db-98f22de89214=Role  # User.Read.All
    az ad app permission add --id $appId --api 00000003-0000-0000-c000-000000000000 --api-permissions 798ee544-9d2d-430c-a058-570e29e34338=Role  # Calendars.Read
    az ad app permission add --id $appId --api 00000003-0000-0000-c000-000000000000 --api-permissions 45bbb07e-7321-4fd7-a8f6-3ff27e6a81c8=Role  # CallRecords.Read.All
    $clientSecretObject = az ad app credential reset --id $appId --append --years 1 --query "{clientSecret:password}" --output json | ConvertFrom-Json
} catch {
    Stop-Script "Failed to create App Registration or set permissions."
}

Write-Host "App Registration has been created successfully!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green

$tenantId = az account show --query tenantId --output tsv
$clientSecret = $clientSecretObject.clientSecret
$functionDefaultKey = az functionapp keys list --name $functionAppName --resource-group $resourceGroup --query masterKey --output tsv
$storageConnectionString = az storage account show-connection-string --resource-group $resourceGroup --name $StorageAccountName --query connectionString --output tsv
$eventHubConnectionString = az eventhubs namespace authorization-rule keys list --resource-group $resourceGroup --namespace-name $eventHubNamespace --name $policyName --query primaryConnectionString --output tsv

Write-Host "All resources (App Registration, Function App, Storage Account) have been created successfully!" -ForegroundColor Green
Write-Host "Now, please follow the below instructions:" -ForegroundColor Green

Write-Host "1. Inform your tenant admin to grant the API permission required by App registration." -ForegroundColor Blue
Write-Host "If you're exactly the tenant admin, run this command to grant:" -ForegroundColor Yellow
Write-Host "az ad app permission admin-consent --id $appId"

Write-Host "2. Paste below info to your local.settings.json" -ForegroundColor Blue
Write-Host "CHAT_API_TOGGLE: True" -ForegroundColor Yellow
Write-Host "EVENT_HUB_FEATURE_TOGGLE: True" -ForegroundColor Yellow
Write-Host "TENANT_ID: $tenantId" -ForegroundColor Yellow
Write-Host "CLIENT_ID: $appId" -ForegroundColor Yellow
Write-Host "CLIENT_SECRET: $clientSecret" -ForegroundColor Yellow
Write-Host "FUNCTION_APP_NAME: $functionAppName" -ForegroundColor Yellow
Write-Host "FUNCTION_DEFAULT_KEY: $functionDefaultKey" -ForegroundColor Yellow
Write-Host "EVENT_HUB_CONNECTION_STRING: $eventHubConnectionString" -ForegroundColor Yellow
Write-Host "BLOB_CONNECTION_STRING: $storageConnectionString" -ForegroundColor Yellow

Write-Host "3. Run functionApp_operation.ps1 for Function App deployment and configuration." -ForegroundColor Blue

Write-Host "Script done! exit code 0" -ForegroundColor Green
exit 0