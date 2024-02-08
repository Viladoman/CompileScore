ROOT = ./

TARGET_DIR = $(ROOT)bin/make/

HEADERS = $(shell find ./src/ -name "*.h")
SOURCES = $(shell find ./src/ -name "*.cpp") 
OBJECTS = $(SOURCES:.cpp=.o)

CXX = clang 
CXXFLAGS = -std=c++20 -O2 -Wall

INCLUDES =  -I$(ROOT)src

TARGET = $(TARGET_DIR)ScoreDataExtractor

%.o: %.cpp
	$(CXX) $(CXXFLAGS) -c $< -o $@ $(INCLUDES)

all: $(OBJECTS)
	$(CXX) $(LDFLAGS) -o $(TARGET) $(OBJECTS) $(LIBS)

clean:
	-rm $(OBJECTS)
	-rm $(TARGET)