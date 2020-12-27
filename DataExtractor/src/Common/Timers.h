#pragma once

namespace Time
{ 
	class Timer
	{ 
	public: 
		void Capture(); 
		long GetElapsed() const ;

	private: 
		long stamp[2];
	};
}