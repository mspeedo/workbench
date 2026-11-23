# HDR APL Scene Tester

Native Windows x64 Direct3D 11 HDR10 gray-scene application for ReShade APL smoothing experiments, especially the MSI X36 shader at https://github.com/mspeedo/QD-OLED-APL-FIXER/tree/test/x36-half-cosine-darkening

## Setup

1. Enable Windows HDR on the target HDR display.
2. Download the Windows artifact from https://github.com/mspeedo/workbench/actions/workflows/apl-hdr-tester-build.yml and extract APLHDRSceneTester.exe.
3. Run the EXE and verify the status reports **HDR10: ACTIVE**. If not, enable HDR, move the test scene to the appropriate HDR display, and resize it. Do not measure APL before HDR10 becomes ACTIVE.
4. Install ReShade into APLHDRSceneTester.exe and select **Direct3D 10/11/12**. Get EOTF_Boost_X36.fx from the shader branch above, enable EOTF_Boost_1D_APL_LUT, and set **APL Input Mode = PQ Decoded Normalized**. Keep APL Reference White at **1300 nits**.
5. Move the separate gray-scene window to the target monitor and press F11 for fullscreen. The control panel is a separate Win32 window and is not included in the swapchain that ReShade samples. You can still adjust controls when the test is in fullscreen.

## Modes and controls

- Step response (**startup default**, 5-second loop): clean high and low plateaus for evaluating exponential rise and half-cosine fall.
- Smooth cosine wave (**5-second loop**): periodic moving APL with no discontinuities.
- Gaming loop (**20-second loop**): deterministic scene with gradual brightness ramps, smaller rapid fluctuations, and **four fast scene-change sequences** around 3.4–4.5 s, 7.8–8.9 s, 12.0–13.0 s, and 16.6–17.8 s. Each rapid darkening or brightening takes about **200 ms** (eight steep ramps total), with brief darker or brighter holds between paired transitions. The brightness remains continuous at every frame—there are **no instantaneous one-frame jumps**. This tests the shader's half-cosine darkening under moving game-like APL. Every FPS samples the **same time-based timeline**.
- Manual: toggle a single low/high step with T or the panel button.

On startup: **Step response, 60 target FPS, 1% to 15% input APL, 5-second loop**. Switching profiles selects its own default period (Gaming 20 seconds; Steps and Wave 5 seconds), and custom loop durations set with Apply are retained per profile until exit. FPS can be 1 to 360; use 30/60/90/120/144/180/240 for comparisons. **Actual FPS** reports the achieved Present rate (which may be lower due to DWM, GPU driver, compositor or VSync).

Keyboard shortcuts while the gray scene has focus: F11 = fullscreen, Esc = windowed, Space = pause/resume timeline, R = restart, T = manual step, 1-4 = select mode. The panel buttons work with the control panel focused.

## HDR APL accuracy

True 10-bit DXGI flip-discard swapchain: DXGI_FORMAT_R10G10B10A2_UNORM in color space DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020. All screen pixels are equal-channel Rec.2020 gray values encoded using SMPTE ST 2084. No SDR approximation and no text/overlay in the test framebuffer.

For the shader's default 1300-nit reference: signal nits = chosen input APL% * 13. Thus 1% = 13 nits; 10% = 130 nits; 15% = 195 nits. These are source-signal values, not a claim about physically measured panel luminance. Higher full-screen APL may be uncomfortably bright; begin with defaults.

Uses QueryPerformanceCounter for frame pacing and elapsed scene time, not frame counts. Attempts Present(0) and permits tearing where OS supports it. Some systems may still throttle actual Present rate: use panel FPS reading to confirm before comparison.

**Caveats:** uniform gray reproduces temporal mean APL but not a real game's spatial highlight distribution; generated pixel values are precise but panel ABL, Windows tone mapping and ReShade hooks can all influence observed luminance. The app cannot verify actual physical brightness and cannot force HDR mode on a disabled display.

## Native build

Using VS2022 Developer Command Prompt with Windows SDK:

    cl /nologo /std:c++17 /EHsc /O2 /W4 /DUNICODE /D_UNICODE /MT /Fe:APLHDRSceneTester.exe src\main.cpp /link d3d11.lib dxgi.lib comctl32.lib winmm.lib user32.lib gdi32.lib

Automated CI builds the EXE, runs deterministic PQ/profile tests, and publishes the artifact. The shader repository is not modified.
