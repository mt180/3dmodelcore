# 3dmodelcore-generator
a simple program for generating animated crossections of 3d models as sounds for viewing via vectorscope

blah blah MIT License blah blah i don't care do whatever you want with this thing

i made this after a conversation with a friend where they said my musical future was to "create a track that, when run through a vectorscope, provides detailed cross-sections of an actually physically feasible device that could send you anywhere in the universe at any point in time". i jokingly said "i could actually do that already. i have most of the knowledge required. except for the teleportation device part. the music would be easy at that point.". here's the music part. still working on the teleportation device

i think the output of this program counts as "music". it's hard to define music in a way that includes niche things and excludes whatever this is

## demo
wip

## usage
run the .exe in the same folder as (at least) the .dll and the .runtimeconfig.json files. i am unsure if the other two files are necessary on other devices, but it runs fine without them on mine.

- filepath: full path of the .obj file you want to generate (relative should work)
- axis: 1, 2, or 3. the axis the crossections will be perpendicular to
- number of splits: the number of crossections to compute
- samplerate: samplerate of the output audio. this will affect duration and pitch. higher = faster
- samples per slice: the duration of each split in audio samples. this will affect duration, pitch, quality, and filesize. higher = slower, larger, and higher fidelity image recreation
- output filepath: location and name of output .wav file. be sure to name with .wav extension

## the code
is a trainwreck. i follow precisely zero quality code standards and my data structures make no sense. it's also terribly optimized. if you need to do a huge number of splits on a large object file, it might just be over for you. i won't be accepting anyone else's commits on this thing but i might update it sometimes. you are more than welcome to fork this thing and make it into something usable
