// UI logic for Copilot Studio Function UI
// Uses MSAL.js for Entra ID authentication

// Configuration - Loaded from environment variables (see .env file, not committed to git)
const CONFIG = {
    CLIENT_ID: import.meta.env.CLIENT_ID,
    TENANT_ID: import.meta.env.TENANT_ID,
    API_BASE_URL: import.meta.env.API_BASE_URL,
    API_SCOPE: import.meta.env.API_SCOPE
};

const msalConfig = {
    auth: {
        clientId: CONFIG.CLIENT_ID,
        authority: `https://login.microsoftonline.com/${CONFIG.TENANT_ID}`,
        redirectUri: window.location.origin + window.location.pathname
    },
    cache: {
        cacheLocation: "sessionStorage",
        storeAuthStateInCookie: false
    }
};

const msalInstance = new msal.PublicClientApplication(msalConfig);
let account = null;

// DOM helper functions
const show = (element) => element.classList.remove("hidden");
const hide = (element) => element.classList.add("hidden");
const getElement = (id) => document.getElementById(id);

// UI management
function updateUI() {
    const userInfo = getElement("user-info");
    const loginBtn = getElement("login-btn");
    const userName = getElement("user-name");
    const form = getElement("message-form");
    
    if (account) {
        show(userInfo);
        hide(loginBtn);
        userName.textContent = account.username;
        show(form);
    } else {
        hide(userInfo);
        show(loginBtn);
        hide(form);
    }
}

// Authentication functions
async function signIn() {
    try {
        const loginResponse = await msalInstance.loginPopup({ scopes: [CONFIG.API_SCOPE] });
        account = loginResponse.account;
        updateUI();
        clearMessages();
    } catch (err) {
        showError("Login failed: " + err.message);
    }
}

function signOut() {
    msalInstance.logoutPopup();
    account = null;
    updateUI();
    clearMessages();
}

async function getToken() {
    if (!account) throw new Error("Not signed in");
    try {
        const response = await msalInstance.acquireTokenSilent({ 
            scopes: [CONFIG.API_SCOPE], 
            account 
        });
        return response.accessToken;
    } catch (err) {
        // fallback to interactive
        const response = await msalInstance.acquireTokenPopup({ 
            scopes: [CONFIG.API_SCOPE] 
        });
        return response.accessToken;
    }
}

// UI message functions
function showError(msg) {
    const errorDiv = getElement("error");
    errorDiv.textContent = msg;
    hide(getElement("result"));
}

function showResult(data) {
    const resultDiv = getElement("result");
    // Clear previous content
    resultDiv.innerHTML = "";
    // Create a <pre> to show pretty JSON
    const pre = document.createElement("pre");
    pre.style.margin = "0";
    pre.style.background = "#f8fafc";
    pre.style.padding = "1em";
    pre.style.borderRadius = "4px";
    pre.style.overflowX = "auto";
    pre.style.fontSize = "1em";
    pre.textContent = JSON.stringify(data, null, 2);
    resultDiv.appendChild(pre);
    show(resultDiv);
    getElement("error").textContent = "";
}

function clearMessages() {
    getElement("error").textContent = "";
    hide(getElement("result"));
}

// Loading indicator helpers
function showLoading() {
    show(getElement("loading-indicator"));
}
function hideLoading() {
    hide(getElement("loading-indicator"));
}

// Event handlers
getElement("login-btn").onclick = signIn;
getElement("logout-btn").onclick = signOut;

// Initialize app
const accounts = msalInstance.getAllAccounts();
if (accounts.length > 0) {
    account = accounts[0];
}
updateUI();

// Form submission handler
getElement("message-form").onsubmit = async function (e) {
    e.preventDefault();
    clearMessages();
    showLoading();

    const conversationId = getElement("conversationId").value.trim();
    const message = getElement("message").value.trim();

    if (!conversationId) {
        showError("Conversation ID is required");
        hideLoading();
        return;
    }

    if (!message) {
        showError("Message is required");
        hideLoading();
        return;
    }

    try {
        const token = await getToken();
        const url = `${CONFIG.API_BASE_URL}/SendMessage`;
        const body = {
            message,
            onlyReturnMessages: true,
            conversationId
        };

        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Authorization": "Bearer " + token,
                "Content-Type": "application/json"
            },
            body: JSON.stringify(body)
        });

        const data = await response.json();

        if (!response.ok) {
            showError(data.error || data.message || "Error sending message");
        } else {
            showResult(data);
            // Update conversation ID if returned
            if (data.data?.conversationId) {
                getElement("conversationId").value = data.data.conversationId;
            }
        }
    } catch (err) {
        showError("Failed to send message: " + err.message);
    } finally {
        hideLoading();
    }
};
