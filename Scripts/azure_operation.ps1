# Generate a random postfix to prevent duplicated resource name
$length = 4
$characters = 'abcdefghijklmnopqrstuvwxyz0123456789'

$randomIndices = @(1..$length | ForEach-Object { Get-Random -Minimum 0 -Maximum $characters.Length })
$randomPostfix = -join ($randomIndices | ForEach-Object { $characters[$_] })


# Get user-input related info.
$resourceGroup = Read-Host -Prompt "Enter your Azure Resource Group name"


# Define resource name
$webJobStorageName = "$resourceGroup$randomPostfix"
$functionAppName = "teams-log-pipeline-$randomPostfix"
$appName = "teams-log-pipeline-$randomPostfix"


# Account Storage
Write-Host "===============================================" -ForegroundColor Yellow
Write-Host "Start creating a new Storage Account in your Azure..." -ForegroundColor Yellow

az storage account create `
	-n $webJobStorageName --location "westus" `
	-g $resourceGroup `
	--sku Standard_LRS

Write-Host "Storage Account have been created successfully!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green

# FunctionApp
Write-Host "===============================================" -ForegroundColor Yellow
Write-Host "Start creating a new FunctionApp in your Azure..." -ForegroundColor Yellow

az functionapp create --resource-group $resourceGroup `
    --consumption-plan-location "westus" `
    --runtime dotnet-isolated `
    --runtime-version 8.0 `
    --functions-version 4 `
    --name $functionAppName `
    --storage-account $webJobStorageName

Write-Host "FunctionApp have been created successfully!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green

# App Registration
Write-Host "===============================================" -ForegroundColor Yellow
Write-Host "Start creating a new App Registration in your Azure..." -ForegroundColor Yellow

$app = az ad app create --display-name $appName --query appId --output json
$appId = $app | ConvertFrom-Json 
az ad app permission add --id $appId --api 00000003-0000-0000-c000-000000000000 --api-permissions df021288-bdef-4463-88db-98f22de89214=Role  # User.Read.All
az ad app permission add --id $appId --api 00000003-0000-0000-c000-000000000000 --api-permissions 798ee544-9d2d-430c-a058-570e29e34338=Role  # Calendar.Read
az ad app permission add --id $appId --api 00000003-0000-0000-c000-000000000000 --api-permissions 45bbb07e-7321-4fd7-a8f6-3ff27e6a81c8=Role  # CallRecords.Read.All

$clientSecretObject = az ad app credential reset --id $appId --append --years 1 --query "{clientSecret:password}" --output json | ConvertFrom-Json

Write-Host "App Registration have been created successfully!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green


$tenantId = az account show --query tenantId --output tsv
$clientSecret = $clientSecretObject.clientSecret
$functionDefaultKey = az functionapp keys list --name $functionAppName --resource-group $resourceGroup --query masterKey --output tsv


Write-Host "All resources (AppRegistraion, FunctionApp, StorageAccount) have been created successfully!" -ForegroundColor Green
Write-Host "Now, please follow below instructions" -ForegroundColor Green

Write-Host "1. inform your tenant admin to grant the API permission requeired by App registration." -ForegroundColor Blue
Write-Host "If you're excalty the tenant admin, run this command to grant:" -ForegroundColor Yellow
Write-Host "az ad app permission admin-consent --id $appId"

Write-Host "2. Paste below info to your local.settings.json" -ForegroundColor Blue
Write-Host "CHAT_API_TOGGLE: true" -ForegroundColor Yellow
Write-Host "TENANT_ID: $tenantId" -ForegroundColor Yellow
Write-Host "CLIENT_ID: $appId" -ForegroundColor Yellow
Write-Host "CLIENT_SECRET: $clientSecret" -ForegroundColor Yellow
Write-Host "FUNCTION_APP_NAME: $functionAppName" -ForegroundColor Yellow
Write-Host "FUNCTION_DEFAULT_KEY: $functionDefaultKey" -ForegroundColor Yellow

Write-Host "3. Run functionApp_operation.ps1 for functionApp depolyment and configuration" -ForegroundColor Blue


