# Replace the visual proxy with future CAD

Keep the existing Rigidbody/controller root, sensor adapters, mission interfaces and collector event boundary. Work in a duplicate dedicated demo prefab first; do not replace original MARUS scenes or discard existing asset GUIDs.

1. Import a mesh with explicit units converted to metres. Orient +Z toward bow, +Y up and +X starboard, with the nominal waterline at root y0. Verify a known dimension against Unity's measured renderer bounds.
2. Attach the mesh beneath the visual child and remove only the old visual proxy. Do not add duplicate rigidbodies or colliders from CAD import to the physical root accidentally. Keep materials/render layers compatible with HDRP and sensor visibility.
3. Replace the conservative proxy colliders with suitable primitive/convex geometry. Update vessel length, width, mass, centre of mass, inertia assumptions, buoyancy sample locations and drag. The current imported monohull and wider twin-hull envelope are intentionally provisional; they must become consistent.
4. Place port/starboard force mounts at actual proposed locations and axes. Update thrust limits, response rate, reverse capability and command mapping from measured actuator data. Verify equal positive thrust drives forward and larger port thrust causes right yaw. Check the complete swept thruster/hull/intake envelope.
5. Place camera and submerged sonar at calibrated extrinsics. Verify near-intake visibility, water-contact ranging, min-range blind spots, own-hull occlusion and acoustic/collision layers with actual rendered/raycast fixtures. Keep the independent observer cameras separate.
6. Align the intake trigger with the physical opening, update dimensions/capacity/storage and test real ingress, overspeed entry, duplicate counting and unintended fish entry. Do not use truth tags to reject bycatch.
7. Re-run reset, stale sensors, conflicts, avoidance, stopping, physical collection and overview/view-independence acceptance with recorded seeds. A visually plausible model is not physical validation.

Real hardware requires calibrated cameras/sonar and synchronized timestamps; detector training and evaluation on representative real marine rubbish/wildlife, calibrated uncertainty and out-of-distribution handling; validated state estimation and environmental models; measured thrust/drag/mass response; an independently safe motor/collector interface with watchdog/emergency stop; and controlled physical trials. Replace adapters behind the existing contracts rather than forwarding simulator object identities. Keep detector/model and imported-asset licence obligations from MODELS.md and ASSETS.md.

Sail/rudder parameters are not part of production v2 navigation. Reintroducing sailing would require a separately specified physical/control model and new validation, not renaming the current thruster results.
