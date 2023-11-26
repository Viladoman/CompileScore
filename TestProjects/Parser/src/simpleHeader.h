#pragma once

#define TAGMACRO
#define SQUARE(X) ((X)*(X))

enum eEnum
{
	A,
	B,
};

template<typename T>
class Templ
{
	T value;
};

struct Test
{
	void method(){}
	float number;
};

int factorial(int input)
{
	return input == 0? SQUARE(1) : input * factorial( input - 1 );
}