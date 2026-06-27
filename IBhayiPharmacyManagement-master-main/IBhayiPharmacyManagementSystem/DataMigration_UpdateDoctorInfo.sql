-- Data Migration Script: Update Doctor Information in UnprocessedScripts
-- This script copies doctor information from Prescriptions to UnprocessedScripts
-- for existing completed prescriptions that don't have doctor information

-- Update UnprocessedScripts with doctor information from their associated Prescriptions
UPDATE UnprocessedScripts 
SET DoctorId = p.DoctorId
FROM UnprocessedScripts us
INNER JOIN Prescriptions p ON us.UnploadId = p.UploadId
WHERE us.DoctorId IS NULL 
  AND p.DoctorId IS NOT NULL
  AND us.Status IN (2, 3); -- 2 = Processing, 3 = Completed

-- Verify the update
SELECT 
    us.UnploadId,
    us.Status,
    us.DoctorId as UnprocessedScript_DoctorId,
    p.DoctorId as Prescription_DoctorId,
    d.Name as DoctorName
FROM UnprocessedScripts us
LEFT JOIN Prescriptions p ON us.UnploadId = p.UploadId
LEFT JOIN Doctors d ON us.DoctorId = d.DoctorId
WHERE us.Status IN (2, 3) -- Processing and Completed
ORDER BY us.UnploadId;
