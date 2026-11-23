#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <commctrl.h>
#include <d3d11.h>
#include <dxgi1_6.h>
#include <wrl/client.h>
#include <mmsystem.h>
#include <algorithm>
#include <cmath>
#include <cwchar>
#include <string>
#include "scene.hpp"

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "winmm.lib")
#pragma comment(lib, "user32.lib")
#pragma comment(lib, "gdi32.lib")

using Microsoft::WRL::ComPtr;
namespace apl=aplscene;
enum Id {PROFILE=101,FPS,LOW,HIGH,LOOP,APPLY,PAUSE,RESTART,TOGGLE,FULLSCREEN,HELP,STATUS};
static HWND panel=nullptr, scene_window=nullptr, selector=nullptr, fps_box=nullptr;
static HWND low_box=nullptr, high_box=nullptr, loop_box=nullptr, status_text=nullptr;
static HWND pause_button=nullptr, manual_button=nullptr;
static ComPtr<ID3D11Device> device;
static ComPtr<ID3D11DeviceContext> context;
static ComPtr<IDXGIFactory2> factory;
static ComPtr<IDXGISwapChain3> swapchain;
static ComPtr<ID3D11RenderTargetView> target;
static apl::Profile mode=apl::Profile::Steps;
static double target_fps=60, low_apl=apl::default_min_apl, high_apl=apl::default_max_apl;
static double loop_seconds=apl::default_loop_seconds(apl::Profile::Steps), scene_seconds=0;
static double profile_loop_seconds[]={
    apl::default_loop_seconds(apl::Profile::Gaming),
    apl::default_loop_seconds(apl::Profile::Steps),
    apl::default_loop_seconds(apl::Profile::Wave),
    apl::default_loop_seconds(apl::Profile::Manual)
};
static double current_apl=0, actual_fps=0, next_frame=0, last_frame=0, stats_start=0;
static unsigned stats_frames=0;
static bool running=true, manual_high=false, fullscreen=false, hdr_ready=false;
static bool tearing=false, quit=false;
static LONG_PTR previous_style=0;
static WINDOWPLACEMENT previous_placement{sizeof(WINDOWPLACEMENT)};
static LARGE_INTEGER frequency{};
static HFONT font=nullptr;

static double now_seconds() {
    LARGE_INTEGER count{};
    QueryPerformanceCounter(&count);
    return double(count.QuadPart)/double(frequency.QuadPart);
}
static HWND widget(const wchar_t* klass,const wchar_t* caption,DWORD style,
                   int x,int y,int w,int h,int id=0,DWORD exstyle=0) {
    HWND hwnd=CreateWindowExW(exstyle,klass,caption,WS_CHILD|WS_VISIBLE|style,
             x,y,w,h,panel,(HMENU)(INT_PTR)id,GetModuleHandleW(nullptr),nullptr);
    if(hwnd&&font) SendMessageW(hwnd,WM_SETFONT,(WPARAM)font,TRUE);
    return hwnd;
}
static bool get_value(HWND hwnd,double& value) {
    wchar_t buffer[128]{};
    GetWindowTextW(hwnd,buffer,128);
    wchar_t* end=nullptr;
    double number=wcstod(buffer,&end);
    if(end==buffer || !std::isfinite(number)) return false;
    while(*end==L' '||*end==L'\t') ++end;
    if(*end!=0) return false;
    value=number;
    return true;
}
static void help() {
    MessageBoxW(panel,
       L"1. Enable Windows HDR for your X36.\n"
       L"2. Install ReShade for APLHDRSceneTester.exe using Direct3D 10/11/12.\n"
       L"3. Load EOTF_Boost_X36.fx from test/x36-half-cosine-darkening.\n"
       L"4. Enable EOTF_Boost_1D_APL_LUT and PQ Decoded Normalized.\n"
       L"5. Check HDR10: ACTIVE in the control window.\n\n"
       L"The gray scene is a separate D3D11 HDR10 PQ swapchain;\n"
       L"the control panel is never drawn into its ReShade input.\n\n"
       L"F11 fullscreen | Esc windowed | Space pause/play | R restart\n"
       L"T toggle manual step | 1-4 select profile\n\n"
       L"Actual FPS shows achieved Present throughput; drivers can impose limits.",
       L"ReShade HDR APL setup", MB_OK|MB_ICONINFORMATION);
}
static void apply() {
    double f,l,h,period;
    if(!get_value(fps_box,f)||!get_value(low_box,l)||!get_value(high_box,h)||
       !get_value(loop_box,period)||f<1||f>360||l<0||h>100||l>=h||
       period<2||period>120) {
        MessageBoxW(panel,L"FPS: 1-360; minimum APL >= 0; maximum APL <= 100;\n"
           L"Min APL must be less than Max APL. Loop: 2-120 seconds.",
           L"Invalid values",MB_OK|MB_ICONWARNING);
        return;
    }
    target_fps=f;low_apl=l;high_apl=h;loop_seconds=period;
    profile_loop_seconds[static_cast<int>(mode)]=loop_seconds;
    next_frame=now_seconds();
}
static void pause_or_play() {
    running=!running;
    SetWindowTextW(pause_button,running?L"Pause (Space)":L"Resume (Space)");
}
static void select_mode(int n) {
    if(n<0||n>3) return;
    profile_loop_seconds[static_cast<int>(mode)]=loop_seconds;
    mode=static_cast<apl::Profile>(n);
    loop_seconds=profile_loop_seconds[n];
    wchar_t loop_label[32]{};
    swprintf_s(loop_label,L"%g",loop_seconds);
    if(loop_box) SetWindowTextW(loop_box,loop_label);
    SendMessageW(selector,CB_SETCURSEL,n,0);
    scene_seconds=0;
}
static void toggle_manual() {
    select_mode(3);
    manual_high=!manual_high;
    SetWindowTextW(manual_button,manual_high?L"Manual: HIGH (T)":L"Manual: LOW (T)");
}
static void change_fullscreen(bool enable) {
    if(enable==fullscreen || !scene_window) return;
    if(enable) {
        previous_style=GetWindowLongPtrW(scene_window,GWL_STYLE);
        previous_placement.length=sizeof(previous_placement);
        GetWindowPlacement(scene_window,&previous_placement);
        MONITORINFO info{sizeof(info)};
        GetMonitorInfoW(MonitorFromWindow(scene_window,MONITOR_DEFAULTTONEAREST),&info);
        SetWindowLongPtrW(scene_window,GWL_STYLE,(previous_style&~WS_OVERLAPPEDWINDOW)|WS_POPUP);
        SetWindowPos(scene_window,HWND_TOP,info.rcMonitor.left,info.rcMonitor.top,
           info.rcMonitor.right-info.rcMonitor.left,info.rcMonitor.bottom-info.rcMonitor.top,
           SWP_FRAMECHANGED|SWP_SHOWWINDOW);
    } else {
        SetWindowLongPtrW(scene_window,GWL_STYLE,previous_style);
        SetWindowPlacement(scene_window,&previous_placement);
        SetWindowPos(scene_window,HWND_NOTOPMOST,0,0,0,0,
            SWP_NOMOVE|SWP_NOSIZE|SWP_FRAMECHANGED|SWP_SHOWWINDOW);
    }
    fullscreen=enable;
}
static void update_status() {
    if(!status_text) return;
    wchar_t b[480]{};
    swprintf_s(b,L"HDR10: %s       Target: %.1f FPS       Actual: %.1f FPS\r\n"
         L"Source APL: %.2f%%       Full-frame gray: %.1f nits\r\n"
         L"Scene time: %.2f / %.1f s       %s\r\n"
         L"D3D11 | R10G10B10A2 | ST2084 PQ / Rec.2020",
         hdr_ready?L"ACTIVE":L"UNAVAILABLE",target_fps,actual_fps,
         current_apl,apl::gray_nits(current_apl),
         std::fmod(scene_seconds,loop_seconds),loop_seconds,
         running?L"running":L"paused");
    SetWindowTextW(status_text,b);
}
static void make_panel() {
    font=(HFONT)GetStockObject(DEFAULT_GUI_FONT);
    widget(L"STATIC",L"True HDR10/PQ gray scene for your X36 ReShade shader",0,16,13,458,22);
    widget(L"STATIC",L"Profile",0,16,51,105,23);
    selector=widget(L"COMBOBOX",L"",CBS_DROPDOWNLIST|WS_VSCROLL,130,45,320,200,PROFILE);
    for(const wchar_t* name:{
        L"Gaming loop (ramps and fluctuations)",L"Step response (sharp transitions)",
        L"Smooth cosine wave",L"Manual low/high step"})
        SendMessageW(selector,CB_ADDSTRING,0,(LPARAM)name);
    SendMessageW(selector,CB_SETCURSEL,1,0);
    widget(L"STATIC",L"Target FPS",0,16,92,105,22);
    fps_box=widget(L"EDIT",L"60",ES_NUMBER|ES_AUTOHSCROLL,130,86,90,26,FPS,WS_EX_CLIENTEDGE);
    widget(L"STATIC",L"Min APL %",0,16,132,105,22);
    low_box=widget(L"EDIT",L"1",ES_AUTOHSCROLL,130,126,90,26,LOW,WS_EX_CLIENTEDGE);
    widget(L"STATIC",L"Max APL %",0,250,132,100,22);
    high_box=widget(L"EDIT",L"15",ES_AUTOHSCROLL,352,126,98,26,HIGH,WS_EX_CLIENTEDGE);
    widget(L"STATIC",L"Loop seconds",0,16,172,105,22);
    loop_box=widget(L"EDIT",L"5",ES_AUTOHSCROLL,130,166,90,26,LOOP,WS_EX_CLIENTEDGE);
    widget(L"BUTTON",L"Apply settings",BS_PUSHBUTTON,250,166,200,29,APPLY);
    pause_button=widget(L"BUTTON",L"Pause (Space)",BS_PUSHBUTTON,16,211,210,33,PAUSE);
    widget(L"BUTTON",L"Restart (R)",BS_PUSHBUTTON,240,211,210,33,RESTART);
    manual_button=widget(L"BUTTON",L"Manual: LOW (T)",BS_PUSHBUTTON,16,254,210,33,TOGGLE);
    widget(L"BUTTON",L"Fullscreen (F11)",BS_PUSHBUTTON,240,254,210,33,FULLSCREEN);
    widget(L"BUTTON",L"ReShade setup and shortcuts",BS_PUSHBUTTON,16,302,434,30,HELP);
    status_text=widget(L"STATIC",L"",SS_LEFT,16,350,458,95,STATUS);
    widget(L"STATIC",L"Move the gray window to the HDR monitor; press F11 there.\n"
          L"Control panel is not part of the frame sampled by ReShade.",
          SS_LEFT,16,451,458,48);
    SetTimer(panel,1,250,nullptr);
    update_status();
}
static HRESULT make_target() {
    target.Reset();
    ComPtr<ID3D11Texture2D> surface;
    HRESULT hr=swapchain->GetBuffer(0,IID_PPV_ARGS(&surface));
    return FAILED(hr)?hr:device->CreateRenderTargetView(surface.Get(),nullptr,&target);
}
static void check_hdr() {
    if(!swapchain) return;
    UINT flags=0;
    constexpr auto colorspace=DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020;
    HRESULT hr=swapchain->CheckColorSpaceSupport(colorspace,&flags);
    hdr_ready=SUCCEEDED(hr)&&(flags&DXGI_SWAP_CHAIN_COLOR_SPACE_SUPPORT_FLAG_PRESENT)&&
              SUCCEEDED(swapchain->SetColorSpace1(colorspace));
}
static bool initialize_d3d() {
    if(FAILED(CreateDXGIFactory2(0,IID_PPV_ARGS(&factory)))) return false;
    BOOL supported=FALSE;
    ComPtr<IDXGIFactory5> fac5;
    if(SUCCEEDED(factory.As(&fac5))&&
       SUCCEEDED(fac5->CheckFeatureSupport(DXGI_FEATURE_PRESENT_ALLOW_TEARING,
                                           &supported,sizeof(supported))))
        tearing=supported!=FALSE;
    D3D_FEATURE_LEVEL level{};
    if(FAILED(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,
        D3D11_CREATE_DEVICE_BGRA_SUPPORT,nullptr,0,D3D11_SDK_VERSION,
        &device,&level,&context))) return false;
    RECT rc{};
    GetClientRect(scene_window,&rc);
    DXGI_SWAP_CHAIN_DESC1 desc{};
    desc.Width=std::max(1L,rc.right-rc.left);
    desc.Height=std::max(1L,rc.bottom-rc.top);
    desc.Format=DXGI_FORMAT_R10G10B10A2_UNORM;
    desc.BufferCount=2;
    desc.SampleDesc.Count=1;
    desc.BufferUsage=DXGI_USAGE_RENDER_TARGET_OUTPUT;
    desc.SwapEffect=DXGI_SWAP_EFFECT_FLIP_DISCARD;
    desc.Scaling=DXGI_SCALING_STRETCH;
    desc.AlphaMode=DXGI_ALPHA_MODE_IGNORE;
    desc.Flags=tearing?DXGI_SWAP_CHAIN_FLAG_ALLOW_TEARING:0;
    ComPtr<IDXGISwapChain1> sc1;
    if(FAILED(factory->CreateSwapChainForHwnd(device.Get(),scene_window,&desc,
                                              nullptr,nullptr,&sc1))) return false;
    if(FAILED(sc1.As(&swapchain))) return false;
    factory->MakeWindowAssociation(scene_window,DXGI_MWA_NO_ALT_ENTER);
    if(FAILED(make_target())) return false;
    check_hdr();
    return true;
}
static void resize_swapchain() {
    if(!swapchain||!context) return;
    RECT rc{};
    GetClientRect(scene_window,&rc);
    if(rc.right<=0||rc.bottom<=0) return;
    context->OMSetRenderTargets(0,nullptr,nullptr);
    target.Reset();
    HRESULT hr=swapchain->ResizeBuffers(0,(UINT)rc.right,(UINT)rc.bottom,
         DXGI_FORMAT_UNKNOWN,tearing?DXGI_SWAP_CHAIN_FLAG_ALLOW_TEARING:0);
    if(SUCCEEDED(hr)) {hr=make_target();check_hdr();}
    if(FAILED(hr)) hdr_ready=false;
}
static void render() {
    if(!swapchain||!target) return;
    current_apl=apl::sample_apl_pct(mode,scene_seconds,loop_seconds,
                                    low_apl,high_apl,manual_high);
    float pq=float(apl::pq_encode_nits(apl::gray_nits(current_apl)));
    const float rgba[4]={pq,pq,pq,1.0f};
    context->OMSetRenderTargets(1,target.GetAddressOf(),nullptr);
    context->ClearRenderTargetView(target.Get(),rgba);
    HRESULT hr=swapchain->Present(0,tearing?DXGI_PRESENT_ALLOW_TEARING:0);
    if(SUCCEEDED(hr)) ++stats_frames;
    if(hr==DXGI_ERROR_DEVICE_REMOVED||hr==DXGI_ERROR_DEVICE_RESET) quit=true;
}
static void handle_key(WPARAM k) {
    if(k==VK_F11) change_fullscreen(!fullscreen);
    else if(k==VK_ESCAPE) {if(fullscreen)change_fullscreen(false);}
    else if(k==VK_SPACE) pause_or_play();
    else if(k=='R') scene_seconds=0;
    else if(k=='T') toggle_manual();
    else if(k>='1'&&k<='4') select_mode(int(k-'1'));
}
static LRESULT CALLBACK scene_proc(HWND hwnd,UINT msg,WPARAM wp,LPARAM lp) {
    switch(msg) {
        case WM_SIZE: if(wp!=SIZE_MINIMIZED)resize_swapchain();return 0;
        case WM_MOVE:
        case WM_DISPLAYCHANGE: check_hdr();return 0;
        case WM_KEYDOWN: handle_key(wp);return 0;
        case WM_CLOSE: DestroyWindow(hwnd);return 0;
        case WM_DESTROY: scene_window=nullptr;quit=true;PostQuitMessage(0);return 0;
    }
    return DefWindowProcW(hwnd,msg,wp,lp);
}
static LRESULT CALLBACK panel_proc(HWND hwnd,UINT msg,WPARAM wp,LPARAM lp) {
    switch(msg) {
        case WM_COMMAND:
            if(LOWORD(wp)==PROFILE&&HIWORD(wp)==CBN_SELCHANGE)
                select_mode(int(SendMessageW(selector,CB_GETCURSEL,0,0)));
            else if(HIWORD(wp)==BN_CLICKED) {
                switch(LOWORD(wp)) {
                    case APPLY:apply();break;
                    case PAUSE:pause_or_play();break;
                    case RESTART:scene_seconds=0;break;
                    case TOGGLE:toggle_manual();break;
                    case FULLSCREEN:change_fullscreen(!fullscreen);break;
                    case HELP:help();break;
                }
            }
            return 0;
        case WM_TIMER:update_status();return 0;
        case WM_CLOSE:DestroyWindow(hwnd);return 0;
        case WM_DESTROY:
            panel=nullptr;
            if(scene_window)DestroyWindow(scene_window);
            quit=true;PostQuitMessage(0);return 0;
    }
    return DefWindowProcW(hwnd,msg,wp,lp);
}
int WINAPI wWinMain(HINSTANCE instance,HINSTANCE,PWSTR,int) {
    SetProcessDPIAware();
    QueryPerformanceFrequency(&frequency);
    WNDCLASSW cls{};
    cls.hInstance=instance;
    cls.hCursor=LoadCursor(nullptr,IDC_ARROW);
    cls.hbrBackground=(HBRUSH)(COLOR_BTNFACE+1);
    cls.lpfnWndProc=panel_proc;
    cls.lpszClassName=L"APLHDRControl";
    if(!RegisterClassW(&cls)) return 1;
    cls.lpfnWndProc=scene_proc;
    cls.lpszClassName=L"APLHDRScene";
    cls.hbrBackground=nullptr;
    if(!RegisterClassW(&cls)) return 1;
    MONITORINFO info{sizeof(info)};
    GetMonitorInfoW(MonitorFromWindow(GetDesktopWindow(),MONITOR_DEFAULTTOPRIMARY),&info);
    RECT r=info.rcWork;
    int x=r.left,y=r.top;
    panel=CreateWindowExW(0,L"APLHDRControl",L"APL HDR Scene Tester - Controls",
             WS_OVERLAPPED|WS_CAPTION|WS_SYSMENU|WS_MINIMIZEBOX,
             x+20,y+45,486,545,nullptr,nullptr,instance,nullptr);
    if(!panel) return 1;
    make_panel();
    int width=std::max(600L,std::min(1600L,r.right-r.left-530));
    int height=std::max(450L,std::min(950L,r.bottom-r.top-100));
    scene_window=CreateWindowExW(0,L"APLHDRScene",
             L"APL HDR Scene - Install ReShade for APLHDRSceneTester.exe",
             WS_OVERLAPPEDWINDOW|WS_VISIBLE,x+520,y+45,width,height,
             nullptr,nullptr,instance,nullptr);
    if(!scene_window) return 1;
    if(!initialize_d3d()) {
        MessageBoxW(panel,L"Could not initialize native D3D11 / HDR10 swapchain.",
                    L"Renderer error",MB_OK|MB_ICONERROR);
        return 2;
    }
    ShowWindow(scene_window,SW_SHOWNORMAL);
    ShowWindow(panel,SW_SHOWNORMAL);
    if(!hdr_ready) MessageBoxW(panel,
          L"HDR10 color space is not available yet.\n"
          L"Enable Windows HDR and move the gray window to the HDR monitor.\n"
          L"Resize it to recheck. Do not evaluate APL until HDR10: ACTIVE.",
          L"HDR10 unavailable",MB_OK|MB_ICONWARNING);
    timeBeginPeriod(1);
    next_frame=last_frame=stats_start=now_seconds();
    MSG message{};
    while(!quit) {
        while(PeekMessageW(&message,nullptr,0,0,PM_REMOVE)) {
            if(message.message==WM_QUIT) {quit=true;break;}
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }
        if(quit)break;
        double now=now_seconds();
        if(now<next_frame-0.002) {
            Sleep((DWORD)std::max(1.0,std::floor((next_frame-now)*1000.0-1.0)));
            continue;
        }
        if(now<next_frame){SwitchToThread();continue;}
        double dt=std::max(0.0,now-last_frame);
        last_frame=now;
        if(running)scene_seconds+=dt;
        render();
        now=now_seconds();
        if(now-stats_start>=1.0) {
            actual_fps=stats_frames/(now-stats_start);
            stats_frames=0;
            stats_start=now;
        }
        double step=1.0/target_fps;
        next_frame+=step;
        if(next_frame<now-step)next_frame=now+step;
    }
    timeEndPeriod(1);
    target.Reset();swapchain.Reset();context.Reset();device.Reset();factory.Reset();
    return 0;
}
