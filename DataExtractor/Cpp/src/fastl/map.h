#pragma once 

#ifdef USE_FASTL

#include "vector.h"
#include "pair.h"
#include "algorithm.h"

namespace fastl
{
	////////////////////////////////////////////////////////////////////////////////////////////
	// Build map as a vectorMap
	template<typename TKey, typename TValue>
	class map
	{
	private: 
		typedef vector<pair<TKey, TValue>> TData;

	public:
		typedef typename TData::iterator       iterator; 
		typedef typename TData::const_iterator const_iterator; 
		typedef typename TData::value_type     value_type;
		typedef typename TData::size_type      size_type;
		typedef value_type&                    reference;
		typedef const value_type&              const_reference;

	public:
		iterator begin() { return m_data.begin(); }
		const_iterator begin() const { return m_data.begin(); }
		iterator end() { return m_data.end(); }
		const_iterator end() const { return m_data.end(); }

		bool empty() const { return m_data.empty(); }
		size_type size() const { return m_data.size();  }

		TValue& operator[]( const TKey& key );

		void clear() { m_data.clear(); }
		
		iterator insert(iterator hint, const value_type& value) { return m_data.insert(hint, value); }
		iterator insert(const_iterator hint, const value_type& value) { return m_data.insert(hint, value); }

		void erase(iterator it) { m_data.erase(it);  }
		size_type erase(const TKey& key);
		
		iterator find( const TKey& key );
		const_iterator find( const TKey& key ) const;

	private: 
		TData m_data;
	};

	// Implementation

	//------------------------------------------------------------------------------------------
	template<typename TKey, typename TValue> TValue& map<TKey,TValue>::operator[]( const TKey& key )
	{ 
		iterator entryIt = fastl::lower_bound(begin(), end(), key, [=](value_type& value, const TKey& key) {return value.first < key; });
		if (entryIt == end() || entryIt->first != key)
		{ 
			entryIt = m_data.emplace(entryIt,key,TValue());
		}

		return entryIt->second;
	}

	//------------------------------------------------------------------------------------------
	template<typename TKey, typename TValue> typename map<TKey,TValue>::size_type map<TKey,TValue>::erase(const TKey& key)
	{ 
		iterator found = find(key);
		if (found != end())
		{
			erase(found);
		} 
		return size();
	}

	//------------------------------------------------------------------------------------------
	template<typename TKey, typename TValue> typename map<TKey,TValue>::iterator map<TKey,TValue>::find( const TKey& key )
	{ 
		iterator found = fastl::lower_bound(begin(), end(), key, [=](value_type& value, const TKey& key) {return value.first < key; });
		return found != end() && found->first == key ? found : end();
	}

	//------------------------------------------------------------------------------------------
	template<typename TKey, typename TValue> typename map<TKey, TValue>::const_iterator map<TKey, TValue>::find(const TKey& key) const
	{ 
		const_iterator found = fastl::lower_bound(begin(), end(), key, [=](value_type& value, const TKey& key) {return value.first < key; });
		return found != end() && found->first == key ? found : end();
	}

}

#else 

#include <map>

namespace fastl
{
	template<typename TKey, typename TValue> using map = std::map<TKey, TValue>;
}

#endif //USE_FASTL

#ifdef FASTL_EXPOSE_PLAIN_ALIAS

template<typename TKey, typename TValue> using map = fastl::map<TKey, TValue>;

#endif //FASTL_EXPOSE_PLAIN_ALIAS