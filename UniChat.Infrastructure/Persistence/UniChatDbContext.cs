using Microsoft.EntityFrameworkCore;
using UniChat.Domain.Entities;

namespace UniChat.Infrastructure.Persistence;

public class UniChatDbContext : DbContext
{
    public UniChatDbContext(DbContextOptions<UniChatDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(x => x.UserName).IsUnique();

        modelBuilder.Entity<Conversation>()
            .HasMany(x => x.Members)
            .WithOne()
            .HasForeignKey(x => x.ConversationId);

        modelBuilder.Entity<Message>()
            .HasMany(x => x.Attachments)
            .WithOne()
            .HasForeignKey(x => x.MessageId);

        // быстрые выборки истории сообщений
        modelBuilder.Entity<Message>()
            .HasIndex(x => new { x.ConversationId, x.CreatedAt });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.TokenHash).IsUnique();

            b.Property(x => x.TokenHash).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.ExpiresAt).IsRequired();
        });
    }
}
