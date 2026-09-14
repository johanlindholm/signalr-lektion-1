// Starkt typad klient. Servern kan bara anropa de metoder som finns här,
// och ett felstavat metodnamn blir ett kompileringsfel i stället för ett tyst fel i runtime.
public interface IChatClient
{
    Task ReceiveMessage(string username, string message);
    Task ReceiveGroupMessage(string groupName, string username, string message);
    Task ReceivePrivateMessage(string fromUser, string message);
    Task ReceiveSystem(string message);
}
