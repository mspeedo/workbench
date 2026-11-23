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
    // SMPTE ST 2084 standard input code values.
    assert(std::abs(pq_encode_nits(100.0)-0.5080784215)<0.00001);
    assert(std::abs(pq_encode_nits(1000.0)-0.7518270962)<0.00001);
    assert(std::abs(gray_nits(10.0)-130.0)<0.000001);
    for(int mode=0;mode<3;++mode) {
        for(int i=0;i<2000;++i) {
            double t=i*0.01;
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
