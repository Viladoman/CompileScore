ROOT = ./

EXECUTABLE_NAME = ScoreDataExtractor
SOURCE_DIR = $(ROOT)src
TARGET_DIR = $(ROOT)bin/make
OBJECT_DIR = $(ROOT)tmp/make

HEADERS = $(shell find $(SOURCE_DIR) -name "*.h")
SOURCES = $(shell find $(SOURCE_DIR) -name "*.cpp") 
OBJECTS = $(SOURCES:$(SOURCE_DIR)/%.cpp=$(OBJECT_DIR)/%.o)
EXECUTABLE_FILE = $(EXECUTABLE_NAME:%=$(TARGET_DIR)/%)

INCLUDES =  -I$(ROOT)src

CXX = clang++
CXXFLAGS = -std=c++20 -O2 -Wall -c $(INCLUDES)

build: $(OBJECTS)
	@mkdir -p $(TARGET_DIR)
	@echo Linking...
	@$(CXX) $(LDFLAGS) -o $(EXECUTABLE_FILE) $(OBJECTS) $(LIBS)
	@echo "Build successful!"

clean:
	-rm $(OBJECTS)
	-rm $(EXECUTABLE_FILE)s

.PHONY: build clean

$(OBJECTS): $(OBJECT_DIR)/%.o: $(SOURCE_DIR)/%.cpp
	@echo Building $<
	@mkdir -p $(@D)
	@$(CXX) $(CXXFLAGS) -o $@ $< 
