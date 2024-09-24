# microsoft-teams-log-pipeline

# 1. Project Background

This project aims to track user activities in Microsoft Teams and store logs in ADX for future retrieval and analysis.

The following Teams events will be logged for each tracked user:
- **User event**: Events or meetings scheduled in Teams Calendar
- **Call Record**: Meetings that the user actually joins.
- **Chat Message**: Messages in the chatroom of meetings the user has joined.

### Tech Stack:
- **Microsoft Graph API**: A unified API endpoint to access data and services from the Microsoft ecosystem. This project focuses on the subscription feature for Teams.
- **Azure**:
  - **App Registration**
  - **Function App**
  - **Storage Account**
  - **Storage Queue**
  - **Event Hub**
  - **Azure Data Explorer (ADX)**

** Notice that below instructions assume you use Windows as OS, and powershell as terminal.**

## 2. Repo Overview (Coming soon...)

### System Architecture

### Repo Structure

Not included files are auto-generated or repo metadata.

```
📦 teams-log-pipeline
 ┣ 📂 App
 ┃ ┣ 📂 daemon_console
 ┃ ┃ ┣ 📜 AuthenticationConfig.cs
 ┃ ┃ ┗ 📜 GlobalFunction.cs
 ┃ ┣ 📂 global_class
 ┃ ┃ ┗ 📜 Object.cs
 ┃ ┣ 📜 CallRecordService.cs
 ┃ ┣ 📜 SubscriptionRenewalService.cs
 ┃ ┗ 📜 UserEventService.cs
 ┣ 📂 Scripts
 ┃ ┣ 📜 azure_operation.ps1
 ┃ ┗ 📜 functionApp_operation.ps1
```

## 3. Azure resource preparation


You can use the provided scripts to automatically create resources, deploy the Function App, and configure the Function App. 
However, some manual steps are still required. Follow the step-by-step guide below.


### 3.1 Azure CLI

Install Azure CLI from [here](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows?tabs=azure-cli)


```powershell
# Login to Azure
az login
az login -- tenant {tenant_id}

# Set subscription
az account set --subscription {subscription_id}
```

### 3.2 Create Azure resources by scripts (recommended)

``` sh
📦 teams-log-pipeline
 ┣ 📂 Scripts
 ┃ ┣ 📜 azure_operation.ps1         #  Create all required Azure resources
 ┃ ┗ 📜 functionApp_operation.ps1   #  Deploy Function App (include env variables)
```

1. Run `azure_operation.ps1`
2. In `azure_operation.ps1`, the App Registration requires several Microsoft Graph API permissions, which need to be granted:
    1. **If you're a tenant admin**, run `az ad app permission admin-consent --id {appId}`
    2. **If you're not a tenant admin**, request your admin to grant the required permission.
3. Follow the output messages from `azure_operation.ps1` to set up `App\local.setting.json` file
    1. `App\local.setting.json` stores local environment variables for development and debugging. 
    2. In the next step, we will deploy these local variables to Azure Function App.
4. Run `functionApp_operation.ps1`

**After completing these four steps, all the necessary resources for this project have been successfully created and properly configured in your Azure environment.**

### 3.3 Create Azure resources manually (to be completed soon...)

#### 3.3.1 Storage Accounts

**It's a prerequisite for creating a Azure function App**

```powershell
az storage account create `
	-n {name} --location {location} `
	-g {resource_group_name} `
	--sku Standard_LRS
```

The storage account is used for state management, function scaling, logging, etc., in the Function App.

#### 3.3.2 FunctionApps

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

If you encounter this error during the execution of `func azure functionapp publish`:
```
Can't determine Project to build. Expected 1 .csproj or .fsproj but found 2
```
A quick workaround is to delete the `./App/bin` and `./App/obj` folders.

#### 3.3.3 App Registration (Service Principal)

``` powershell
az ad app create --display-name {your_app_name}

# User.Read.All
az ad app permission add --id {appId} --api 00000003-0000-0000-c000-000000000000 --api-permissions df021288-bdef-4463-88db-98f22de89214=Role
# Calendar.Read
az ad app permission add --id {appId} --api 00000003-0000-0000-c000-000000000000 --api-permissions 798ee544-9d2d-430c-a058-570e29e34338=Role
# CallRecords.Read.All
az ad app permission add --id {appId} --api 00000003-0000-0000-c000-000000000000 --api-permissions 45bbb07e-7321-4fd7-a8f6-3ff27e6a81c8=Role

# Generate a clientSecret
az ad app credential reset --id $appId --append --years 1

# Grant permissions
az ad app permission admin-consent --id {appId}

```

## 4. Initialize Subscription & Demo (Coming soon...)



