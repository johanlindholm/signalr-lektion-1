// Klient till den svaga hubben. Fungerar, men läs den med samma frågor som ChatHub.cs.

const statusEl = document.getElementById("status");
const messagesEl = document.getElementById("messages");
const usernameEl = document.getElementById("username");
const messageEl = document.getElementById("message");
const groupEl = document.getElementById("group");
const groupMessageEl = document.getElementById("group-message");

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/chat")
    .build();

// Gör anslutningen nåbar från webbläsarens konsol (används i attackövningen).
// Observera: det här ändrar inget säkerhetsmässigt. En användare kommer alltid åt
// sin egen klientkod och kan skicka vad som helst till servern ändå.
window.connection = connection;

connection.on("ReceiveMessage", (username, message) => {
    addMessage(`<strong>${username}</strong>: ${message}`);
});

connection.on("ReceiveGroupMessage", (groupName, username, message) => {
    addMessage(`<strong>[${groupName}] ${username}</strong>: ${message}`, "group");
});

connection.on("ReceiveSystem", (message) => {
    addMessage(message, "system");
});

connection.onclose((err) => {
    setStatus("Anslutningen stängdes", false);
    if (err) addMessage(`Fel: ${err.message}`, "error");
});

function addMessage(html, cssClass) {
    const li = document.createElement("li");
    if (cssClass) li.className = cssClass;
    // Bygger raden som HTML så att namn kan visas i fetstil.
    li.innerHTML = html;
    messagesEl.appendChild(li);
    li.scrollIntoView();
}

function setStatus(text, connected) {
    statusEl.textContent = text;
    statusEl.className = connected ? "status status--on" : "status status--off";
}

document.getElementById("send-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
        await connection.invoke("SendMessage", usernameEl.value, messageEl.value);
        messageEl.value = "";
    } catch (err) {
        addMessage(`Fel: ${err.message}`, "error");
    }
});

document.getElementById("group-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
        await connection.invoke("JoinGroup", groupEl.value);
    } catch (err) {
        addMessage(`Fel: ${err.message}`, "error");
    }
});

document.getElementById("send-group").addEventListener("click", async () => {
    try {
        await connection.invoke("SendToGroup", groupEl.value, usernameEl.value, groupMessageEl.value);
        groupMessageEl.value = "";
    } catch (err) {
        addMessage(`Fel: ${err.message}`, "error");
    }
});

async function start() {
    try {
        await connection.start();
        setStatus("Ansluten", true);
    } catch (err) {
        setStatus("Anslutning misslyckades", false);
        console.error(err);
    }
}

start();
