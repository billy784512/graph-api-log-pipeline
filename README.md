# microsoft-teams-log-pipeline

## 1. Project Background

This project aims to track user activaties in Microsfot Teams and store logs in ADX for future retrieval and analysis.

For the tracked user, the following three Teams events will be logged:
- **User event**: Events or meetings scheduled in Teams Calendar
- **Call Record**: Meetings that the user actually joins.
- **Chat Message**: Messages in the chatroom of meetings the user has joined.

In this project, Following tech stack will be utilize:
- **Microsoft Graph API**: A unified API endpoint for easy access to data and services from across the Microsoft ecosystem. Here we focus on the usage of the subscrption feature for Teams.
- **Azure**:
  - **FunctionApp**
  - **StorageAccount**
  - **StorageQueue**
  - **EventHub**
  - **Azure Data Explorer (ADX)**


## 2. System Architecture

## 3. Azure resource preparation (By script, recommended)

You can use script provided in this repo to create resource, deploy functionApp and config functionApp automatically.
However, there still have a little bit operations need you perform manually. 

So follow below step-by-step guide!

### 3.1 Azure CLI

Install Azure CLI from here:
https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows?tabs=azure-cli


```powershell
# Login
az login
az login -- tenant {tenant_id}

# Set subscription
az account set --subscription {subscription_id}
```

### 3.2 Run scripts

1. Run `.\Scripts\azure_operation.ps1`, this script will do:
    1. Create all required resources in your Azure cloud
2. In previous step, the App Registration required several Graph API permissions, and they need to be granted:
    1. **If you're tenant admin**, run `az ad app permission admin-consent --id {appId}`, where appId can be found by `az ad app list --displayName {appName}`
    2. **If you're not tenant admin**, inform your admin to grant.
3. Follow the output message from `.\Scripts\azure_operation.ps1` to setup `.\App\local.setting.json` file
    1. Typically, this file is a local environment file to help you develope and debug. But in next step, we will deploy these local variables to Azure FunctionApp.
4. Run `.\Scripts\functionApp_operation.ps1`, this script will do:
    1. Deploy the local function project (in `.\App`) to Azure FunctionApp
    2. Config the Azure FunctionApp environment variable based on `.\App\local.setting.json`

## 4. Azure resource prepartion (Manaual, to be updated soon)

### 4.1 Azure CLI

Install Azure CLI from here:
https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows?tabs=azure-cli


```powershell
# Login
az login
az login -- tenant {tenant_id}

# Set subscription
az account set --subscription {subscription_id}
```

### 4.2 Storage Accounts

**It's a prerequisite for creating a Azure function App**

```powershell
az storage account create `
	-n {name} --location {location} `
	-g {resource_group_name} `
	--sku Standard_LRS
```

This storage account will help function App for state management, function scaling, logging, etc.

### 4.3 FunctionApps

``` powershell
# Create a FunctionApp in Azure
az functionapp create --resource-group {resource_group_name} `
    --consumption-plan-location {location} `
    --runtime dotnet-isolated `
    --runtime-version 8.0 `
    --functions-version 4 `
    --name {function_app_name} `
    --storage-account {storage_account_name}
```

``` powershell
# Deploy
# Assume you're in the root dir of this project
cd App

Compress-Archive -Path . app.zip

az functionapp deployment source config-zip ` 
    -g {resource_group_name} `
    -n {function_app_name} `
    --src app.zip

func azure functionapp publish {function_app_name}
```

If you encounter this error message during executing `func azure functionapp publish`:
```
Can't determine Project to build. Expected 1 .csproj or .fsproj but found 2
```
The quick workaround is remove `./App/bin` and `./App/obj` folders.

### 4.4 App Registration (Service Principal)

``` powershell
az ad app create --display-name {your_app_name}

# User.Read.All
az ad app permission add --id {appId} --api 00000003-0000-0000-c000-000000000000 --api-permissions df021288-bdef-4463-88db-98f22de89214=Role
# Calendar.Read
az ad app permission add --id {appId} --api 00000003-0000-0000-c000-000000000000 --api-permissions 798ee544-9d2d-430c-a058-570e29e34338=Role
# CallRecords.Read.All
az ad app permission add --id {appId} --api 00000003-0000-0000-c000-000000000000 --api-permissions 45bbb07e-7321-4fd7-a8f6-3ff27e6a81c8=Role

# Gen a clientSecret
az ad app credential reset --id $appId --append --years 1

# Grant permissions
az ad app permission admin-consent --id {appId}

```

# 5. Init subscriptio & Demo (To be updated soon) 