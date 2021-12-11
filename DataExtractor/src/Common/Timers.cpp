
#include "Timers.h"

#include <time.h>

namespace Time
{ 
	// -----------------------------------------------------------------------------------------------------------
	void Timer::Capture()
	{ 
		stamp[0] = stamp[1];
		stamp[1] = clock();
	}

	// -----------------------------------------------------------------------------------------------------------
	long Timer::GetElapsed() const
	{ 
		return stamp[1]-stamp[0]; 
	}
}