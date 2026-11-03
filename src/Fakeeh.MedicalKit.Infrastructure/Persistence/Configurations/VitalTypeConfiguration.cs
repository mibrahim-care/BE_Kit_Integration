using Fakeeh.MedicalKit.Domain.Entities;
using Fakeeh.MedicalKit.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fakeeh.MedicalKit.Infrastructure.Persistence.Configurations;

internal sealed class VitalTypeConfiguration : IEntityTypeConfiguration<VitalType>
{
    public void Configure(EntityTypeBuilder<VitalType> builder)
    {
        builder.ToTable("VitalTypes");
        builder.HasKey(t => t.Code);

        builder.Property(t => t.Code).HasMaxLength(20);
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Category).HasMaxLength(30).IsRequired();
        builder.Property(t => t.LoincCode).HasMaxLength(20);
        builder.Property(t => t.ObservationModel).HasConversion<string>().HasMaxLength(20);

        builder.HasMany(t => t.Fields)
            .WithOne()
            .HasForeignKey(f => f.VitalTypeCode)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(VitalTypeSeed.Types);
    }
}

internal sealed class VitalTypeFieldConfiguration : IEntityTypeConfiguration<VitalTypeField>
{
    public void Configure(EntityTypeBuilder<VitalTypeField> builder)
    {
        builder.ToTable("VitalTypeFields");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.VitalTypeCode).HasMaxLength(20);
        builder.Property(f => f.Key).HasMaxLength(50).IsRequired();
        builder.Property(f => f.Label).HasMaxLength(100).IsRequired();
        builder.Property(f => f.Unit).HasMaxLength(20).IsRequired();
        builder.Property(f => f.UcumCode).HasMaxLength(20).IsRequired();
        builder.Property(f => f.LoincCode).HasMaxLength(20);

        builder.HasIndex(f => new { f.VitalTypeCode, f.Key }).IsUnique();

        builder.HasData(VitalTypeSeed.Fields);
    }
}
