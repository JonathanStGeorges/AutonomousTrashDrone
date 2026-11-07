# In-simulator dashboard

`DemoDashboard` receives the lead-owned `DemoRuntime` reference. Its 1600×900 logical layout scales to the current Game view/window. The main camera occupies the left upper area; completed onboard RGB and sonar data occupy the right column. The lower-left map displays estimated perception tracks and the known boundary/active goal. It never reads scenario actor lists. The lower telemetry panel shows decision reason, target heuristic score/age, speed, wind, actuator commands, collector fill, render FPS and measured sensor processing rates. Rates labelled `sim Hz` use simulation time, so accelerated tests must also report wall rendering rate.

Start/Stop call mission commands; Stop closes intake and depowers through the physical actuator interface. Pause/Resume calls `DemoRuntime.SetPaused` and freezes simulation time. Manual mode uses the mission's W/A/D actuator handling with collection still closed. Switch view selects the lead's chase/overhead view modes. The scenario selector changes only the pending reset choice; Reset seed rebuilds that scenario with the entered seed and leaves the mission stopped. Start then runs it.

Motor assistance is explicitly labelled in the header and can be disabled. RGB/sonar/map overlays can be toggled; previews remain the actual completed sensor images. Camera/sonar freeze controls intentionally stop observation delivery so freshness interlocks can be demonstrated. The sonar-noise slider modifies the common degraded representation used by both preview and perception. Camera's frozen preview exposes wall age as well as simulated capture age.

`RubbishCaptured`, `Bycatch` and `CollisionCount` are evaluator-only display fields, initialized to -1 (pending) until the lead supplies scoring. They do not feed mission decisions. The physical aggregate capture count and storage fill remain visible even before evaluator scoring arrives.

GUI and scenario behaviour must be checked in a rendered integration run; source compilation alone does not establish button usability, overlay alignment or readable layout at a particular display resolution.
