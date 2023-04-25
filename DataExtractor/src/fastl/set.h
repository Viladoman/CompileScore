#pragma once 

#ifdef USE_FASTL

#include "algorithm.h"
#include "vector.h"

namespace fastl
{
	////////////////////////////////////////////////////////////////////////////////////////////
	// Build map as a vectorMap
	template<typename TKey>
	class set
	{
	private:
		typedef vector<TKey> TData;

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
		size_type size() const { return m_data.size(); }

		void clear() { m_data.clear(); }

		template< class... Args > pair<iterator, bool> emplace( Args&&... args );
		pair<iterator, bool> insert( TKey& key );

		void erase( iterator it ) { m_data.erase( it ); }
		size_type erase( const TKey& key );

		iterator find( const TKey& key );
		const_iterator find( const TKey& key ) const;

	private:
		TData m_data;
	};

	// Implementation

	//------------------------------------------------------------------------------------------
	template<typename TKey>
	template<class... Args > pair<typename set<TKey>::iterator, bool> set<TKey>::emplace( Args&&... args )
	{
		TKey inputValue{ args... };
		iterator entryIt = fastl::lower_bound( begin(), end(), inputValue, [=]( value_type& a, const value_type& b ) {return a < b; } );
		if( entryIt == end() || *entryIt != inputValue )
		{
			entryIt = m_data.emplace( entryIt, args... );
			return pair<iterator, bool>( entryIt, true );
		}
		return pair<iterator, bool>( entryIt, false );
	}

	//------------------------------------------------------------------------------------------
	template<typename TKey> pair<typename set<TKey>::iterator, bool> set<TKey>::insert( TKey& key )
	{
		iterator entryIt = fastl::lower_bound(begin(), end(), key, [=](value_type& a, const value_type& b) {return a < b; });
		if (entryIt == end() || *entryIt != key)
		{
			entryIt = m_data.insert(entryIt, key);
			return pair<iterator, bool>(entryIt, true);
		}
		return pair<iterator, bool>(entryIt, false);
	}

	//------------------------------------------------------------------------------------------
	template<typename TKey> typename set<TKey>::size_type set<TKey>::erase( const TKey& key )
	{
		iterator found = find( key );
		if( found != end() )
		{
			erase( found );
		}
		return size();
	}

	//------------------------------------------------------------------------------------------
	template<typename TKey> typename set<TKey>::iterator set<TKey>::find( const TKey& key )
	{
		iterator found = fastl::lower_bound( begin(), end(), key, [=]( const TKey& value, const TKey& key ) {return value < key; } );
		return found != end() && *found == key ? found : end();
	}

	//------------------------------------------------------------------------------------------
	template<typename TKey> typename set<TKey>::const_iterator set<TKey>::find( const TKey& key ) const
	{
		const_iterator found = fastl::lower_bound( begin(), end(), key, [=]( const TKey& value, const TKey& key ) {return value < key; } );
		return found != end() && *found == key ? found : end();
	}

}

#else 

#include <set>

namespace fastl
{
	template<typename TKey> using set = std::set<TKey>;
}

#endif //USE_FASTL

#ifdef FASTL_EXPOSE_PLAIN_ALIAS

template<typename TKey> using set = fastl::set<TKey>;

#endif //FASTL_EXPOSE_PLAIN_ALIAS