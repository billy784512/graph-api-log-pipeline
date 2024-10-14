# microsoft-teams-log-pipeline

# 1. Project Background

This project aims to track user activities in Microsoft Teams and store logs in ADX for future retrieval and analysis.

The following Teams events will be logged for each tracked user:
- **User Event**: Events or meetings scheduled in Teams Calendar
- **Call Record**: Meetings that the user actually joins.
- **Chat Message**: Messages in the chatroom of meetings the user has joined.

### Required Resources:
- **Microsoft Graph API**: A unified API endpoint to access data and services from the Microsoft ecosystem. This project focuses on the subscription feature for Teams.
- **Azure**:
  - **App Registration**
  - **Function App**
  - **Storage Account**
  - **Storage Queue**
  - **Event Hub**
  - **Azure Data Explorer (ADX)**

**Notice that below instructions assume you use Windows as OS, and powershell as terminal.**

## 2. Repo Overview

### System Architecture
Comming Sooooooooon...

### Repo Structure

Files not included are auto-generated or metadata.

```
📂 App
┣ 📂 Models
┃ ┗ 📜 Subscription.cs
┣ 📂 Utils
┃ ┣ 📜 AuthenticationConfig.cs
┃ ┗ 📜 UtilityFunction.cs
┣ 📜App.csproj
┣ 📜CallRecordService.cs
┣ 📜host.json
┣ 📜local.settings.json
┣ 📜Program.cs
┣ 📜SubscriptionRenewalService.cs
┗ 📜UserEventService.cs
📂 Scripts
┣ 📜 azure_operation.ps1
┣ 📜 functionApp_operation.ps1
┗ 📜 req.ps1
```

## 3. Azure Resource Preparation


Follow [3.1](#31-create-azure-resources-by-powershell-scripts) to create resources by scrips.

Follw [3.2](#32-create-azure-resources-by-azure-portal-comming-sooooon) to create resources by Azure Portal (manually).


### 3.1 Create Azure resources by powershell scripts

First, install Azure CLI from [here](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows?tabs=azure-cli)


```powershell
# Login to Azure
az login
az login -- tenant {tenant_id}

# Set subscription
az account set --subscription {subscription_id}
```


``` sh
📦 teams-log-pipeline
 ┣ 📂 Scripts
 ┃ ┣ 📜 azure_operation.ps1         #  Create all required Azure resources
 ┃ ┗ 📜 functionApp_operation.ps1   #  Deploy Function App (include env variables)
```

**Please `cd` to `\Scripts` when you excute a script.**

#### 3.1.1 azure_operation.ps1

This script is used for creating required resource in your Azure for this project. Below list created resources: 

1. **App Registration** with below Microsoft Graph API permissions (need admin grant to activate)
    1. User.Read.All
    2. Calendar.Read
    3. CallRecords.Read.All
2. **Blob Storage**
3. **Function App**
4. **Event Hub**

After script execution, you need:

1. **Follow the output messages to set up `App\local.setting.json` file**
2. **Request your admin to grant the permission requirement in App Registraion**
    1. **If you're a tenant admin**, run `az ad app permission admin-consent --id {appId}` or use Azure Portal to grant.


#### 3.1.2 functionApp_operation.ps1

This script is used for Function App deployment and configure its environment variables.

**Be aware that this script will load the value in `local.settings.json` to configure environment variables, if you don't add value in `local.settings.json`, it won't work.**


### 3.2 Create Azure resources by Azure Portal (comming sooooon...)

#### 3.2.1 App Registration (Service Principal)

#### 3.2.2 Storage Accounts

#### 3.2.3 Event Hub

#### 3.2.4 Function App

## 4. Initialize Subscription & Demo

Find the URL in Azure Portal first.

![](/Assets/URL.png)

Copt and paste the URL in `Scripts\req.ps1` and excute it to initialize subscription.


