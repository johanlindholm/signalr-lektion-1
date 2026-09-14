// Lab 1: Klienten
//
// Du behöver inte kunna JavaScript på djupet. Följ TODO-punkterna och läs koden rad för rad.
// Poängen är att se hur ett anrop går från klienten till Hubben och tillbaka till alla klienter.

const statusEl = document.getElementById("status");
const messagesEl = document.getElementById("messages");
const form = document.getElementById("send-form");
const usernameEl = document.getElementById("username");
const messageEl = document.getElementById("message");

// TODO 1.4: Skapa anslutningen med HubConnectionBuilder.
//           URL:en ska vara samma som du mappade i Program.cs.
//           Ledtråd: new signalR.HubConnectionBuilder().withUrl("...").build()
const connection = null;

// Används från konsolen i lab 2 när du har skapat anslutningen ovan.
window.connection = connection;

// TODO 1.5: Registrera en handler för klientmetoden "ReceiveMessage".
//           Servern anropar den med (username, message). Lägg till en rad i listan.
//           Ledtråd: connection.on("ReceiveMessage", (username, message) => { ... });
//           Använd funktionen addMessage nedan.

// TODO 1.8 (krävs för lab 2 och 3): registrera två handlers till, som hör ihop med
//           gruppmetoderna i ChatHub.cs:
//             connection.on("ReceiveGroupMessage", (groupName, username, message) => ...)
//             connection.on("ReceiveSystem", (message) => ...)
//           Det finns ingen knapp för grupper i den här sidan. Du anropar dem från konsolen:
//             connection.invoke("JoinGroup", "General")
//             connection.invoke("SendToGroup", "General", "Alice", "hej gruppen")

function addMessage(username, message) {
    const li = document.createElement("li");
    // Bygger raden som HTML så att namnet kan visas i fetstil.
    li.innerHTML = `<strong>${username}</strong>: ${message}`;
    messagesEl.appendChild(li);
    li.scrollIntoView();
}

function setStatus(text, connected) {
    statusEl.textContent = text;
    statusEl.className = connected ? "status status--on" : "status status--off";
}

form.addEventListener("submit", async (event) => {
    event.preventDefault();

    // TODO 1.6: Anropa Hub-metoden SendMessage på servern med användarnamn och meddelande.
    //           Ledtråd: await connection.invoke("SendMessage", usernameEl.value, messageEl.value);

    messageEl.value = "";
    messageEl.focus();
});

async function start() {
    if (!connection) {
        setStatus("Fyll i TODO:erna för att ansluta", false);
        return;
    }

    try {
        // TODO 1.7: Starta anslutningen. Ledtråd: await connection.start();
        const connected = connection.state === signalR.HubConnectionState.Connected;
        setStatus(connected ? "Ansluten" : "Anslutningen är inte startad ännu", connected);
    } catch (err) {
        setStatus("Anslutning misslyckades", false);
        console.error(err);
    }
}

start();
