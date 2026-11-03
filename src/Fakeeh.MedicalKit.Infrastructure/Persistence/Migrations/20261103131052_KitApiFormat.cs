using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fakeeh.MedicalKit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KitApiFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "VitalTypes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "VitalTypes",
                keyColumn: "Code",
                keyValue: "BC",
                column: "Unit",
                value: null);

            migrationBuilder.UpdateData(
                table: "VitalTypes",
                keyColumn: "Code",
                keyValue: "BP",
                column: "Unit",
                value: "mmHg");

            migrationBuilder.UpdateData(
                table: "VitalTypes",
                keyColumn: "Code",
                keyValue: "BT",
                column: "Unit",
                value: "g/dL");

            migrationBuilder.UpdateData(
                table: "VitalTypes",
                keyColumn: "Code",
                keyValue: "GLUCOSE",
                column: "Unit",
                value: "mg/dL");

            migrationBuilder.UpdateData(
                table: "VitalTypes",
                keyColumn: "Code",
                keyValue: "HR",
                column: "Unit",
                value: "bpm");

            migrationBuilder.UpdateData(
                table: "VitalTypes",
                keyColumn: "Code",
                keyValue: "PF",
                column: "Unit",
                value: "L/min");

            migrationBuilder.UpdateData(
                table: "VitalTypes",
                keyColumn: "Code",
                keyValue: "SPO2",
                column: "Unit",
                value: "%");

            migrationBuilder.UpdateData(
                table: "VitalTypes",
                keyColumn: "Code",
                keyValue: "TEMP",
                column: "Unit",
                value: "°C");

            migrationBuilder.UpdateData(
                table: "VitalTypes",
                keyColumn: "Code",
                keyValue: "WEIGHT",
                column: "Unit",
                value: "kg");

            migrationBuilder.CreateIndex(
                name: "IX_Readings_NationalId_VitalTypeCode_EffectiveAt",
                table: "Readings",
                columns: new[] { "NationalId", "VitalTypeCode", "EffectiveAt" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Readings_NationalId_VitalTypeCode_EffectiveAt",
                table: "Readings");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "VitalTypes");
        }
    }
}
