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
    public DbSet<MessageHide> MessageHides => Set<MessageHide>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(x => x.UserName).IsUnique();
        modelBuilder.Entity<User>().HasIndex(x => x.Email).IsUnique();

        modelBuilder.Entity<User>()
            .HasOne<Attachment>()
            .WithMany()
            .HasForeignKey(x => x.AvatarAttachmentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Conversation>()
            .HasMany(x => x.Members)
            .WithOne()
            .HasForeignKey(x => x.ConversationId);

        modelBuilder.Entity<Message>()
            .HasMany(x => x.Attachments)
            .WithOne()
            .HasForeignKey(x => x.MessageId);

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

        modelBuilder.Entity<MessageHide>()
            .HasIndex(x => new { x.UserId, x.MessageId })
            .IsUnique();

        modelBuilder.Entity<MessageHide>()
            .HasIndex(x => x.MessageId);
    }
}
