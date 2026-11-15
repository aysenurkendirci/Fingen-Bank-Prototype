-- A second way the app can look and speak: the young mode, brighter and rounder, on first-name terms and led by
-- savings. It sits with the other display preferences and follows the customer from device to device.

ALTER TABLE dbo.UserPreferences ADD
    Mode VARCHAR(8) NOT NULL CONSTRAINT DF_UserPreferences_Mode DEFAULT 'standard'
                             CONSTRAINT CK_UserPreferences_Mode CHECK (Mode IN ('standard', 'young'));
