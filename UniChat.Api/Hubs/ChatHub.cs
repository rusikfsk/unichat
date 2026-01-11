using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using UniChat.Api.Auth;
using UniChat.Infrastructure.Persistence;

namespace UniChat.Api.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<Guid, int> OnlineCounts = new();
    private static readonly ConcurrentDictionary<Guid, DateTimeOffset> LastSeen = new();

    private readonly UniChatDbContext _db;

    public ChatHub(UniChatDbContext db)
    {
        _db = db;
    }

    private async Task<Guid> RequireMembership(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var convId) || convId == Guid.Empty)
            throw new HubException("invalid_conversation_id");

        var me = Context.User?.GetUserId()
                 ?? throw new HubException("unauthorized");

        var isMember = await _db.Memberships
            .AnyAsync(m => m.ConversationId == convId && m.UserId == me);

        if (!isMember)
            throw new HubException("forbidden");

        return convId;
    }

    public override async Task OnConnectedAsync()
    {
        var me = Context.User!.GetUserId();

        OnlineCounts.AddOrUpdate(me, 1, (_, v) => v + 1);
        LastSeen.TryRemove(me, out _);

        var onlineUserIds = OnlineCounts.Keys.ToList();

        await Clients.Caller.SendAsync("presence_snapshot", new
        {
            onlineUserIds
        });

        if (OnlineCounts[me] == 1)
        {
            await Clients.Others.SendAsync("presence_online", new
            {
                userId = me
            });
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var me = Context.User!.GetUserId();

        if (OnlineCounts.TryGetValue(me, out var count))
        {
            var next = Math.Max(0, count - 1);
            if (next == 0)
            {
                OnlineCounts.TryRemove(me, out _);
                var at = DateTimeOffset.UtcNow;
                LastSeen[me] = at;

                await Clients.Others.SendAsync("presence_offline", new
                {
                    userId = me,
                    lastSeenAt = at
                });
            }
            else
            {
                OnlineCounts[me] = next;
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(string conversationId)
    {
        var convId = await RequireMembership(conversationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, convId.ToString());
    }

    public async Task LeaveConversation(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var convId) || convId == Guid.Empty)
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, convId.ToString());
    }

    public async Task Typing(string conversationId)
    {
        var convId = await RequireMembership(conversationId);
        var me = Context.User!.GetUserId();

        await Clients.OthersInGroup(convId.ToString()).SendAsync("typing", new
        {
            conversationId = convId,
            userId = me
        });
    }

    public async Task StopTyping(string conversationId)
    {
        var convId = await RequireMembership(conversationId);
        var me = Context.User!.GetUserId();

        await Clients.OthersInGroup(convId.ToString()).SendAsync("stop_typing", new
        {
            conversationId = convId,
            userId = me
        });
    }
}
