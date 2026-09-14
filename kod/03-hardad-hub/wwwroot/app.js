// Klient till den härdade hubben.
//
// Skillnader mot den svaga klienten:
//   - Användaren loggar in först. Servern sätter en cookie. Hubben läser identiteten ur cookien.
//   - SendMessage skickar bara meddelandet. Klienten kan inte längre välja avsändarnamn.
//   - All text renderas med textContent, aldrig innerHTML. Samma data, säker sink.
//   - Automatisk reconnect är påslagen och statusen visas för användaren.
//   - Fel från servern (HubException) visas i listan i stället för att försvinna i konsolen.

const statusEl = document.getElementById("status");
const messagesEl = document.getElementById("messages");
const loginForm = document.getElementById("login-form");
const loginNameEl = document.getElementById("login-name");
const logoutBtn = document.getElementById("logout");
const messageEl = document.getElementById("message");
const groupEl = document.getElementById("group");
const groupMessageEl = document.getElementById("group-message");
const privateToEl = document.getElementById("private-to");
const privateMessageEl = document.getElementById("private-message");

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/chat")
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();

// Nåbar från konsolen för labbens attackövning. Ingen säkerhetsförlust: klientkod är alltid öppen.
window.connection = connection;

connection.on("ReceiveMessage", (username, message) => addMessage(username, message));
connection.on("ReceiveGroupMessage", (groupName, username, message) => addMessage(`[${groupName}] ${username}`, message, "group"));
connection.on("ReceivePrivateMessage", (fromUser, message) => addMessage(`(privat) ${fromUser}`, message, "private"));
connection.on("ReceiveSystem", (message) => addMessage(null, message, "system"));

connection.onreconnecting((err) => setStatus("Återansluter...", false));
connection.onreconnected(() => setStatus("Ansluten", true));
connection.onclose((err) => {
    setStatus("Anslutningen stängdes", false);
    setChatEnabled(false);
    if (err) addMessage(null, `Anslutningen stängdes: ${err.message}`, "error");
});

// Säker rendering: allt som kommer från servern behandlas som text.
function addMessage(username, message, cssClass) {
    const li = document.createElement("li");
    if (cssClass) li.className = cssClass;

    if (username) {
        const strong = document.createElement("strong");
        strong.textContent = username;
        li.appendChild(strong);
        li.appendChild(document.createTextNode(": "));
    }

    li.appendChild(document.createTextNode(message));
    messagesEl.appendChild(li);
    li.scrollIntoView();
}

function setStatus(text, connected) {
    statusEl.textContent = text;
    statusEl.className = connected ? "status status--on" : "status status--off";
}

function setChatEnabled(enabled) {
    document.querySelectorAll("#send-form, #group-form, #private-form")
        .forEach(f => f.querySelectorAll("input, button").forEach(el => el.disabled = !enabled));
    loginNameEl.disabled = enabled;
    loginForm.querySelector("button[type=submit]").disabled = enabled;
    logoutBtn.hidden = !enabled;
}

// Alla anrop går genom samma hjälpfunktion så att HubException-fel visas för användaren.
async function invoke(method, ...args) {
    try {
        await connection.invoke(method, ...args);
        return true;
    } catch (err) {
        addMessage(null, `Fel: ${err.message}`, "error");
        return false;
    }
}

loginForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    const response = await fetch("/dev-login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: loginNameEl.value }),
    });

    if (!response.ok) {
        const body = await response.json().catch(() => ({}));
        addMessage(null, `Inloggning misslyckades: ${body.error ?? response.status}`, "error");
        return;
    }

    const user = await response.json();
    setStatus(`Ansluter som ${user.name}...`, false);

    try {
        await connection.start();
        setStatus(`Ansluten som ${user.name}${user.roles.length ? " (" + user.roles.join(", ") + ")" : ""}`, true);
        setChatEnabled(true);
        messageEl.focus();
    } catch (err) {
        setStatus("Anslutning misslyckades", false);
        addMessage(null, `Anslutning misslyckades: ${err.message}`, "error");
    }
});

logoutBtn.addEventListener("click", async () => {
    await connection.stop();
    await fetch("/logout", { method: "POST" });
    setStatus("Ej inloggad", false);
    setChatEnabled(false);
});

document.getElementById("send-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    if (await invoke("SendMessage", messageEl.value)) messageEl.value = "";
});

document.getElementById("group-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    await invoke("JoinGroup", groupEl.value);
});

document.getElementById("send-group").addEventListener("click", async () => {
    if (await invoke("SendToGroup", groupEl.value, groupMessageEl.value)) groupMessageEl.value = "";
});

document.getElementById("private-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    if (await invoke("SendPrivate", privateToEl.value, privateMessageEl.value)) privateMessageEl.value = "";
});
