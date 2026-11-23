#include "scene.hpp"
#include <cassert>
#include <cmath>
#include <cstdio>
using namespace aplscene;
int main() {
    // Default stimulus and per-profile cycle values.
    static_assert(default_min_apl == 1.0, "Start at 1 percent APL");
    static_assert(default_max_apl == 15.0, "Start at 15 percent APL");
    static_assert(default_loop_seconds(Profile::Steps) == 5.0, "Step loop: 5 seconds");
    static_assert(default_loop_seconds(Profile::Wave) == 5.0, "Wave loop: 5 seconds");
    static_assert(default_loop_seconds(Profile::Gaming) == 20.0, "Gaming loop: 20 seconds");
    assert(sample_apl_pct(Profile::Steps,0.6,5.0,1.0,15.0,false)==15.0);
    assert(sample_apl_pct(Profile::Steps,2.0,5.0,1.0,15.0,false)==1.0);
    auto patch=center_patch(3440,1440);
    assert(patch.side==223 && patch.x==1608 && patch.y==608);
    assert(std::abs(patch.area_fraction-0.01)<0.0001);
    assert(center_patch(1,300).side==1 && center_patch(0,0).side==0);
    assert(frame_apl_pct(1,100,0)==1);
    assert(std::abs(frame_apl_pct(1,100,0.01)-1.0669230769)<1e-9);
    assert(frame_apl_pct(10,130,patch.area_fraction)==10);
    // SMPTE ST 2084 standard input code values.
    assert(std::abs(pq_encode_nits(100.0)-0.5080784215)<0.00001);
    assert(std::abs(pq_encode_nits(1000.0)-0.7518270962)<0.00001);
    assert(std::abs(gray_nits(10.0)-130.0)<0.000001);
    // Four rapid game-like scene-change sequences, consisting of eight
    // 200-ms ramps in a 20-second loop (not single-frame jumps).
    struct Ramp { double begin; double end; double minimum_change; };
    constexpr Ramp ramps[]={
        {0.17,0.18,0.35},{0.215,0.225,0.30},
        {0.39,0.40,0.45},{0.435,0.445,0.35},
        {0.60,0.61,0.50},{0.64,0.65,0.30},
        {0.83,0.84,0.45},{0.88,0.89,0.45}
    };
    for(const auto& ramp:ramps) {
        double start=gaming_fraction(ramp.begin);
        double middle=gaming_fraction(0.5*(ramp.begin+ramp.end));
        double finish=gaming_fraction(ramp.end);
        assert(std::abs(finish-start)>=ramp.minimum_change);
        // The middle must actually be part-way through the slope.
        double lower=std::min(start,finish);
        double upper=std::max(start,finish);
        assert(middle>lower+0.08 && middle<upper-0.08);
        assert(std::abs(middle-0.5*(start+finish))<0.12);
        // Both endpoints must be continuous: no one-frame brightness jumps.
        for(double boundary:{ramp.begin,ramp.end}) {
            double before=gaming_fraction(boundary-1e-7);
            double after=gaming_fraction(boundary+1e-7);
            assert(std::abs(after-before)<0.001);
        }
    }
    assert(std::abs(gaming_fraction(0.0)-gaming_fraction(1.0))<1e-10);
    assert(std::abs(gaming_fraction(0.0500001)-gaming_fraction(0.0499999))<0.001);
    for(int mode=0;mode<3;++mode) {
        for(int i=0;i<2000;++i) {
            // Sample away from exact knot times to avoid unnecessary
            // floating-point boundary ambiguity.
            double t=(i+0.25)*0.01;
            double a=sample_apl_pct(static_cast<Profile>(mode),t,20.0,3.0,35.0,false);
            double b=sample_apl_pct(static_cast<Profile>(mode),t+20.0,20.0,3.0,35.0,false);
            assert(a>=3.0-1e-8&&a<=35.0+1e-8);
            assert(std::abs(a-b)<1e-7);
        }
    }
    for(int i=0;i<500;++i) {
        double t=i*0.01;
        for(auto p:{Profile::Steps,Profile::Wave}) {
            double a=sample_apl_pct(p,t,5.0,1.0,15.0,false);
            double b=sample_apl_pct(p,t+5.0,5.0,1.0,15.0,false);
            assert(std::abs(a-b)<1e-7);
        }
    }
    assert(sample_apl_pct(Profile::Steps,2.1,20.0,3.0,35.0,false)==35.0);
    assert(sample_apl_pct(Profile::Steps,7.1,20.0,3.0,35.0,false)==3.0);
    assert(sample_apl_pct(Profile::Manual,1,20,3,35,false)==3.0);
    assert(sample_apl_pct(Profile::Manual,1,20,3,35,true)==35.0);
    std::puts("ST2084 and deterministic scene tests passed.");
    return 0;
}
