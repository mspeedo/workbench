#pragma once
#include <algorithm>
#include <cmath>
namespace aplscene {
constexpr double reference_white_nits = 1300.0;
constexpr double pi = 3.1415926535897932384626433832795;
enum class Profile { Gaming = 0, Steps = 1, Wave = 2, Manual = 3 };
constexpr double default_min_apl = 1.0;
constexpr double default_max_apl = 15.0;
// Profile defaults only: user-entered loop lengths are remembered per profile.
inline constexpr double default_loop_seconds(Profile profile) {
    return profile == Profile::Gaming ? 20.0 : 5.0;
}
inline double clamp01(double x) { return std::max(0.0, std::min(1.0, x)); }
inline double pq_encode_nits(double nits) {
    // SMPTE ST 2084 / HDR10; normalize to 10,000 nits.
    constexpr double m1=2610.0/16384.0, m2=2523.0/32.0;
    constexpr double c1=3424.0/4096.0, c2=2413.0/128.0, c3=2392.0/128.0;
    double p=std::pow(std::max(0.0,std::min(10000.0,nits))/10000.0,m1);
    return std::pow((c1+c2*p)/(1.0+c3*p),m2);
}
inline double gaming_fraction(double phase) {
    // Fixed, deterministic game-like APL envelope, independent of frame rate.
    struct Key { double t,v; };
    constexpr Key points[]={
        {0.00,0.12},{0.05,0.25},{0.13,0.70},{0.17,0.65},
        {0.22,0.14},{0.30,0.28},{0.35,0.90},{0.39,0.80},
        {0.45,0.26},{0.52,0.47},{0.57,0.94},{0.64,0.18},
        {0.73,0.43},{0.80,0.17},{0.85,0.92},{0.92,0.39},{1.00,0.12}
    };
    double v=points[0].v;
    for(unsigned i=1;i<sizeof(points)/sizeof(points[0]);++i) {
        if(phase<=points[i].t) {
            double u=(phase-points[i-1].t)/(points[i].t-points[i-1].t);
            v=points[i-1].v+(points[i].v-points[i-1].v)*u;
            break;
        }
    }
    return clamp01(v+0.038*std::sin(2*pi*13*phase)+0.019*std::sin(2*pi*31*phase));
}
inline double sample_apl_pct(Profile mode,double seconds,double loop,double low,double high,bool manual_high) {
    low=std::max(0.0,std::min(100.0,low));
    high=std::max(low,std::min(100.0,high));
    if(mode==Profile::Manual) return manual_high?high:low;
    if(loop<=0) loop=20.0;
    double phase=std::fmod(std::max(0.0,seconds),loop)/loop;
    double value=0;
    switch(mode) {
        case Profile::Steps: value=((phase>=0.10&&phase<0.35)||(phase>=0.65&&phase<0.85))?1.0:0.0; break;
        case Profile::Wave: value=0.5-0.5*std::cos(2*pi*phase); break;
        default: value=gaming_fraction(phase); break;
    }
    return low+(high-low)*value;
}
inline double gray_nits(double apl) {return reference_white_nits*std::max(0.0,std::min(100.0,apl))/100.0;}
}
