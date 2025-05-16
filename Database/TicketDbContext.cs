using Microsoft.EntityFrameworkCore;

public class TicketDbContext : DbContext
{
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<ZLGMember> ZLGMembers { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<UserTicket> UserTickets { get; set; }

    private readonly string _connectionString;
    private readonly string _provider;

    public TicketDbContext(string connectionString, string provider)
    {
        _connectionString = connectionString;
        _provider = provider;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_provider == "Postgres")
            optionsBuilder.UseNpgsql(_connectionString);
        else
            throw new Exception("Unsupported database provider.");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ✅ Explicitly tell EF Core that these tables already exist
        modelBuilder.Entity<Ticket>().ToTable("Tickets");
        modelBuilder.Entity<Message>().ToTable("Messages");
        modelBuilder.Entity<ZLGMember>().ToTable("ZLGMembers");
        modelBuilder.Entity<UserProfile>().ToTable("UserProfiles");
        modelBuilder.Entity<UserTicket>().ToTable("UserTickets");

        // ✅ Define composite primary key for UserTickets
        modelBuilder.Entity<UserTicket>()
            .HasKey(ut => new { ut.UserProfileId, ut.TicketId });
    }

}

