# Verification record

Editor: Unity 6000.3.22f1.

Before modification:
- Chinese and English implementations both retimed a 2-second .anim to 1 second.
- Both lost weighted tangent mode (Both became None).
- Both lost loopTime (true became false).
- Baseline details are in the local ignored Logs/baseline.log.

After modification:
- 8 regression cases and 68 assertions passed. See regression.txt.
- Real Unity asset import/export was used, including the repository's synthetic ASCII FBX fixture.
- Temporary test assets were removed after the run.
- Mouse-driven GUI interaction and third-party Humanoid/DCC files were not tested.

Reproduction command is in README.md.
