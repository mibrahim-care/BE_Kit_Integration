using Fakeeh.MedicalKit.Domain.Entities;
using Fakeeh.MedicalKit.Domain.Enums;

namespace Fakeeh.MedicalKit.Infrastructure.Persistence.Seed;

internal static class VitalTypeSeed
{
    private const string Vital = VitalType.VitalSignsCategory;
    private const string Lab = VitalType.LaboratoryCategory;

    public static readonly VitalType[] Types =
    [
        Type("BP", "Blood Pressure", Vital, "85354-9", ObservationModel.Components, "mmHg"),
        Type("HR", "Heart Rate", Vital, "8867-4", ObservationModel.SingleValue, "bpm"),
        Type("TEMP", "Temperature", Vital, "8310-5", ObservationModel.SingleValue, "°C"),
        Type("SPO2", "Oxygen Saturation (SpO2)", Vital, "59408-5", ObservationModel.SingleValue, "%"),
        Type("WEIGHT", "Weight", Vital, "29463-7", ObservationModel.SingleValue, "kg"),
        Type("GLUCOSE", "Blood Glucose", Vital, null, ObservationModel.SingleValue, "mg/dL"),
        Type("PF", "Peak Flow", Vital, null, ObservationModel.Components, "L/min"),
        Type("BT", "Blood Test", Lab, null, ObservationModel.PerField, "g/dL"),
        Type("BC", "Body Composition", Vital, null, ObservationModel.PerField, null)
    ];

    public static readonly VitalTypeField[] Fields =
    [
        Field(1, "BP", "systolic", "Systolic", "mmHg", "mm[Hg]", "8480-6", true, 1),
        Field(2, "BP", "diastolic", "Diastolic", "mmHg", "mm[Hg]", "8462-4", true, 2),

        Field(3, "HR", "Value", "Heart Rate", "bpm", "/min", "8867-4", true, 1),
        Field(4, "TEMP", "Value", "Temperature", "°C", "Cel", "8310-5", true, 1),
        Field(5, "SPO2", "Value", "SpO2", "%", "%", "59408-5", true, 1),
        Field(6, "WEIGHT", "Value", "Weight", "kg", "kg", "29463-7", true, 1),
        Field(7, "GLUCOSE", "Value", "Blood Glucose", "mg/dL", "mg/dL", null, true, 1),

        Field(8, "PF", "per_min", "PEF", "L/min", "L/min", null, true, 1),
        Field(9, "PF", "fev1_l", "FEV1", "L", "L", null, false, 2),

        Field(10, "BT", "hb_gdL", "Hemoglobin", "g/dL", "g/dL", "718-7", true, 1),
        Field(11, "BT", "glucometer_mgdL", "Glucometer", "mg/dL", "mg/dL", null, false, 2),
        Field(12, "BT", "lactate_mmoll", "Lactate", "mmol/L", "mmol/L", null, false, 3),
        Field(13, "BT", "ketone_mmoll", "Ketone", "mmol/L", "mmol/L", null, false, 4),
        Field(14, "BT", "na_mmoll", "Sodium", "mmol/L", "mmol/L", null, false, 5),
        Field(15, "BT", "k_mmoll", "Potassium", "mmol/L", "mmol/L", null, false, 6),

        Field(16, "BC", "bmi", "BMI", "kg/m²", "kg/m2", "39156-5", false, 1),
        Field(17, "BC", "body_fat", "Body fat", "kg", "kg", null, false, 2),
        Field(18, "BC", "body_fat_percentage", "Body fat percentage", "%", "%", null, false, 3),
        Field(19, "BC", "body_water_percentage", "Body water percentage", "%", "%", null, false, 4),
        Field(20, "BC", "visceral_fat", "Visceral fat level", "level", "{level}", null, false, 5),
        Field(21, "BC", "skeletal_muscle", "Skeletal muscle mass", "kg", "kg", null, false, 6),
        Field(22, "BC", "resting_metabolism", "Resting metabolism", "kcal", "kcal", null, false, 7),
        Field(23, "BC", "basal_metabolic_rate", "Basal metabolic rate", "kcal/day", "kcal/d", null, false, 8),
        Field(24, "BC", "protein_levels", "Protein levels", "kg", "kg", null, false, 9),
        Field(25, "BC", "mineral", "Mineral content", "kg", "kg", null, false, 10)
    ];

    private static VitalType Type(string code, string name, string category, string? loinc, ObservationModel model, string? unit) =>
        new()
        {
            Code = code,
            Name = name,
            IsActive = true,
            Category = category,
            LoincCode = loinc,
            ObservationModel = model,
            Unit = unit
        };

    private static VitalTypeField Field(
        int id, string type, string key, string label, string unit, string ucum, string? loinc, bool required, int order) =>
        new()
        {
            Id = id,
            VitalTypeCode = type,
            Key = key,
            Label = label,
            Unit = unit,
            UcumCode = ucum,
            LoincCode = loinc,
            Required = required,
            SortOrder = order
        };
}
