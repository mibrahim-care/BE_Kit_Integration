using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fakeeh.MedicalKit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VitalTypes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LoincCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ObservationModel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitalTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Readings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PatientId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VitalTypeCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FieldKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UcumCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Interpretation = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PerformerRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeviceRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Readings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Readings_VitalTypes_VitalTypeCode",
                        column: x => x.VitalTypeCode,
                        principalTable: "VitalTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VitalTypeFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    VitalTypeCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UcumCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LoincCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Required = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitalTypeFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VitalTypeFields_VitalTypes_VitalTypeCode",
                        column: x => x.VitalTypeCode,
                        principalTable: "VitalTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadingComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FieldKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LoincCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UcumCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReadingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingComponents_Readings_ReadingId",
                        column: x => x.ReadingId,
                        principalTable: "Readings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "VitalTypes",
                columns: new[] { "Code", "Category", "IsActive", "LoincCode", "Name", "ObservationModel" },
                values: new object[,]
                {
                    { "BC", "vital-signs", true, null, "Body Composition", "PerField" },
                    { "BP", "vital-signs", true, "85354-9", "Blood Pressure", "Components" },
                    { "BT", "laboratory", true, null, "Blood Test", "PerField" },
                    { "GLUCOSE", "vital-signs", true, null, "Blood Glucose", "SingleValue" },
                    { "HR", "vital-signs", true, "8867-4", "Heart Rate", "SingleValue" },
                    { "PF", "vital-signs", true, null, "Peak Flow", "Components" },
                    { "SPO2", "vital-signs", true, "59408-5", "Oxygen Saturation (SpO2)", "SingleValue" },
                    { "TEMP", "vital-signs", true, "8310-5", "Temperature", "SingleValue" },
                    { "WEIGHT", "vital-signs", true, "29463-7", "Weight", "SingleValue" }
                });

            migrationBuilder.InsertData(
                table: "VitalTypeFields",
                columns: new[] { "Id", "Key", "Label", "LoincCode", "Required", "SortOrder", "UcumCode", "Unit", "VitalTypeCode" },
                values: new object[,]
                {
                    { 1, "systolic", "Systolic", "8480-6", true, 1, "mm[Hg]", "mmHg", "BP" },
                    { 2, "diastolic", "Diastolic", "8462-4", true, 2, "mm[Hg]", "mmHg", "BP" },
                    { 3, "Value", "Heart Rate", "8867-4", true, 1, "/min", "bpm", "HR" },
                    { 4, "Value", "Temperature", "8310-5", true, 1, "Cel", "°C", "TEMP" },
                    { 5, "Value", "SpO2", "59408-5", true, 1, "%", "%", "SPO2" },
                    { 6, "Value", "Weight", "29463-7", true, 1, "kg", "kg", "WEIGHT" },
                    { 7, "Value", "Blood Glucose", null, true, 1, "mg/dL", "mg/dL", "GLUCOSE" },
                    { 8, "per_min", "PEF", null, true, 1, "L/min", "L/min", "PF" },
                    { 9, "fev1_l", "FEV1", null, false, 2, "L", "L", "PF" },
                    { 10, "hb_gdL", "Hemoglobin", "718-7", true, 1, "g/dL", "g/dL", "BT" },
                    { 11, "glucometer_mgdL", "Glucometer", null, false, 2, "mg/dL", "mg/dL", "BT" },
                    { 12, "lactate_mmoll", "Lactate", null, false, 3, "mmol/L", "mmol/L", "BT" },
                    { 13, "ketone_mmoll", "Ketone", null, false, 4, "mmol/L", "mmol/L", "BT" },
                    { 14, "na_mmoll", "Sodium", null, false, 5, "mmol/L", "mmol/L", "BT" },
                    { 15, "k_mmoll", "Potassium", null, false, 6, "mmol/L", "mmol/L", "BT" },
                    { 16, "bmi", "BMI", "39156-5", false, 1, "kg/m2", "kg/m²", "BC" },
                    { 17, "body_fat", "Body fat", null, false, 2, "kg", "kg", "BC" },
                    { 18, "body_fat_percentage", "Body fat percentage", null, false, 3, "%", "%", "BC" },
                    { 19, "body_water_percentage", "Body water percentage", null, false, 4, "%", "%", "BC" },
                    { 20, "visceral_fat", "Visceral fat level", null, false, 5, "{level}", "level", "BC" },
                    { 21, "skeletal_muscle", "Skeletal muscle mass", null, false, 6, "kg", "kg", "BC" },
                    { 22, "resting_metabolism", "Resting metabolism", null, false, 7, "kcal", "kcal", "BC" },
                    { 23, "basal_metabolic_rate", "Basal metabolic rate", null, false, 8, "kcal/d", "kcal/day", "BC" },
                    { 24, "protein_levels", "Protein levels", null, false, 9, "kg", "kg", "BC" },
                    { 25, "mineral", "Mineral content", null, false, 10, "kg", "kg", "BC" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingComponents_ReadingId",
                table: "ReadingComponents",
                column: "ReadingId");

            migrationBuilder.CreateIndex(
                name: "IX_Readings_ExternalId",
                table: "Readings",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Readings_PatientId_EffectiveAt",
                table: "Readings",
                columns: new[] { "PatientId", "EffectiveAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Readings_PatientId_VitalTypeCode",
                table: "Readings",
                columns: new[] { "PatientId", "VitalTypeCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Readings_VitalTypeCode",
                table: "Readings",
                column: "VitalTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_VitalTypeFields_VitalTypeCode_Key",
                table: "VitalTypeFields",
                columns: new[] { "VitalTypeCode", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReadingComponents");

            migrationBuilder.DropTable(
                name: "VitalTypeFields");

            migrationBuilder.DropTable(
                name: "Readings");

            migrationBuilder.DropTable(
                name: "VitalTypes");
        }
    }
}
