using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Models;

namespace RotationDating.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<ParticipantApplication> Applications => Set<ParticipantApplication>();
    public DbSet<ParticipantVote> Votes => Set<ParticipantVote>();
    public DbSet<QuestionCard> QuestionCards => Set<QuestionCard>();
    public DbSet<ParticipantAiMatch> AiMatches => Set<ParticipantAiMatch>();
    public DbSet<EventCandidateDate> EventCandidateDates => Set<EventCandidateDate>();
    public DbSet<ApplicationAvailability> ApplicationAvailabilities => Set<ApplicationAvailability>();
    public DbSet<Survey> Surveys => Set<Survey>();
    public DbSet<SurveyQuestion> SurveyQuestions => Set<SurveyQuestion>();
    public DbSet<SurveyOption> SurveyOptions => Set<SurveyOption>();
    public DbSet<SurveyResponse> SurveyResponses => Set<SurveyResponse>();
    public DbSet<SurveyAnswer> SurveyAnswers => Set<SurveyAnswer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(entity =>
        {
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Location).HasMaxLength(500);
            entity.HasIndex(e => e.EventDate);
        });

        modelBuilder.Entity<EventCandidateDate>(entity =>
        {
            entity.ToTable("EventCandidateDates");
            entity.HasIndex(c => c.EventId);
            entity.HasOne(c => c.Event)
                .WithMany(e => e.CandidateDates)
                .HasForeignKey(c => c.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApplicationAvailability>(entity =>
        {
            entity.ToTable("ApplicationAvailabilities");
            entity.HasIndex(a => a.ApplicationId);
            entity.HasOne(a => a.Application)
                .WithMany(p => p.Availabilities)
                .HasForeignKey(a => a.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Phone).HasMaxLength(30).IsRequired();
            entity.Property(p => p.Occupation).HasMaxLength(100);
            entity.Property(p => p.Memo).HasMaxLength(1000);
            entity.HasOne(p => p.Event)
                .WithMany(e => e.Participants)
                .HasForeignKey(p => p.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParticipantApplication>(entity =>
        {
            entity.ToTable("ParticipantApplications");
            entity.Property(a => a.Name).HasMaxLength(100).IsRequired();
            entity.Property(a => a.BirthDate).HasMaxLength(30);
            entity.Property(a => a.Gender).HasMaxLength(20);
            entity.Property(a => a.Phone).HasMaxLength(30);
            entity.Property(a => a.Residence).HasMaxLength(100);
            entity.Property(a => a.Workplace).HasMaxLength(100);
            entity.Property(a => a.PreferredAgeRange).HasMaxLength(50);
            entity.Property(a => a.Interests).HasMaxLength(500);
            entity.Property(a => a.Memo).HasMaxLength(200);
            entity.HasOne(a => a.Event)
                .WithMany(e => e.Applications)
                .HasForeignKey(a => a.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuestionCard>(entity =>
        {
            entity.ToTable("QuestionCards");
            entity.Property(q => q.Text).HasMaxLength(500).IsRequired();
            entity.HasIndex(q => q.SortOrder);
        });

        modelBuilder.Entity<ParticipantAiMatch>(entity =>
        {
            entity.ToTable("ParticipantAiMatches");
            entity.Property(m => m.Reason).HasMaxLength(1000);
            entity.HasIndex(m => new { m.EventId, m.VoteType });
            entity.HasOne(m => m.Male)
                .WithMany()
                .HasForeignKey(m => m.MaleApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(m => m.Female)
                .WithMany()
                .HasForeignKey(m => m.FemaleApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParticipantVote>(entity =>
        {
            entity.ToTable("ParticipantVotes");
            entity.HasIndex(v => new { v.VoterApplicationId, v.TargetApplicationId, v.VoteType }).IsUnique();
            entity.HasOne(v => v.Event)
                .WithMany()
                .HasForeignKey(v => v.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(v => v.Voter)
                .WithMany()
                .HasForeignKey(v => v.VoterApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(v => v.Target)
                .WithMany()
                .HasForeignKey(v => v.TargetApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Survey>(entity =>
        {
            entity.Property(s => s.Title).HasMaxLength(200).IsRequired();
            entity.Property(s => s.Slug).HasMaxLength(32).IsRequired();
            entity.Property(s => s.WelcomeTitle).HasMaxLength(200).IsRequired();
            entity.Property(s => s.WelcomeContent).HasMaxLength(4000).IsRequired();
            entity.HasIndex(s => s.Slug).IsUnique();
        });

        modelBuilder.Entity<SurveyQuestion>(entity =>
        {
            entity.Property(q => q.Title).HasMaxLength(200).IsRequired();
            entity.Property(q => q.Content).HasMaxLength(2000).IsRequired();
            entity.HasIndex(q => new { q.SurveyId, q.SortOrder });
            entity.HasOne(q => q.Survey)
                .WithMany(s => s.Questions)
                .HasForeignKey(q => q.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SurveyOption>(entity =>
        {
            entity.Property(o => o.Text).HasMaxLength(500).IsRequired();
            entity.HasIndex(o => new { o.QuestionId, o.SortOrder });
            entity.HasOne(o => o.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(o => o.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SurveyResponse>(entity =>
        {
            entity.Property(r => r.PhoneNumber).HasMaxLength(30).IsRequired();
            entity.Property(r => r.NormalizedPhone).HasMaxLength(20).IsRequired();
            entity.HasIndex(r => r.SurveyId);
            entity.HasIndex(r => new { r.SurveyId, r.NormalizedPhone }).IsUnique();
            entity.HasOne(r => r.Survey)
                .WithMany(s => s.Responses)
                .HasForeignKey(r => r.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SurveyAnswer>(entity =>
        {
            entity.Property(a => a.TextAnswer).HasMaxLength(4000);
            entity.HasIndex(a => new { a.ResponseId, a.QuestionId });
            entity.HasOne(a => a.Response)
                .WithMany(r => r.Answers)
                .HasForeignKey(a => a.ResponseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.Question)
                .WithMany()
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.Option)
                .WithMany()
                .HasForeignKey(a => a.OptionId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
