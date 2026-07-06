-- Data Migration Script: Update AmountDue for Imported Prescriptions
-- This script updates AmountDue for DispensedPrescription records that were 
-- imported with AmountDue = 0. It calculates the correct amount based on 
-- the medication price and quantity.

-- Update AmountDue for DispensedPrescription records that have zero amount
-- and calculate based on medication price and prescription line quantity
UPDATE dp
SET dp.AmountDue = CAST((m.Price * pl.Quantity) AS decimal(18,2))
FROM DispensedPrescriptions dp
INNER JOIN PrescriptionLines pl ON dp.PrescriptionLineId = pl.PrescriptionLineId
INNER JOIN Medications m ON pl.MedicationId = m.MedicationId
WHERE dp.AmountDue = 0
  AND m.Price IS NOT NULL
  AND m.Price > 0
  AND pl.Quantity > 0;

-- Verify the update - show records that were updated
SELECT 
    dp.DispensedPrescriptionId,
    dp.PrescriptionLineId,
    m.Name as MedicationName,
    pl.Quantity as PrescriptionQuantity,
    dp.QuantityDispensed as DispensedQuantity,
    m.Price as MedicationPrice,
    dp.AmountDue as UpdatedAmountDue,
    CAST((m.Price * pl.Quantity) AS decimal(18,2)) as CalculatedAmount,
    dp.DispensingNotes
FROM DispensedPrescriptions dp
INNER JOIN PrescriptionLines pl ON dp.PrescriptionLineId = pl.PrescriptionLineId
INNER JOIN Medications m ON pl.MedicationId = m.MedicationId
WHERE dp.AmountDue > 0
  AND dp.DispensingNotes LIKE '%Imported from XLSX%'
ORDER BY dp.DispensedPrescriptionId;

