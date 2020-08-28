#pragma once 

#ifdef USE_FASTL

namespace fastl 
{ 
    //------------------------------------------------------------------------------------------
    template<class Iterator, class Predicate>
    constexpr Iterator find_if(Iterator first, Iterator last, Predicate p)
    {
        for (; first != last; ++first) 
        {
            if (p(*first)) return first;
        }
        return last;
    }

    //------------------------------------------------------------------------------------------
    template<class Iterator, class T>
    constexpr Iterator find(Iterator first, Iterator last, const T& value)
    {
        return find_if(first, last, [=](const T& input) { return input == value; });
    }

    //------------------------------------------------------------------------------------------
    template<class Iterator, class Predicate>
    Iterator remove_if(Iterator first, Iterator last, Predicate p)
    {
        first = fastl::find_if(first, last, p);
        if (first != last)
        {
            for(Iterator i = first; ++i != last; )
            { 
                if (!p(*i)) *first++ = *i;
            }
        }
        return first;
    }

    //------------------------------------------------------------------------------------------
    template< class Iterator, class T >
    Iterator remove(Iterator first, Iterator last, const T& value)
    {
        return remove_if(first, last, [=](const T& input) { return input == value; });
    }

    //------------------------------------------------------------------------------------------
    template<class Iterator, class T, class Compare>
    Iterator lower_bound(Iterator first, Iterator last, const T& value, Compare comp)
    {
        //CAUTION: Linear search instead of binary search
        for (; first != last && comp(*first, value); ++first) {}
        return first;
    }

    //------------------------------------------------------------------------------------------
    template<class Iterator, class T>
    Iterator lower_bound(Iterator first, Iterator last, const T& value)
    {
        return lower_bound(first, last, value, [=](const T& lhs, const T& rhs) { return lhs < rhs; });
    }


    //TODO ~ ramonv ~ add sort
}

#else 

#include <algorithm>

namespace fastl
{
    template<class Iterator, class T> constexpr Iterator find(Iterator first, Iterator last, const T& value) { return std::find(first, last, value); }
    template<class Iterator, class Predicate> constexpr Iterator find_if(Iterator first, Iterator last, Predicate p) { return std::find_if(first, last, p); }

    template<class Iterator, class T > inline Iterator remove(Iterator first, Iterator last, const T& value) { return std::remove(first, last, value); }
    template<class Iterator, class Predicate> inline Iterator remove_if(Iterator first, Iterator last, Predicate p) { return std::remove_if(first, last, p);  }

    template<class Iterator, class T> Iterator lower_bound(Iterator first, Iterator last, const T& value) { return std::lower_bound(first, last, value); }
    template<class Iterator, class T, class Compare> Iterator lower_bound(Iterator first, Iterator last, const T& value, Compare comp) { return std::lower_bound(first, last, value, comp);  }
} 

#endif //USE_FASTL

