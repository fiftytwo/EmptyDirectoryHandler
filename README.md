# Empty Directory Handler

## Motivation

There is an old problem with empty Unity directories and Git. Unity always generates new meta files or delete them or delete directories making meta-chaos in Git. Unity 2020.1 (or maybe earlier?) made a step in this direction. Now Unity 2020.1 creates an empty directory if it sees corresponding meta file. But it still can't delete empty directory when corresponding meta file disappears from git. Instead Unity generates new meta file. Sometimes it leads to GUID conflicts which leave Asset Database in an invalid state. It breaks project search partially and some `AssetDatabase` methods may work incorrectly for some paths. One of the invalid state indicator is the following error message:

`gOnDemandAssets->empty() || GetOnDemandModeV2() != AssetDatabase::OnDemandMode::Off`

The above error comes with warning messages which can show what assets exactly are the root cause. So, empty directories may be that cause when Unity regenerates missing meta files.

This package implements AssetPostprocessor which monitors empty directories. It generates empty hidden file with a reserved name `.empty_directory` for empty directories. When something meaningfull appears in the directory this file is deleted. It makes project feel healthy and Git friendly in automatic way. Manual file stubs for empty directories are not so good because not every team member may be accurate enough to keep an eye on them, specially creative team members who don't like all this Git stuff.

## Installation

In Package Manager do `Add package from git URL...` and paste version tagged URL `https://github.com/fiftytwo/EmptyDirectoryHandler.git#v0.0.1`
