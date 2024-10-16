# microsoft-teams-log-pipeline

## 1. Project Background

This project aims to track user activities in Microsoft Teams via Microsoft Graph API change notifications and store logs for future retrieval and analysis.

The following Microsoft Teams events will be logged for each tracked user:
- **User Event**: Events or meetings scheduled in Teams Calendar
- **Call Record**: Meetings that the user actually joins.
- **Chat Message**: Messages in the chatroom of meetings the user has joined.

### Required Resources:
- **Microsoft Graph API**: A unified API endpoint to access data and services from the Microsoft ecosystem. This project focuses its change notification feature.
  - **change notification**: When tracked event is happend (e.g. a new canledar event is created), Microsoft Graph API will notify given webhook URL.
- **Azure**:
  - **App Registration**
  - **Function App**
  - **Storage Account**
  - **Storage Queue**
  - **Event Hub**
  - **Azure Data Explorer (ADX) / Azure Fabric** 

**Notice that below instructions assume you use Windows as OS, and powershell as terminal.**

## 2. Repo Overview

### System Architecture

Reference:
- [Microsoft Graph API change notifications](https://learn.microsoft.com/en-us/graph/api/resources/change-notifications-api-overview?view=graph-rest-1.0)
- [subscription resource type](https://learn.microsoft.com/en-us/graph/api/resources/subscription?view=graph-rest-1.0)

<br/>

![](/Assets/subscription.png)

Creating subscriptions is the prerequisite to activate Graph API change notification. 
- The purpose of subscription resource type is to tell Graph API who and waht events have to track."
- The subscriptions have expire time, so renew process is needed.
  - Store the subscription list to blob storage for facliating renew process. 

<br/>

![](/Assets/log-pipeline.png)

After subscribed event is triggered, Graph API will send a HTTP request to a webhook URL.
- The webhook URL can be customized during subscription creating.
- There are several log redirection services to handle notification .

<br/>

Then, Log redirection services will:
- Parse incoming HTTP body to log (JSON).
- Pass logs to Event Hub.
- Store logs to Storage Account for persistent storage (optional).

<br/>

Finally, Azure Fabric consumes logs in EventHub consistently.


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

**Follow [3.](#3-azure-resource-preparation-by-powershell-scripts) to create resources by Powershell scrips.**

**Check [4.](#4-azure-resource-preparation-by-azure-portal) if you want to create resources manually in Azure Portal.**

## 3. Azure Resource Preparation (by Powershell scripts)

prerequisite:

- Install Azure CLI from [here](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows?tabs=azure-cli)
- Install Azure Function App Core Tools (Node.js evnrionment required)
  - `npm install -g azure-functions-core-tools@4 --unsafe-perm true`  


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

### 3.1 azure_operation.ps1

This script is used for creating required resource in your Azure for this project. Below list created resources: 

1. **App Registration** with below Microsoft Graph API permissions (need admin grant to activate)
    1. User.Read.All
    2. Calendar.Read
    3. CallRecords.Read.All
2. **Blob Storage**
3. **Function App**
4. **Event Hub**

After script execution, you need:

1. **Follow the output messages to set up `App\local.setting.json` file, you can directly override the `App\local.setting.json.example` in repo.**
2. **Request your admin to grant the permission requirement in App Registraion.**
    1. **If you're a tenant admin**, run `az ad app permission admin-consent --id {appId}` or use Azure Portal to grant.


### 3.2 functionApp_operation.ps1

This script is used for Function App deployment and configure its environment variables.

**Be aware that this script will load the value in `local.settings.json` to configure environment variables, if you don't add value in `local.settings.json`, it won't work.**


## 4 Azure Resource Preparation (by Azure Portal)

### 4.1 App Registration (Service Principal)

### 4.2 Storage Accounts

### 4.3 Event Hub

### 4.4 Function App

## 5. Initialize Subscription & Demo

Find the URL in Azure Portal first.

![](/Assets/URL.png)

Copy and paste the URL in `Scripts\req.ps1` and excute it to initialize subscription.


