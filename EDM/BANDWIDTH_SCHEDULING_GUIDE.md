# Bandwidth Scheduling Implementation Summary

## Overview
Implemented time-based bandwidth scheduling feature with full backward compatibility for existing settings files. Users can now define multiple time-based bandwidth schedules (e.g., limited speed 9am-5pm, full speed midnight-6am).

## Files Created

### 1. **EDM/Models/BandwidthSchedule.cs** (NEW)
- **TimeRange class**: Represents time periods with StartHour and EndHour (0-23)
  - `IsInRange(hour)` method handles wrap-around logic (e.g., 22:00-06:00 spans midnight)
- **BandwidthSchedule class**: Contains TimeRange and SpeedLimitKbps
  - Constructor ensures valid hour values and positive speed limits

## Files Modified

### 2. **EDM/Services/SettingsService.cs**
- **AppSettings class**:
  - Added `BandwidthSchedules` property (List<BandwidthSchedule>) with empty list default

- **Load() method**:
  - Updated to handle backward compatibility
  - If BandwidthSchedules field is missing from old settings.json, it initializes to empty list
  - No crashes for existing users upgrading

- **New Methods**:
  - `GetBandwidthSchedules()`: Returns list of configured schedules
  - `SetBandwidthSchedules(list)`: Persists schedule list to settings.json and logs update
  - `GetActiveBandwidthLimitKbps()`: Core logic method that:
	- Checks current time against all configured schedules
	- Returns the most restrictive (lowest non-zero) active limit
	- Falls back to global bandwidth limit if no schedule is active
	- Returns 0 if unlimited

### 3. **EDM/Services/Interfaces/ISettingsService.cs**
- Added interface methods:
  - `GetActiveBandwidthLimitKbps()`: For consumers to query active limit
  - `GetBandwidthSchedules()`: Get schedule list
  - `SetBandwidthSchedules()`: Set schedule list

### 4. **EDM/Services/SchedulerService.cs**
- No functional changes (kept for existing time-of-day scheduler functionality)
- Bandwidth schedule logic moved to SettingsService to centralize access

### 5. **EDM/Services/DownloadService.cs**
- Updated `combinedSpeedProvider` lambda to call `GetActiveBandwidthLimitKbps()` instead of `GetBandwidthLimitKbps()`
- Downloads now respect active time-based schedules automatically

### 6. **EDM/Services/AdaptiveConnectionManager.cs**
- Updated bandwidth estimation to use `GetActiveBandwidthLimitKbps()` instead of `GetBandwidthLimitKbps()`
- Ensures connection manager adapts to scheduled limits

### 7. **EDM/Services/AdaptiveChunkSizer.cs**
- Updated chunk size calculation to use `GetActiveBandwidthLimitKbps()` instead of `GetBandwidthLimitKbps()`
- Automatic chunk sizing respects time-based schedules

## Architecture & Design

### Backward Compatibility
- Old settings.json files without `BandwidthSchedules` field load successfully
- Missing field defaults to empty list (no scheduling active)
- Existing users can continue using global `BandwidthLimitKbps` without UI changes

### Schedule Resolution Logic
1. If no schedules defined → use global `BandwidthLimitKbps`
2. Check current hour against all active schedules
3. If multiple schedules match current time:
   - Use the most restrictive (lowest non-zero) limit
   - Prevents conflicting schedules from causing issues
4. If scheduled limit is 0 (unlimited) → fall back to global limit
5. Return 0 only if all paths = unlimited

### Integration Points
- **SettingsService**: Central point for all bandwidth limit queries
- **DownloadService**: Uses active limit in speed provider
- **AdaptiveConnectionManager**: Estimates bandwidth based on active limit
- **AdaptiveChunkSizer**: Calculates chunks using active limit

## Usage Example

```csharp
// Set up schedules: Limited speed 9am-5pm, full speed other hours
var schedules = new List<BandwidthSchedule>
{
	new BandwidthSchedule(9, 17, 512),   // 512 KB/s from 9:00 to 17:00
	new BandwidthSchedule(0, 9, 0),      // Unlimited 0:00 to 9:00 (midnight to 9am)
	new BandwidthSchedule(17, 24, 0)     // Unlimited 17:00 to 24:00 (5pm to midnight)
};

settingsService.SetBandwidthSchedules(schedules);

// During download:
int activeLimitKbps = settingsService.GetActiveBandwidthLimitKbps();  // Returns 512 or 0 based on time
```

## Testing Notes
- ✅ Build successful with all changes
- ✅ Backward compatibility verified (Load handles missing field)
- ✅ Multiple downloads picking up active limits automatically
- ✅ Wrap-around time ranges work correctly (e.g., 22:00-06:00)

## Future Enhancements
- UI dialog to manage schedules (SchedulerWindow extension)
- Per-URL schedule overrides
- Holiday/exception date support
- Visual timeline display of scheduled bandwidth limits
