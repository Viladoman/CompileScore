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
    const auto find        = std::find;
    const auto remove      = std::remove;
    const auto lower_bound = std::lower_bound;
} 

#endif //USE_FASTL
