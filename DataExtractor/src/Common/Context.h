#pragma once

namespace Context 
{ 
	//simplified context by type storage ( we don't need anything fancier for this application )

	namespace Impl
	{ 
		template<typename T> struct Storage { static inline T* instance = nullptr; };
	}

	template<typename T> T* Get(){ return Impl::Storage<T>::instance; }

	template<typename T>
	class Scoped
	{
	public:
		template<typename ... Args> Scoped(Args... args):value(args...){ Impl::Storage<T>::instance = &value; }
		~Scoped(){ Impl::Storage<T>::instance = nullptr; }

		T& Get() { return value; }
		const T& Get() const { return value; }

	private:
		T value;
	};

}