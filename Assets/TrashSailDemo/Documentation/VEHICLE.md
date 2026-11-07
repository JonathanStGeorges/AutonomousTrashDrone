# Production vessel, actuators and intake

The configurable engineering proxy retains a stable Rigidbody root, separate visual and collision children, sensor mounts, collector and two force mounts. The current imported MARUS boat visual does not match the conservative twin-hull collision envelope exactly. Default dimensions/mass remain 3 m x 2.2 m and 110 kg; these are provisional simulation values, not confirmed hardware specifications.

## Primary twin thrusters

DemoVessel applies port and starboard force through AddForceAtPosition. Default mounts are (-0.85,-0.2,-1.1) and (+0.85,-0.2,-1.1) m, facing local +Z; configured mounting Euler angles replace that orientation. Each signed command is bounded, reverse is configurable, maximum force defaults to 80 N per thruster, and normalized response rate defaults to 2/s. Positive port-minus-starboard produces right yaw. DemoVessel.Control requests bounded heading-rate/surge feedback; Allocate maps surge/yaw to independently saturated commands.

Four-point spring/damper buoyancy, gravity, longitudinal/lateral/yaw drag and Rigidbody inertia determine motion. Optional WaterHeightSampler supplies water height; the unwired mean-water default is y0. Current subtracts from water-relative drag velocity; optional configured wind force is a disturbance. Operational control never replaces transform/velocity. Reset alone explicitly restores pose and motion.

Command expiry closes the collector and ramps thrusters toward idle. Applied forces can persist during actuator response; idle does not erase momentum. PortCommand/StarboardCommand, applied forces, ActuatorHealthy and RequestedSpeed/Heading are exposed. SailDrive is zero; legacy sail/motor fields are migration aliases and are not production propulsion controls. The earlier polar/tacking tests and claims belong to checkpoint 7b2f197, not this configuration.

## Sensor and collection geometry

Preserve the actual configured CameraMount and SonarMount when replacing visuals. Camera pitch/FOV may be overridden by scene assembly: inspect the regenerated scene rather than assuming an old prototype angle. Sonar must remain submerged, ahead of own-hull occlusion and clear of trigger geometry. Layer8 is own hull,9 underwater actors,10 floating targets,11 obstacles; verify camera culling and sonar ray coverage after geometry changes.

The intake defaults to centre (0,-0.08,1.8), configurable width, 0.9 m height and 0.8 m fore/aft depth. Active plus available capacity plus an external Rigidbody centre inside the trigger produces capture. Normal operational policy separately checks model eligibility/conflicts, freshness, alignment, safe speed and hazard clearance. Unexpected overspeed ingress still captures and increments OverspeedCaptureCount. No truth label suppresses fish capture. Deduplication prevents repeated counts; captured bodies move to storage only after the physical event.

OceanPopulation preserves collected world records across streaming. Reset clears actors/contents, counting, actuator response, own-state history, camera sessions, sonar tracks and mission state through runtime coordination. Imported rubbish/fish geometry and seeded motion are documented in ASSETS.md. The fish is scripted rigid-body motion, not validated swimming biology.

## Limits and verification

Buoyancy, drag, propulsion allocation, mass properties and acoustic geometry are approximations. The renderer may show waves while physics remains mean-water. Own state is explicitly ideal when noise/latency are zero. Physical thrust, yaw sign, stopping, capture/bycatch, stale-command handling and sensor visibility need the current integrated tests. Historical sailing evidence does not validate primary thrusters. See GATES.md and the lead's current evidence report; CAD replacement steps are in CAD_REPLACEMENT.md.
