using Fakeeh.MedicalKit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fakeeh.MedicalKit.Infrastructure.Persistence.Configurations;

internal sealed class ReadingConfiguration : IEntityTypeConfiguration<Reading>
{
    public void Configure(EntityTypeBuilder<Reading> builder)
    {
        builder.ToTable("Readings");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.ExternalId).HasMaxLength(200).IsRequired();
        builder.Property(r => r.PatientId).HasMaxLength(64).IsRequired();
        builder.Property(r => r.NationalId).HasMaxLength(20);
        builder.Property(r => r.VitalTypeCode).HasMaxLength(20).IsRequired();
        builder.Property(r => r.FieldKey).HasMaxLength(50);
        builder.Property(r => r.Category).HasMaxLength(30).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Value).HasPrecision(18, 4);
        builder.Property(r => r.Unit).HasMaxLength(20);
        builder.Property(r => r.UcumCode).HasMaxLength(20);
        builder.Property(r => r.Interpretation).HasMaxLength(10);
        builder.Property(r => r.Note).HasMaxLength(2000);
        builder.Property(r => r.Address).HasMaxLength(500);
        builder.Property(r => r.PerformerRef).HasMaxLength(200);
        builder.Property(r => r.DeviceRef).HasMaxLength(200);

        builder.HasOne<VitalType>()
            .WithMany()
            .HasForeignKey(r => r.VitalTypeCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.ExternalId).IsUnique();
        builder.HasIndex(r => new { r.PatientId, r.EffectiveAt }).IsDescending(false, true);
        builder.HasIndex(r => new { r.PatientId, r.VitalTypeCode });

        builder.OwnsMany(r => r.Components, component =>
        {
            component.ToTable("ReadingComponents");
            component.WithOwner().HasForeignKey("ReadingId");
            component.Property<int>("Id");
            component.HasKey("Id");

            component.Property(c => c.FieldKey).HasMaxLength(50).IsRequired();
            component.Property(c => c.LoincCode).HasMaxLength(20);
            component.Property(c => c.Value).HasPrecision(18, 4);
            component.Property(c => c.Unit).HasMaxLength(20).IsRequired();
            component.Property(c => c.UcumCode).HasMaxLength(20).IsRequired();
        });
    }
}
