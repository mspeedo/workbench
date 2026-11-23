#include "scene.hpp"
#include <cassert>
#include <cmath>
#include <cstdio>
using namespace aplscene;
int main() {
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
    assert(sample_apl_pct(Profile::Steps,2.1,20.0,3.0,35.0,false)==35.0);
    assert(sample_apl_pct(Profile::Steps,7.1,20.0,3.0,35.0,false)==3.0);
    assert(sample_apl_pct(Profile::Manual,1,20,3,35,false)==3.0);
    assert(sample_apl_pct(Profile::Manual,1,20,3,35,true)==35.0);
    std::puts("ST2084 and deterministic scene tests passed.");
    return 0;
}
