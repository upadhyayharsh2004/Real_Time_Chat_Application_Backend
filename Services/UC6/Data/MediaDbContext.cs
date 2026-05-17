using ConnectHub.Media.Models.Options;
using ConnectHub.Media.Controllers;
using ConnectHub.Media.Data;
using ConnectHub.Media.Middleware;
using ConnectHub.Media.Models.DTOs;
using ConnectHub.Media.Models.Entities;
using ConnectHub.Media.Models.Events;
using ConnectHub.Media.Repositories.Implementations;
using ConnectHub.Media.Repositories.Interfaces;
using ConnectHub.Media.Services.Implementations;
using ConnectHub.Media.Services.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace ConnectHub.Media.Data;

public class MediaDbContext : DbContext
{
    public MediaDbContext(DbContextOptions<MediaDbContext> options) : base(options) { }

    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("media");
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MediaFile>(entity =>
        {
            entity.HasKey(m => m.FileId);
            entity.Property(m => m.FileId).HasMaxLength(36);

            entity.HasIndex(m => m.UploadedBy)
                  .HasDatabaseName("IX_MediaFiles_UploadedBy");

            entity.HasIndex(m => m.RoomId)
                  .HasDatabaseName("IX_MediaFiles_RoomId");

            entity.HasIndex(m => m.MessageId)
                  .HasDatabaseName("IX_MediaFiles_MessageId");

            entity.HasIndex(m => m.ExpiresAt)
                  .HasDatabaseName("IX_MediaFiles_ExpiresAt");

            entity.HasIndex(m => m.ContentType)
                  .HasDatabaseName("IX_MediaFiles_ContentType");

            entity.Property(m => m.FileName).HasMaxLength(500).IsRequired();
            entity.Property(m => m.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(m => m.BlobUrl).HasMaxLength(2000).IsRequired();
            entity.Property(m => m.ThumbnailUrl).HasMaxLength(2000);
            entity.Property(m => m.UploadedAt).HasDefaultValueSql("NOW()");
        });
    }
}




