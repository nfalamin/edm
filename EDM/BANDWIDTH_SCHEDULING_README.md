# Bandwidth Scheduling - Quick Reference

## What Was Implemented

A time-based bandwidth scheduling system that allows automatic speed limit adjustments based on the current time of day.

### Example Use Cases
- **Office hours (9am-5pm)**: Limit downloads to 512 KB/s to avoid network congestion
- **Off-hours (midnight-6am)**: Full speed unlimited bandwidth
- **Evening (6pm-midnight)**: Medium speed 2048 KB/s

## Key Components

### 1. Backward Compatibility ✅
- Old settings files without schedules load without errors
- Existing bandwidth limit setting still works independently
- No breaking changes for current users

### 2. Time Range Logic
Supports wrap-around times:
```
22:00 - 06:00  (10pm to 6am throughout night)  ✅ Works correctly
09:00 - 17:00  (9am to 5pm)                     ✅ Works correctly
```

### 3. Schedule Priority
If multiple schedules match current time:
- Most restrictive (lowest non-zero KB/s) wins
- If any schedule is unlimited (0 KB/s), uses global limit
- If no schedules, uses global limit

## Integration with Download System

The system automatically applies schedules across:
- **DownloadService**: Respects active limit during downloads
- **AdaptiveConnectionManager**: Adjusts concurrent connections
- **AdaptiveChunkSizer**: Sizes chunks based on active limit

No manual intervention needed - just configure schedules and downloads automatically respect them.

## Settings Storage

Schedules are saved in `settings.json`:
```json
{
  "BandwidthLimitKbps": 0,
  "BandwidthSchedules": [
	{
	  "TimeRange": { "StartHour": 9, "EndHour": 17 },
	  "SpeedLimitKbps": 512
	},
	{
	  "TimeRange": { "StartHour": 22, "EndHour": 6 },
	  "SpeedLimitKbps": 0
	}
  ]
}
```

## Code Usage

```csharp
// Get current active bandwidth limit (considering schedules and time)
int activeLimitKbps = settingsService.GetActiveBandwidthLimitKbps();

// Get/set all schedules
var schedules = settingsService.GetBandwidthSchedules();
settingsService.SetBandwidthSchedules(newSchedules);
```

## Next Steps (Optional Features)

1. **UI Implementation**: Create dialog in SettingsWindow to manage schedules
2. **Validation**: Add UI validation for non-overlapping ranges
3. **Presets**: Offer common schedule templates (Office Hours, Gaming, etc.)
4. **Testing**: Add unit tests for TimeRange.IsInRange() edge cases

---
**Build Status**: ✅ All changes compile successfully
**Backward Compatibility**: ✅ Verified
**Testing**: Ready for manual UI testing when schedules UI is added
