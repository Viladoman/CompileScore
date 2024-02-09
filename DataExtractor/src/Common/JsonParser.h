#pragma once

#include "BasicTypes.h"

namespace Json
{ 
	struct Token
	{ 
		enum class Type
		{ 
			Invalid,
			EndOfFile,
			ObjectOpen, 
			ObjectClose, 
			ArrayOpen, 
			ArrayClose,
			Undefined,
			Null,
			String, 
			Number,
			True,
			False
		};

		constexpr Token()
			: str(nullptr)
			, length(0u)
			, type(Type::Invalid)
		{}

		constexpr Token(
			const char* _str , 
			const U64 _length, 
			const Type _type
		)
			: str(_str)
			, length(_length)
			, type(_type)
		{}

		const char* str; 
		U64 length;
		Type  type; 
	};

	class Reader
	{ 
	public: 
		Reader(const char* buffer);
		bool NextToken(Token& token); 
		void SkipObject();
	private: 
		const char* cursor;
	};
}