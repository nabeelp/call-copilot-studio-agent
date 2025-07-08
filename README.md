# Copilot Studio Client – Azure Functions with Entra ID Authentication

This solution provides an HTTP-triggered Azure Function app that interacts with Copilot Studio hosted agents, supporting secure Entra ID authentication and pass-through of bearer tokens. It is based on a conversion from a console app to a scalable, cloud-ready Azure Functions architecture.

---

## Web UI for Testing

A basic web UI is provided in the `src/frontend` folder to help with testing the API and authentication flow. This UI allows you to sign in, acquire tokens, and interact with the backend Azure Functions endpoints easily during development.

---

## Prerequisites

- .NET 8.0 SDK
- Azure Functions Core Tools v4
- Azure subscription (for deployment)
- Agent created in Microsoft Copilot Studio (published)
- Entra ID App Registration with CopilotStudio.Copilots.Invoke API permission

- Entra ID App Registrations for both the API and the Web UI (see below)

---

## Solution Overview

- **Azure Functions**: HTTP-triggered endpoints for health check, starting conversations, and sending messages to Copilot Studio agents.
- **Entra ID Authentication**: Functions require valid Entra ID bearer tokens, which are validated and securely passed through to Copilot Studio APIs.
- **PassThroughTokenHandler**: Custom handler extracts and applies incoming bearer tokens to outgoing Copilot Studio API calls, ensuring secure, delegated access.
- **Configuration**: All settings are managed via `local.settings.json` (for local dev) or Azure App Settings (for cloud deployment).

---

## Setup Instructions

### 1. Create and Publish a Copilot Studio Agent

1. Go to [Copilot Studio](https://copilotstudio.microsoft.com)
2. Create and publish a new agent
3. In Copilot Studio, navigate to **Settings → Advanced → Metadata** and record:
    - **Schema name**
    - **Environment Id**
4. Use these values in your Azure Functions configuration to connect to the correct agent.

### 2. Register Applications in Entra ID (Azure AD)

You will need **two** app registrations in Entra ID:

- **API App Registration**: For the Azure Functions backend
- **Web UI App Registration**: For the frontend test UI

#### a. Register the API Application

1. In [Azure Portal](https://portal.azure.com), go to **Entra ID → App registrations → New registration**
2. Name it (e.g., `CopilotStudio-API`), select "Accounts in this organization directory only"
3. Platform: No redirect URI needed for API
4. After registration, record the **Application (client) ID** and **Directory (tenant) ID**
5. Go to **Expose an API**
    - Click "Set" for Application ID URI (e.g., `api://<client-id>`)
    - Add a scope (e.g., `user_impersonation`)
6. Go to **API permissions → Add a permission → APIs my organization uses**
    - Search for `Power Platform API`
    - Under **Delegated Permissions**, select `CopilotStudio.Copilots.Invoke`
    - Add permissions and (optionally) grant admin consent
7. Go to **Certificates & secrets**
    - Create a new client secret (record the value, as it will not be shown again)
8. Use the Application (client) ID, Directory (tenant) ID, and client secret in your Azure Functions configuration.

   > **NOTE:** Ensure the API app registration has the necessary permissions to call Copilot Studio APIs.

> **TIP:** If you do not see `Power Platform API`, follow [these instructions](https://learn.microsoft.com/power-platform/admin/programmability-authentication-v2#step-2-configure-api-permissions) to add it to your tenant.

#### b. Register the Web UI Application

1. In **App registrations**, click **New registration**
2. Name it (e.g., `CopilotStudio-UI`), select "Accounts in this organization directory only"
3. Platform: Add a **Single-page application (SPA)**
    - Redirect URI: `http://localhost:3000` (or wherever you serve the UI)
4. After registration, record the **Application (client) ID** and **Directory (tenant) ID**
5. Go to **Authentication**
    - Ensure "Access tokens (implicit flow)" is enabled
6. Go to **API permissions → Add a permission → My APIs**
    - Select your API app registration
    - Under **Delegated permissions**, select the scope you defined (e.g., `user_impersonation`)
    - Add permissions and (optionally) grant admin consent
7. Use the Application (client) ID and Directory (tenant) ID in your UI's .env file.
8. Update the API_SCOPE in your UI's `.env` file to reference your API app registration's client id.

#### c. Configure API Permissions Between UI and API in Entra ID

1. In the **Web UI app registration**, under **API permissions**, ensure you have a delegated permission for your API app (the scope you exposed, e.g., `user_impersonation`).
2. In the **API app registration**, under **Expose an API**, confirm the scope is present and matches what the UI requests.
3. (Optional) Grant admin consent for the permissions if required for your tenant.

---

## Configuration

### `local.settings.json` Example

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "CopilotStudioClientSettings__EnvironmentId": "Default-your-environment-id",
    "CopilotStudioClientSettings__SchemaName": "your-copilot-schema-name",
    "CopilotStudioClientSettings__TenantId": "your-tenant-id",
    "CopilotStudioClientSettings__UseS2SConnection": "false",
    "CopilotStudioClientSettings__AppClientId": "your-app-client-id",
    "CopilotStudioClientSettings__AppClientSecret": "your-app-client-secret"
  }
}
```

---

## Endpoints

### 1. Health Check
- **GET** `/api/HealthCheck`
- **Authorization**: Anonymous
- **Description**: Simple health check endpoint

### 2. Start Conversation
- **POST** `/api/StartConversation`
- **Authorization**: Bearer token required (Entra ID)
- **Description**: Initiates a new conversation with the Copilot Studio agent

### 3. Send Message
- **POST** `/api/SendMessage`
- **Authorization**: Bearer token required (Entra ID)
- **Description**: Sends a message to the Copilot Studio agent and returns the response

---

## Authentication Flow

- Clients must include a valid Entra ID bearer token in the `Authorization` header for all endpoints except health check.
- The Azure Function validates the token and passes it through to Copilot Studio APIs using the `PassThroughTokenHandler`.
- No tokens are stored; they are used only for the duration of the request.

### Example Request

```http
POST https://your-function-app.azurewebsites.net/api/StartConversation
Authorization: Bearer {your-entra-id-token}
Content-Type: application/json

{}
```

### Acquiring a Token (Client Side)

Use MSAL or a similar library to acquire a token for the Copilot Studio API scope:

```javascript
const tokenRequest = {
    scopes: ["api://your-copilot-app-id/.default"],
    account: account
};
const response = await msalInstance.acquireTokenSilent(tokenRequest);
const accessToken = response.accessToken;
```

---

## Security Considerations

- **Token Validation**: Tokens are checked for presence and format; Copilot Studio performs full validation.
- **No Secret Storage**: No client secrets are stored in the function app for user-interactive login.
- **Logging**: Authentication failures are logged; tokens are cleared from memory after use.
- **Authorization Level**: Endpoints require valid tokens; health check remains anonymous for monitoring.

---

## Running Locally

1. Restore packages:
   ```bash
   dotnet restore
   ```
2. Build the project:
   ```bash
   dotnet build
   ```
3. Start the Azure Functions runtime:
   ```bash
   func start
   ```
   The functions will be available at `http://localhost:7071`

---

## Deployment to Azure

1. Deploy the function app to Azure
2. Configure application settings in the Azure portal (matching your `local.settings.json`)
3. Ensure networking and security settings are appropriate
4. Test the authentication flow end-to-end

---

## Testing

### Bearer Token Validation

```bash
# Test without token (should return 401)
curl -X POST https://your-function-app.azurewebsites.net/api/StartConversation

# Test with invalid token (should return 401)
curl -X POST https://your-function-app.azurewebsites.net/api/StartConversation \
  -H "Authorization: Bearer invalid-token"

# Test with valid token (should return 200)
curl -X POST https://your-function-app.azurewebsites.net/api/StartConversation \
  -H "Authorization: Bearer {valid-token}"
```

### Health Check

```bash
curl https://your-function-app.azurewebsites.net/api/HealthCheck
```

---

## Additional Resources

- [Copilot Studio Documentation](https://learn.microsoft.com/en-us/microsoft-copilot-studio/)
- [Power Platform API Authentication](https://learn.microsoft.com/power-platform/admin/programmability-authentication-v2)
- [Azure Functions Documentation](https://learn.microsoft.com/azure/azure-functions/)
- [MSAL Authentication Library](https://learn.microsoft.com/azure/active-directory/develop/msal-overview)

---

## Future Work Possibilities

- **Stream API Responses to UI**: Implement streaming of responses from the backend Azure Functions to the frontend UI for improved user experience and real-time feedback.
- **Enhanced Error Handling**: Add more granular error messages and user-friendly error reporting in both backend and frontend.
- **Conversation History**: Persist and display conversation history between the user and Copilot Studio agent in the UI.
- **Role-Based Access Control**: Integrate more advanced authorization scenarios, such as role-based access for different endpoints or actions.
- **Automated Testing**: Add end-to-end and integration tests for authentication, API calls, and UI flows.
- **Deployment Automation**: Provide scripts or pipelines for automated deployment to Azure, including infrastructure as code.
- **Scalability Improvements**: Explore options for scaling the function app and optimizing cold start performance.
- **Monitoring & Telemetry**: Integrate Application Insights or similar tools for better observability and diagnostics.
- **UI Enhancements**: Improve the frontend UI for usability, accessibility, and additional features (e.g., loading indicators, error banners).

---

## License

This project is provided as a sample and is not supported for production use. See LICENSE for details.
