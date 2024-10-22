# microsoft-graph-api-log-pipeline

This project leverages **Microsoft Graph API change notifications** to store logs from various activities for efficient retrieval and analysis. The project ensures structured log pipeline and storage.

- **Microsoft Graph API**: A unified API endpoint to access data and services from the Microsoft ecosystem. This project focuses its change notification feature.
  - **change notification**: When subscribed resource is changed (e.g. a subscribed user is listed to a canledar event), Microsoft Graph API will notify given webhook URL with log as payload.

## Features

- **Real-time log ingestion** using Microsoft Graph API change notifications.
- **Cloud-native application**: All components are in the cloud environment, ensuring scalability, availability and easy maintenance.
- **Event-driven architecture** for reliable log streaming and processing.
- **Subscription to specific resources:**
  - **User Events**: Logs user events such as calendar updates.
  - **Call Records**: Captures call record details for analysis.
  - **Chat Messages**: Tracks chat messages, limited to **the chatroom in a call**.


## Overview of Subscribed Resource

| **Resource Name** | **Description**                                                           | **Changed Type** | **Graph API Path**            |
|-------------------|---------------------------------------------------------------------------|------------------|-------------------------------|
| user (events)     | User-related events. (e.g. calendar changes)                              | Created          | `/user/{userId}/events`       |
| callRecords       | Details of a Teams call.                                                  | Created          | `/communications/callRecords` |
| chatMessages      | Mmessages in a chat. (in this project, only chat of call will be tracked) | X                | `/chats/{chatId}/messages`    |

**Notice:** Because we only want to track messages in the chatroom in a call, we query chatMessages with `chatId` from Graph API directly, instead of creating a subscribtion.

## Architecture

![](/Assets/subscription.png)

**Microsoft Graph API Subscription**

Creating subscriptions is the prerequisite to activate Graph API change notification. It tells Graph API waht resources have to be tracked.
- The subscriptions have expire time, so renew process is needed.
  - Store the subscription list to blob storage for facliating renew process. 
- **Subscription Service** will create/renew subscriptions. 
  - You can send a HTTP request to trigger this process, or just wait cronjob trigger it (at 00:00, GMT+8)

<br/>

![](/Assets/log-pipeline.png)

**Handling webhook requests**

When subscribed resources changes, Microsoft Graph API will send notification to given webhook URL.
- **Log Redirection Service** play as request handler, handling incoming notifications and redirecting the data.
  - The incoming data will be parsed into JSON format.
  - The destination of redirection can be Event Hub or Stroage Account. 

<br/>

**Streaming process**

- This project utilize Event Hub to implement asynchronous log storage.
  - There are several topics (by resource type).
  - **Log Redirection Service** is the producer.
  - **Azure Fabric** is the consumer.

<br/>

Reference:
- [Microsoft Graph API change notifications](https://learn.microsoft.com/en-us/graph/api/resources/change-notifications-api-overview?view=graph-rest-1.0)
- [subscription resource type](https://learn.microsoft.com/en-us/graph/api/resources/subscription?view=graph-rest-1.0)


## Repo Structure

Files not included are auto-generated or metadata.

``` sh
📂 App
┣ 📂 Models
┃ ┗ 📜 Subscription.cs
┣ 📂 Utils
┃ ┣ 📜 AuthenticationConfig.cs
┃ ┗ 📜 UtilityFunction.cs
┣ 📜App.csproj
┣ 📜CallRecordService.cs             # Log Redirection Service
┣ 📜host.json
┣ 📜local.settings.json
┣ 📜Program.cs
┣ 📜SubscriptionService.cs           # Subscription Service
┗ 📜UserEventService.cs              # Log Redirection Service
📂 Scripts
┣ 📜 azure_operation.ps1
┣ 📜 functionApp_operation.ps1
┗ 📜 req.ps1
```

## Prerequisites

### Required Azure Resource

- **App Registration**
- **Function App**
- **Storage Account**
- **Storage Queue**
- **Event Hub**
- **Azure Data Explorer (ADX) / Azure Fabric** 

**Follow [here](#3-azure-resource-preparation-by-powershell-scripts) to create resources by Powershell scrips.**

**Check [here](#4-azure-resource-preparation-by-azure-portal) to see reminders if you want to create resources manually in Azure Portal.**

### Required permissions for **Graph API Access** (In App Registration)

| **Permission**                        | **Description**                                                                                   |
|---------------------------------------|---------------------------------------------------------------------------------------------------|
| User.Read.All (Application)           | Allows the app to read user profiles without a signed in user.                                    |
| Calendars.Read (Application)          | Allows the app to read events of all calendars without a signed-in user.                          |
| CallRecords.Read.All (Application)    | Allows the app to read call records for all calls and online meetings without a signed-in user.   |


## Powershell Script Setup Instructions

**Notice that instructions in this section assume you use Windows as OS, and powershell as terminal.**

**Please `cd` to `\Scripts` whenever you excute a script.**

### Prerequisite

- Install Azure CLI from [here](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows?tabs=azure-cli)
- Install Azure Function App Core Tools (Node.js evnrionment required)
  - `npm install -g azure-functions-core-tools@4 --unsafe-perm true`  


``` sh
📦 teams-log-pipeline
 ┣ 📂 Scripts
 ┃ ┣ 📜 azure_operation.ps1         #  Create all required Azure resources
 ┃ ┣ 📜 functionApp_operation.ps1   #  Deploy Function App (include env variables)
 ┃ ┗ 📜 req.ps1                     #  Utility script for sending http request easily
```

### azure_operation.ps1

This script is used for creating required resource in your Azure: 

1. **App Registration** with below Microsoft Graph API permissions (need admin grant to activate)
    1. User.Read.All
    2. Calendars.Read
    3. CallRecords.Read.All
2. **Blob Storage**
3. **Event Hub**
4. **Function App**

</br>

After script execution, you need:

1. **Follow the output messages to set up `App\local.setting.json` file, you can directly override the `App\local.setting.json.example` provided in repo.**
2. **Request your admin to grant the permission requirement in App Registraion.**
    1. **If you're a tenant admin**, run `az ad app permission admin-consent --id {appId}` or use Azure Portal to grant.


### functionApp_operation.ps1

This script is used for Function App deployment and configure its environment variables.

**Be aware that this script will load the value in `local.settings.json` to configure environment variables, if you don't add value in `local.settings.json`, it won't work.**


## Manually Setup Instructions (by Azure Portal)

### App Registration (Service Principal)

1. Grant the Graph API permissions mentioned in [here](#2-graph-api-access-with-appropriate-permissions-in-app-registration).
2. Generate a client secret in "Certificates & secrets" tab.

**Requried credentials**:
- **client ID**
- **tenant ID**
- **client Secret** (generated in step 2.)

### Storage Accounts

The storage Accounts will auto-create during creating Function App, so **no need** to create it manaully.

**Required credentials**:
- **Connection string** (in "Access keys" tab)

### Event Hub

Create the Event Hub Namespace first, then add three Event Hub in the namespace.

The names these three entities must be ["userevents-topic", "callrecords-topic", "chatmessages-topic"].

**Required credentials**:
- **Connection string** (in "Shared access policies" tab)

### Function App

Create a Function App with following configuration:
- Runtime Stack: .NET
- Version: 8, isolated worker model
- Operating System: Windows

**Required credentials**:
- **FUNCTION_APP_NAME**
- **_master key** (in "App keys" tab)

### Environment configuration in Function App

Paste all credentials above mentioned as value with corresponding name in [here](#environment-variables).

## Environment Variables

| **Name**                        | **Description**                                                                          |
|---------------------------------|------------------------------------------------------------------------------------------|
| CHAT_API_TOGGLE                 | Turn on the "chatMessage" notification or not. (True/False)                              |
| EVENT_HUB_FEATURE_TOGGLE        | Decide to use Event Hub or Account Storage as redirection destination.  (True/False)     |
| TENANT_ID                       | The identifier of working tenant.                                                        |
| CLIENT_ID                       | The identifier of App Registration                                                       |
| CLIENT_SECRET                   | The secret of App Registration, used for creating a authenticated Graph API client.      |
| FUNCTION_APP_NAME               | Config the webhook URL during create/renew subscription process                          |
| FUNCTION_DEFAULT_KEY            | Config the webhook URL during create/renew subscription process                          |
| EVENT_HUB_CONNECTION_STRING     | Config Event Hub connection                                                              |
| BLOB_CONNECTION_STRING          | Config Storage Account connection                                                        |

## Usage

### 1. Create / Renew Subscription 

**By HTTP Request**

Find the URL in Azure Portal first.

![](/Assets/URL.png)

Copy and paste the URL in `Scripts\req.ps1` and excute script to initialize subscription.

</br>

**By Timer Trigger**

Just wait for cronjob in everyday 00:00 (GMT+8)


### 2. Check subscription

In Storage Account, check whether `subscription-container` contains `subscriptionList.json`.

You also can check which user account is subscribed in `subscriptionList.json`.

### 3. Test the pipeline

Create a calendar event for subscribed user.

Normally, the Event Hub (or Storage Account) will receive the log in real-time level.