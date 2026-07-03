.PHONY: all build deploy

all: build deploy

build:
	cd Source && dotnet build

deploy:
	./Source/deploy
