
#include "simpleHeader.h"

struct Local
{
	Test field;
};

struct LocalInherit : public Test, public Templ<Test>
{
};

Test FunctionDefinition( Local& l, const Test* t1, const Test** t2, Test t3)
{
	return Test();
}

void FunctionBody()
{
	Test   a;
	Test*  b = new Test();
	Test** c = &b;
	
	Local l;
	eEnum e;
	
	int val = factorial( 5 + B );
	
	b->number = SQUARE(val) * SQUARE(2.5f);
	
	b->method();
}