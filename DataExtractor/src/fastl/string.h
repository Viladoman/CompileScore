#pragma once

#ifdef USE_FASTL

#include "vector.h"

namespace fastl
{
	//------------------------------------------------------------------------------------------
	template<typename TChar> size_t ComputeStrLen(const TChar* str)
	{
		size_t ret; 
		for (ret = 0u; str[ret] != '\0';++ret){}
		return ret; 
	} 

	//------------------------------------------------------------------------------------------
	template<typename TChar> int ComputeStrCmp(const TChar* a, const TChar* b)
	{
		for (size_t i = 0; ;++i)
		{
			if (a[i] != b[i]) return a[i] < b[i] ? -1 : 1;
			else if (a[i] == '\0') return 0;
		}
	}

	////////////////////////////////////////////////////////////////////////////////////////////
	// Build string as a vector<char>
	template<typename TChar>
	class StringImpl
	{
	private:
		typedef vector<TChar> TData;
	public:
		typedef TChar value_type;
		typedef typename TData::size_type size_type;

		static constexpr size_type npos = -1;
	public:
		StringImpl();
		StringImpl(const char* input);
		StringImpl(const char* input, const size_type length);

		void clear();

		bool empty() const { return size() == 0u; }
		size_type size() const { return m_data.empty() ? 0 : m_data.size() - 1; }
		size_type length() const { return size(); }

		value_type* begin() { return m_data.begin(); }
		const value_type* begin() const { return m_data.begin(); }
		value_type* end() { return m_data.end() - 1; }
		const value_type* end() const { return m_data.end() - 1; }

		const value_type* c_str() const { return m_data.begin(); }

		value_type& operator[](size_type index) { return m_data[index]; }
		value_type  operator[](size_type index) const { return m_data[index]; }

		StringImpl& erase( size_type index){ m_data.erase(m_data.begin()+index); return *this; }
		StringImpl& erase( size_type index, size_type count){ m_data.erase(m_data.begin()+index,m_data.begin()+index+count); return *this; }

		StringImpl<TChar> operator+(const char c);
		StringImpl<TChar> operator+(const char* str);
		StringImpl<TChar> operator+(const StringImpl<TChar>& str);

		void operator += (const char c) { m_data.insert(m_data.end()-1,c); }
		void operator += (const char* str) { Append(str,ComputeStrLen(str)); }
		void operator += (const StringImpl<TChar>& str) { Append(str.c_str(), str.size()); }

		bool operator == (const char* str) const { return ComputeStrCmp(c_str(), str) == 0; }
		bool operator != (const char* str) const { return ComputeStrCmp(c_str(), str) != 0; }
		bool operator <  (const char* str) const { return ComputeStrCmp(c_str(), str) < 0; }
		bool operator >  (const char* str) const { return ComputeStrCmp(c_str(), str) > 0; }

		bool operator == (const StringImpl<TChar>& str) const { return *this == str.c_str(); }
		bool operator != (const StringImpl<TChar>& str) const { return *this != str.c_str(); }
		bool operator <  (const StringImpl<TChar>& str) const { return *this < str.c_str(); }
		bool operator >  (const StringImpl<TChar>& str) const { return *this > str.c_str(); }

	private: 
		void Append(const char* str, const size_type appendSize);

	private: 
		TData m_data;
	};

	//Implementation

	//------------------------------------------------------------------------------------------
	template<typename TChar> StringImpl<TChar>::StringImpl()
	{ 
		clear();
	}

	//------------------------------------------------------------------------------------------
	template<typename TChar> StringImpl<TChar>::StringImpl(const char* input)
	{ 
		clear(); 
		Append(input, ComputeStrLen(input));
	}

	//------------------------------------------------------------------------------------------
	template<typename TChar> StringImpl<TChar>::StringImpl(const char* input, const size_type length)
	{ 
		clear();
		Append(input, length);
	}

	//------------------------------------------------------------------------------------------
	template<typename TChar> inline void StringImpl<TChar>::clear() 
	{ 
		m_data.resize(1); 
		m_data[0] = '\0'; 
	}

	//------------------------------------------------------------------------------------------
	template<typename TChar> StringImpl<TChar> StringImpl<TChar>::operator+(const char c)
	{ 
		StringImpl<TChar> ret = *this; 
		ret += c; 
		return ret;
	}
	//------------------------------------------------------------------------------------------
	template<typename TChar> StringImpl<TChar> StringImpl<TChar>::operator+(const char* str)
	{ 
		StringImpl<TChar> ret = *this;
		ret += str;
		return ret; 
	} 

	//------------------------------------------------------------------------------------------
	template<typename TChar> StringImpl<TChar> StringImpl<TChar>::operator+(const StringImpl<TChar>& str)
	{ 
		StringImpl<TChar> ret = *this;
		ret += str;
		return ret; 
	}

	//------------------------------------------------------------------------------------------
	template<typename TChar> void StringImpl<TChar>::Append(const char* str, const size_type appendSize)
	{ 
		size_type writeIndex = size();
		m_data.resize(m_data.size()+appendSize);
		for (size_type i = 0; i < appendSize; ++i, ++writeIndex)
		{
			m_data[writeIndex] = str[i];
		}
		m_data.back() = '\0';
	}	

	using string = StringImpl<char>;
	using wstring = StringImpl<wchar_t>;
}

#else

#include <string>

namespace fastl
{ 
  using string = std::string;
  using wstring = std::wstring;
}

#endif //USE_FASTL

#ifdef FASTL_EXPOSE_PLAIN_ALIAS

using string = fastl::string;
using wstring = fastl::wstring;

#endif //FASTL_EXPOSE_PLAIN_ALIAS