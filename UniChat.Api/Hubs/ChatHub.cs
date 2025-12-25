using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace UniChat.Api.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    // Клиент должен вызывать после выбора чата/открытия диалога
    public Task JoinConversation(string conversationId)
        => Groups.AddToGroupAsync(Context.ConnectionId, conversationId);

    public Task LeaveConversation(string conversationId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);

    // typing: клиент шлёт, когда пользователь печатает
    // Мы ретранслируем в группу разговора
    public Task Typing(string conversationId)
        => Clients.OthersInGroup(conversationId).SendAsync("typing", new
        {
            conversationId,
            userId = Context.UserIdentifier // если настроен NameIdentifier (см. Program.cs)
        });

    // stop_typing: клиент шлёт, когда перестал печатать
    public Task StopTyping(string conversationId)
        => Clients.OthersInGroup(conversationId).SendAsync("stop_typing", new
        {
            conversationId,
            userId = Context.UserIdentifier
        });
}
