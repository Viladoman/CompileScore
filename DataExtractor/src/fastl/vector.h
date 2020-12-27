#pragma once

#ifdef USE_FASTL

//Forward declare the placement new in order to avoid #include <new> 
void* operator new  (size_t size, void* ptr) noexcept; 

namespace fastl 
{ 
	//------------------------------------------------------------------------------------------
	//Consider moving this around if needed somewhere else 
	template <class T> struct remove_reference { typedef T type; };
	template <class T> struct remove_reference<T&> { typedef T type; };
	template <class T> struct remove_reference<T&&> { typedef T type; };
	template <typename T> typename remove_reference<T>::type&& move(T&& arg) { return static_cast<typename remove_reference<T>::type&&>(arg); }

	template <bool, typename T = void> struct enable_if {};
	template <typename T> struct enable_if<true, T> { typedef T type; };
	template <bool B, typename T = void> using enable_if_t = typename enable_if<B, T>::type;

	template<typename T, typename ... Args> void Construct(T* ptr, Args&&... args) { new (ptr) T(move(args)...); }

	template<typename T> T* CreateBuffer(size_t size){ return (T*) new char[size*sizeof(T)]; }
	template<typename T> void DestroyBuffer(T* buffer){ delete[] reinterpret_cast<char*>(buffer); }

	////////////////////////////////////////////////////////////////////////////////////////////
	template<typename T> class vector
	{ 
	private: 
		enum { DEFAULT_CAPACITY_SIZE = 8 };
	public:
		typedef T        value_type;
		typedef size_t   size_type;

		typedef T*       iterator;
		typedef const T* const_iterator;
		typedef T&       reference;
		typedef const T& const_reference;

	public:
		vector();
		explicit vector(size_t size);

		//If more than 1 argument is provided we assume that we want to construct the vector with its elements ( using SFINAE - fake initializer list )
		template<typename ... Args, enable_if_t<(sizeof...(Args) > 1)>* = nullptr> 
		vector(Args&&... args) : m_data(CreateBuffer<T>(sizeof...(Args))), m_size(0u), m_capacity(sizeof...(Args))
		{ 
			(emplace_back(args),...); 
		}

		vector(const vector<T>& input);
		vector(vector<T>&& input);
		~vector();

		vector<T>& operator = (const vector<T>& t);
		vector<T>& operator = (vector<T>&& t);

		reference operator[](size_type index) { return m_data[index]; }
		const_reference operator[](size_type index) const { return m_data[index]; }

		size_type size() const{ return m_size; }
		size_type capacity() const { return m_capacity; }
		
		iterator begin() { return m_data; }
		const_iterator begin() const { return m_data; }
		iterator end() { return m_data+m_size;	} 
		const_iterator end() const { return m_data+m_size;	}
		reference back() { return m_data[m_size-1]; }
		bool empty() const { return m_size == 0u; }

		void reserve(const size_type size);
		void resize(const size_type size);
		void clear();

		void push_back(const value_type& value);
		iterator insert(iterator it,const value_type& value);
		template<typename ... Args> iterator emplace(iterator it, Args&&... args);
		template<typename ... Args> void emplace_back(Args&&... args);

		void pop_back();

		iterator erase(iterator it);
		iterator erase(iterator fromIt,iterator toIt); 

	private: 
		void Destroy();

	private:
		value_type* m_data;
		size_type   m_size;
		size_type   m_capacity;
	};

	//Implementation

	//------------------------------------------------------------------------------------------
	template<typename T> vector<T>::vector()
		: m_data(nullptr)
		, m_size(0u)
		, m_capacity(0u)
	{
	}

	//------------------------------------------------------------------------------------------
	template<typename T> vector<T>::vector(size_t size)
		: m_data(CreateBuffer<T>(size))
		, m_size(size)
		, m_capacity(size)
	{ 
		//Call the default constructor for all preallocated elements
		for (size_type i = 0u; i < m_size; ++i)
		{
			Construct<T>(&m_data[i]); 
		}
	}

	//------------------------------------------------------------------------------------------
	template<typename T> vector<T>::vector(const vector<T>& input)
		: m_data(CreateBuffer<T>(input.m_capacity))
		, m_size(input.m_size)
		, m_capacity(input.m_capacity)
	{
		for (size_t i = 0u; i < m_size; ++i)
		{
			Construct<T>(&m_data[i]); 
			m_data[i] = input[i];
		} 
	}

	//------------------------------------------------------------------------------------------
	template<typename T> vector<T>::vector(vector<T>&& input)
		: m_data(move(input.m_data))
		, m_size(move(input.m_size))
		, m_capacity(move(input.m_capacity))
	{ 
		input.m_data = nullptr;
		input.m_size = 0u; 
		input.m_capacity = 0u;
	}

	//------------------------------------------------------------------------------------------
	template<typename T> vector<T>::~vector() 
	{ 
		Destroy();
	}

	//------------------------------------------------------------------------------------------
	template<typename T> inline vector<T>& vector<T>::operator= (const vector<T>& input)
	{
		clear();
		reserve(input.m_capacity);
		m_size = input.m_size;
		for (size_type i = 0u; i < m_size; ++i)
		{
			Construct<T>(&m_data[i], input[i]);
		}
		return *this;
	}

	//------------------------------------------------------------------------------------------
	template<typename T> vector<T>& vector<T>::operator = (vector<T>&& t)
	{
		if (this != &t) 
		{ 
			Destroy(); 
			m_data = move(t.m_data);
			m_size = move(t.m_size); 
			m_capacity = move(t.m_capacity);
			t.m_data = nullptr;
			t.m_size = 0u;
			t.m_capacity = 0u; 
		}
		return *this;
	}

	//------------------------------------------------------------------------------------------
	template<typename T> inline void vector<T>::reserve(const size_type size)
	{
		if (size > m_capacity)
		{ 
			m_capacity = size;
			T* newData = CreateBuffer<T>(m_capacity);

			for (size_type i = 0u; i < m_size; ++i)
			{
				Construct<T>(&newData[i],move(m_data[i]));
				m_data[i].~T();
			}

			DestroyBuffer(m_data);
			m_data = newData;
			
		}
	}

	//------------------------------------------------------------------------------------------
	template<typename T> inline void vector<T>::resize(const size_type size)
	{
		reserve(size);

		for (size_type i=size;i<m_size;++i)
		{
			m_data[i].~T();
		}

		for (size_type i=m_size;i<size;++i)
		{
			Construct<T>(&m_data[i]);
		}

		m_size = size;
	}

	//------------------------------------------------------------------------------------------
	template<typename T> inline void vector<T>::clear()
	{
		resize(0u);
	}

	//------------------------------------------------------------------------------------------
	template<typename T> inline void vector<T>::push_back(const value_type& value)
	{
		emplace(end(), value);
	}

	//------------------------------------------------------------------------------------------
	template<typename T> inline typename vector<T>::iterator vector<T>::insert(iterator it,const value_type& value)
	{ 
		return emplace(it, value);
	}

	//------------------------------------------------------------------------------------------
	template<typename T> template<typename ... Args> void vector<T>::emplace_back(Args&&... args)
	{
		emplace(end(),move(args)...);
	}

	//------------------------------------------------------------------------------------------
	template<typename T> inline void vector<T>::pop_back()
	{ 
		if (!empty())
		{ 
			resize(m_size-1);
		}
	}

	//------------------------------------------------------------------------------------------
	template<typename T> template<typename ... Args> typename vector<T>::iterator vector<T>::emplace(iterator it, Args&&... args)
	{ 
		const size_type index = it-begin();

		if (m_size == m_capacity)
		{ 
			reserve(m_capacity == 0u? DEFAULT_CAPACITY_SIZE : 2u*m_capacity);
		}

		iterator insertIt = begin() + index; //this is important as reserve might move the memory around
		iterator endIt = end();

		if(endIt == insertIt)
		{ 
			Construct<T>(insertIt,move(args)...);
		}
		else
		{
			//Build the new element 
			Construct<T>(end(),move(*(end()-1))); 

			//Shift remaining elements
			for (iterator i = end()-1; i > insertIt;--i)
			{
				*i = move(*(i-1));
			}	

			*insertIt = T(move(args)...);
		}

		++m_size;

		return insertIt;
	}

	//------------------------------------------------------------------------------------------
	template<typename T> inline typename vector<T>::iterator vector<T>::erase(iterator it)
	{ 
		return erase(it,it+1);
	}

	//------------------------------------------------------------------------------------------
	template<typename T> inline typename vector<T>::iterator vector<T>::erase(iterator fromIt, iterator toIt)
	{
		const size_type rangeSize = toIt-fromIt;
		const_iterator batchEndIt = end()-rangeSize;

		for (iterator i = fromIt; i < batchEndIt; ++i)
		{
			*i = move(*(i + rangeSize));
		}

		resize(m_size - rangeSize);
		return fromIt;
	}

	//------------------------------------------------------------------------------------------
	template<typename T> void vector<T>::Destroy()
	{
		for (size_type i=0u;i<m_size;++i)
		{
			m_data[i].~T();
		}

		DestroyBuffer(m_data);
	}
}

#else

#include <vector>

namespace fastl
{ 
	template<typename T> using vector = std::vector<T>;
}

#endif //USE_FASTL


#ifdef FASTL_EXPOSE_PLAIN_ALIAS

template<typename T> using vector = fastl::vector<T>;

#endif //FASTL_EXPOSE_PLAIN_ALIAS