using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UniChat.Infrastructure.Persistence;

public sealed class UniChatDbContextFactory : IDesignTimeDbContextFactory<UniChatDbContext>
{
    public UniChatDbContext CreateDbContext(string[] args)
    {
        // 1) Сначала пробуем переменную окружения (удобно для CI/девопса)
        var cs = Environment.GetEnvironmentVariable("UNICHAT_CONNECTION");

        // 2) Если не задана — берём дефолт (мой контейнер)
        cs ??= "Host=127.0.0.1;Port=5432;Database=unichat;Username=unichat;Password=unichat_pwd";

        var options = new DbContextOptionsBuilder<UniChatDbContext>()
            .UseNpgsql(cs)
            .Options;

        return new UniChatDbContext(options);
    }
}
